using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DriverBalance.Data;
using DriverBalance.Web.Tests.Data;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DriverBalance.Web.Tests.ApiTests;

internal static class McpHelpers
{
    public sealed record TokenBody(long Id, string Name, string Token);
    public sealed record TokenListItem(long Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

    public static async Task<TokenBody> CreateTokenAsync(HttpClient c, string name = "bot")
    {
        var r = await Api.PostAsync(c, "/api/tokens", new { name });
        r.EnsureSuccessStatusCode();
        return await Api.ReadAsync<TokenBody>(r);
    }

    public static async Task<McpClient> ConnectAsync(ApiFactory f, string token)
    {
        var http = f.CreateClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token },
        }, http, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    public static async Task<CallToolResult> CallAsync(McpClient mcp, string tool, object? args = null)
    {
        var dict = (args is null ? [] : JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.Clone()));
        return await mcp.CallToolAsync(tool, dict);
    }

    public static string TextOf(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(t => t.Text));

    public static JsonNode JsonOf(CallToolResult r) => JsonNode.Parse(TextOf(r))!;

    public static HttpRequestMessage RawMcpPost(string? bearer)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"),
        };
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Accept.ParseAdd("text/event-stream");
        if (bearer is not null) req.Headers.Authorization = new("Bearer", bearer);
        return req;
    }

    public static async Task<List<Api.VehicleBody>> VehiclesAsync(HttpClient c) =>
        await Api.ReadAsync<List<Api.VehicleBody>>(await c.GetAsync("/api/vehicles"));
}

public sealed class McpTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Mcp_NoToken_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(null))).StatusCode);

    [Fact]
    public async Task Mcp_TestUserHeaderWithoutBearer_401()
    {
        // The Test scheme is the default here and would sign this request in; /mcp must not accept it.
        var c = f.ClientFor(1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(McpHelpers.RawMcpPost(null))).StatusCode);
    }

    [Theory]
    [InlineData("dbk_notarealtoken")]
    [InlineData("garbage")]
    public async Task Mcp_UnknownToken_401(string token) =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(token))).StatusCode);

    [Fact]
    public async Task Mcp_ListTools_ContainsAll()
    {
        var token = await McpHelpers.CreateTokenAsync(f.ClientFor(1));
        await using var mcp = await McpHelpers.ConnectAsync(f, token.Token);
        var names = (await mcp.ListToolsAsync()).Select(t => t.Name).ToHashSet();
        string[] expected = ["add_expense", "log_fuel", "log_maintenance", "swap_tyres", "list_tyre_sets",
            "list_recent_expenses", "delete_expense", "get_stats", "import_trips_csv"];
        foreach (var name in expected) Assert.Contains(name, names);
    }

    [Fact]
    public async Task Mcp_AddExpense_LandsOnDefaultVehicleOfTokenUser()
    {
        f.EnsureUser(2);
        var user2 = f.ClientFor(2);
        var first = await Api.NewVehicleAsync(user2, "Second-user first");
        var second = (await Api.ReadAsync<Api.VehicleBody>(await Api.PostAsync(user2, "/api/vehicles",
            new { name = "Second-user default", isDefault = true }))).Id;
        var otherUsersVehicle = await Api.NewVehicleAsync(f.ClientFor(1), "User 1 car");

        var token = await McpHelpers.CreateTokenAsync(user2);
        await using var mcp = await McpHelpers.ConnectAsync(f, token.Token);
        var result = await McpHelpers.CallAsync(mcp, "add_expense", new { category = "Parking", amount = 120, note = "garage" });

        Assert.NotEqual(true, result.IsError);
        var json = McpHelpers.JsonOf(result);
        Assert.Equal("Parking", (string)json["category"]!);
        Assert.Equal("CZK", (string)json["currency"]!);
        Assert.Equal("2026-11-08", (string)json["date"]!);
        Assert.Equal("Mcp", (string)json["source"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)json["summary"]));
        Assert.False((bool)json["needsConversion"]!);

        var onDefault = await Api.ReadAsync<List<JsonElement>>(await user2.GetAsync($"/api/vehicles/{second}/expenses"));
        Assert.Single(onDefault);
        Assert.Equal((long)json["id"]!, onDefault[0].GetProperty("id").GetInt64());
        Assert.Equal("[]", await (await user2.GetAsync($"/api/vehicles/{first}/expenses")).Content.ReadAsStringAsync());
        Assert.Equal("[]", await (await f.ClientFor(1).GetAsync($"/api/vehicles/{otherUsersVehicle}/expenses")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mcp_NoDefaultVehicle_UsesFirst_AndExplicitVehicleOfAnotherUser_IsError()
    {
        f.EnsureUser(3);
        var user3 = f.ClientFor(3);
        var mine = await Api.NewVehicleAsync(user3, "Mine"); // first vehicle becomes default
        var foreign = await Api.NewVehicleAsync(f.ClientFor(1), "Foreign");

        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(user3)).Token);
        var ok = await McpHelpers.CallAsync(mcp, "add_expense", new { category = "Wash", amount = 50 });
        Assert.NotEqual(true, ok.IsError);
        Assert.Single(await Api.ReadAsync<List<JsonElement>>(await user3.GetAsync($"/api/vehicles/{mine}/expenses")));

        var bad = await McpHelpers.CallAsync(mcp, "add_expense", new { vehicleId = foreign, category = "Wash", amount = 50 });
        Assert.True(bad.IsError);
        Assert.Contains("not found", McpHelpers.TextOf(bad), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("[]", await (await f.ClientFor(1).GetAsync($"/api/vehicles/{foreign}/expenses")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mcp_ForeignCurrency_NeedsConversion()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        var bare = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "add_expense",
            new { vehicleId = vid, category = "Fuel", amount = 40, currency = "eur" }));
        Assert.True((bool)bare["needsConversion"]!);
        Assert.Equal("EUR", (string)bare["currency"]!);
        Assert.Null(bare["homeAmount"]);

        var withRate = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "add_expense",
            new { vehicleId = vid, category = "Fuel", amount = 40, currency = "EUR", rate = 25.2 }));
        Assert.False((bool)withRate["needsConversion"]!);
        Assert.Equal(1008m, (decimal)withRate["homeAmount"]!);

        var withHome = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "add_expense",
            new { vehicleId = vid, category = "Fuel", amount = 40, currency = "EUR", homeAmount = 990 }));
        Assert.False((bool)withHome["needsConversion"]!);
        Assert.Equal(990m, (decimal)withHome["homeAmount"]!);

        var stored = await Api.ReadAsync<List<JsonElement>>(await c.GetAsync($"/api/vehicles/{vid}/expenses"));
        Assert.Equal(3, stored.Count);
    }

    [Fact]
    public async Task Mcp_Validation_ReturnsReadableErrorResult()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        (string Tool, object Args, string Expect)[] cases =
        [
            ("add_expense", new { vehicleId = vid, category = "Fuel", amount = 0 }, "Amount must be greater than 0"),
            ("add_expense", new { vehicleId = vid, category = "Fuel", amount = 5, currency = "CZ" }, "3-letter"),
            ("add_expense", new { vehicleId = vid, category = "Nope", amount = 5 }, "category"),
            ("add_expense", new { vehicleId = vid, category = "Fuel", amount = 5, date = "yesterday" }, "date"),
            ("add_expense", new { vehicleId = vid, category = "Insurance", amount = 5, coversFrom = "2026-06-01", coversTo = "2026-05-01" }, "CoversFrom"),
            ("log_fuel", new { vehicleId = vid, litres = 0, totalPrice = 100 }, "Litres"),
            ("swap_tyres", new { vehicleId = vid, tyreSetId = 99999 }, "Tyre set"),
            ("log_maintenance", new { vehicleId = vid, types = new[] { "Flux" }, cost = 100 }, "Flux"),
            ("delete_expense", new { vehicleId = vid, id = 99999 }, "not found"),
            ("get_stats", new { vehicleId = vid, kind = "bogus" }, "kind"),
            ("add_expense", new { vehicleId = 99999, category = "Fuel", amount = 5 }, "not found"),
        ];
        foreach (var (tool, args, expect) in cases)
        {
            var r = await McpHelpers.CallAsync(mcp, tool, args);
            Assert.True(r.IsError == true, $"{tool} {JsonSerializer.Serialize(args)} should be an error");
            Assert.Contains(expect, McpHelpers.TextOf(r), StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("[]", await (await c.GetAsync($"/api/vehicles/{vid}/expenses")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mcp_FuelMaintenanceTyresListDelete_RoundTrip()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        var setId = (await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { name = "Winters", season = "Winter" })))
            .GetProperty("id").GetInt64();
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        var fuel = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "log_fuel",
            new { vehicleId = vid, litres = 42.5, totalPrice = 1700, odometer = 12345, station = "Shell", date = "2026-11-01" }));
        Assert.Equal("Fuel", (string)fuel["category"]!);
        Assert.Equal(42.5, (double)fuel["litres"]!);
        Assert.True((bool)fuel["fullTank"]!);
        Assert.Equal("Shell", (string)fuel["station"]!);

        var maint = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "log_maintenance",
            new { vehicleId = vid, types = new[] { "OilChange", "AirFilter" }, cost = 4500, description = "service" }));
        Assert.Equal("Maintenance", (string)maint["category"]!);
        Assert.Equal(["OilChange", "AirFilter"], maint["maintenance"]!.AsArray().Select(n => (string)n!).ToArray());

        var swap = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "swap_tyres", new { vehicleId = vid, tyreSetId = setId, odometer = 12400 }));
        Assert.Equal(setId, (long)swap["tyreSetId"]!);
        Assert.Equal(["TyreSwap"], swap["maintenance"]!.AsArray().Select(n => (string)n!).ToArray());

        var sets = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "list_tyre_sets", new { vehicleId = vid }));
        Assert.Equal("Winters", (string)sets["tyreSets"]![0]!["name"]!);

        var recent = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "list_recent_expenses", new { vehicleId = vid, count = 2 }));
        Assert.Equal(2, recent["expenses"]!.AsArray().Count);

        var deleted = await McpHelpers.CallAsync(mcp, "delete_expense", new { vehicleId = vid, id = (long)fuel["id"]! });
        Assert.NotEqual(true, deleted.IsError);
        var left = await Api.ReadAsync<List<JsonElement>>(await c.GetAsync($"/api/vehicles/{vid}/expenses"));
        Assert.Equal(2, left.Count);
    }

    [Fact]
    public async Task ZeroAmount_ValidOnlyForMaintenance_SameRulesForRestAndMcp()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        var free = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "log_maintenance", new { vehicleId = vid, types = new[] { "Coolant" }, cost = 0 }));
        var id = (long)free["id"]!;

        // REST can edit the zero-amount Maintenance expense MCP created.
        var put = await c.PutAsJsonAsync($"/api/vehicles/{vid}/expenses/{id}",
            new { date = "2026-11-01", category = "Maintenance", amount = 0, currency = "CZK", odometer = 5000, maintenance = new[] { "Coolant" } }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2026-11-01", category = "Fuel", amount = 0, currency = "CZK" })).StatusCode);
        Assert.True((await McpHelpers.CallAsync(mcp, "add_expense", new { vehicleId = vid, category = "Fuel", amount = 0 })).IsError);
        Assert.True((await McpHelpers.CallAsync(mcp, "log_maintenance", new { vehicleId = vid, types = new[] { "Coolant" }, cost = -1 })).IsError);
    }

    [Fact]
    public async Task Mcp_AddExpense_RateMustBePositive_PlaceStored_PlaceTooLongRejected()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        foreach (var rate in new[] { 0, -1.5 })
        {
            var bad = await McpHelpers.CallAsync(mcp, "add_expense", new { vehicleId = vid, category = "Parking", amount = 10, currency = "EUR", rate });
            Assert.True(bad.IsError);
            Assert.Contains("rate", McpHelpers.TextOf(bad));
        }

        var ok = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "add_expense", new { vehicleId = vid, category = "Parking", amount = 10, place = "  Garage X " }));
        Assert.Equal("Garage X", (string)ok["place"]!);

        var tooLong = await McpHelpers.CallAsync(mcp, "add_expense", new { vehicleId = vid, category = "Parking", amount = 10, place = new string('x', 101) });
        Assert.True(tooLong.IsError);
        Assert.Contains("Place", McpHelpers.TextOf(tooLong));
    }

    [Fact]
    public async Task Mcp_GetStats_RangeToIsInclusive_31Days()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2026-10-01", category = "Parking", amount = 10, currency = "CZK" });
        await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2026-10-31", category = "Parking", amount = 20, currency = "CZK" });
        await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2026-11-01", category = "Parking", amount = 40, currency = "CZK" });
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        var viaMcp = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "range", from = "2026-10-01", to = "2026-10-31" }));
        var viaApi = (await Api.ReadAsync<JsonNode>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats",
            new { periods = new[] { new { from = "2026-10-01", to = "2026-11-01", label = "2026-10-01..2026-10-31" } } })))[0]!;
        Assert.True(JsonNode.DeepEquals(viaApi, viaMcp["stats"]));
        Assert.Equal(30m, (decimal)viaMcp["stats"]!["money"]!["total"]!);
        Assert.Contains("01/10/2026 – 31/10/2026", (string)viaMcp["summary"]!);
    }

    [Fact]
    public async Task Mcp_GetStats_RangeToBeforeFrom_IsError()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);
        Assert.True((await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "range", from = "2026-10-31", to = "2026-10-01" })).IsError);
    }

    [Fact]
    public async Task Mcp_GetStats_EqualsApiStats()
    {
        var c = f.ClientFor(1);
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2025-01-10", category = "Fuel", amount = 1500, currency = "CZK", litres = 40.0, fullTank = true });
        await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2025-01-20", category = "Maintenance", amount = 3000, currency = "CZK", maintenance = new[] { "OilChange" }, odometer = 1500 });
        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);

        // range
        var viaMcp = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "range", from = "2025-01-01", to = "2025-01-31" }));
        var viaApi = (await Api.ReadAsync<JsonNode>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats",
            new { periods = new[] { new { from = "2025-01-01", to = "2025-02-01", label = "2025-01-01..2025-01-31" } } })))[0]!;
        Assert.True(JsonNode.DeepEquals(viaApi, viaMcp["stats"]), $"range differs:\nAPI {viaApi}\nMCP {viaMcp["stats"]}");
        Assert.NotNull(viaMcp["parameters"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)viaMcp["summary"]));
        Assert.Contains("01/01/2025 – 31/01/2025", (string)viaMcp["summary"]!);

        // calendar month
        var cal = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "calendar", year = 2025, month = 1 }));
        var calApi = (await Api.ReadAsync<JsonNode>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats",
            new { periods = new[] { new { from = "2025-01-01", to = "2025-02-01", label = "2025-01" } } })))[0]!;
        Assert.True(JsonNode.DeepEquals(calApi["usage"], cal["stats"]!["usage"]));

        // interval
        var interval = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "interval", maintenanceType = "OilChange" }));
        var intervalsApi = (await Api.ReadAsync<JsonNode>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/intervals", new { type = new[] { "OilChange" } })))[0]!;
        Assert.True(JsonNode.DeepEquals(intervalsApi["stats"], interval["stats"]));

        var badIndex = await McpHelpers.CallAsync(mcp, "get_stats", new { vehicleId = vid, kind = "interval", maintenanceType = "OilChange", intervalIndex = 50 });
        Assert.True(badIndex.IsError);
    }

    [Fact]
    public async Task Mcp_ImportCsv_KeepsNbspAndCrLf()
    {
        var c = f.ClientFor(1);
        var viaMcpVehicle = await Api.NewVehicleAsync(c, "mcp import");
        var viaApiVehicle = await Api.NewVehicleAsync(c, "api import");
        var expected = await Api.ImportFixtureAsync(c, viaApiVehicle);

        var csv = await File.ReadAllTextAsync(TestDb.Fixture("synthetic-trips.csv"));
        Assert.Contains(' ', csv);
        Assert.Contains("\r\n", csv);

        await using var mcp = await McpHelpers.ConnectAsync(f, (await McpHelpers.CreateTokenAsync(c)).Token);
        var result = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "import_trips_csv", new { vehicleId = viaMcpVehicle, csvText = csv }));
        Assert.Equal(expected.Imported, (int)result["imported"]!);
        Assert.True((int)result["imported"]! > 0);
        Assert.False(string.IsNullOrWhiteSpace((string?)result["summary"]));
    }

    [Fact]
    public async Task Token_Lifecycle_ShownOnce_StoresHashOnly_Lists_Revokes_401()
    {
        var c = f.ClientFor(1);
        var created = await McpHelpers.CreateTokenAsync(c, "assistant");
        Assert.StartsWith("dbk_", created.Token);
        Assert.Matches("^dbk_[A-Za-z0-9_-]{43}$", created.Token); // 32 bytes, base64url, no padding

        using (var scope = f.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DriverBalanceDbContext>>();
            using var db = new DriverBalanceDbContext(options, new FixedUser(1));
            var row = db.ApiTokens.Single(t => t.Id == created.Id);
            Assert.DoesNotContain(created.Token, row.TokenHash);
            Assert.NotEqual(created.Token, row.TokenHash);
            Assert.Null(row.LastUsedAt);
        }

        var listJson = await (await c.GetAsync("/api/tokens")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(created.Token, listJson);
        var list = JsonSerializer.Deserialize<List<McpHelpers.TokenListItem>>(listJson, ApiJson.Options)!;
        var item = Assert.Single(list, t => t.Id == created.Id);
        Assert.Equal("assistant", item.Name);
        Assert.Null(item.LastUsedAt);

        // Token works and stamps LastUsedAt.
        var ok = await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(created.Token));
        Assert.NotEqual(HttpStatusCode.Unauthorized, ok.StatusCode);
        var used = (await Api.ReadAsync<List<McpHelpers.TokenListItem>>(await c.GetAsync("/api/tokens"))).Single(t => t.Id == created.Id);
        Assert.NotNull(used.LastUsedAt);

        // Revoke.
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/tokens/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(created.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/tokens/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Token_Revoked_401()
    {
        var c = f.ClientFor(1);
        var token = await McpHelpers.CreateTokenAsync(c);
        Assert.NotEqual(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(token.Token))).StatusCode);
        await c.DeleteAsync($"/api/tokens/{token.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(token.Token))).StatusCode);
    }

    [Fact]
    public async Task Tokens_AreUserScoped_NameRequired()
    {
        f.EnsureUser(4);
        var mine = await McpHelpers.CreateTokenAsync(f.ClientFor(1), "mine");
        var other = f.ClientFor(4);
        Assert.DoesNotContain(await Api.ReadAsync<List<McpHelpers.TokenListItem>>(await other.GetAsync("/api/tokens")), t => t.Id == mine.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/tokens/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(other, "/api/tokens", new { name = "  " })).StatusCode);
    }
}

public sealed class McpLocalAuthTests(LocalAuthFactory f) : IClassFixture<LocalAuthFactory>
{
    [Fact]
    public async Task Mcp_LocalUserCookieless_401()
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Fake-Remote", "127.0.0.1");
        // Same client, no bearer: the REST API accepts the loopback caller...
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/vehicles")).StatusCode);
        // ...but /mcp must not.
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(McpHelpers.RawMcpPost(null))).StatusCode);
    }

    [Fact]
    public async Task ApiToken_DoesNotAuthenticateRestApi()
    {
        var local = f.CreateClient();
        local.DefaultRequestHeaders.Add("X-Fake-Remote", "127.0.0.1");
        var token = await McpHelpers.CreateTokenAsync(local);

        // A remote caller (LocalUser refuses it) presenting a valid bearer token still gets no REST access.
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles");
        req.Headers.Add("X-Fake-Remote", "192.168.1.20");
        req.Headers.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Mcp_TokenCreatedViaLocalUser_Works()
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Fake-Remote", "127.0.0.1");
        var token = await McpHelpers.CreateTokenAsync(c);
        var r = await f.CreateClient().SendAsync(McpHelpers.RawMcpPost(token.Token));
        Assert.NotEqual(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
