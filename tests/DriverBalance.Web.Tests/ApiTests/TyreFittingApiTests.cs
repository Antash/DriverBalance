using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class TyreFittingApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Swap(string date, object? tyres = null, long? tyreSetId = null, string category = "Maintenance", bool withSwapItem = true) => new
    {
        date, category, amount = 1000, currency = "CZK", tyreSetId, tyres,
        work = withSwapItem ? new object[] { new { type = "TyreSwap", part = "None" } } : Array.Empty<object>(),
    };

    private static async Task<long> NewSetAsync(HttpClient c, long vid, string name)
    {
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { name, season = "Summer" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<JsonElement> SetAsync(HttpClient c, long vid, long id)
    {
        var all = JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/tyres")).RootElement;
        return all.EnumerateArray().First(x => x.GetProperty("id").GetInt64() == id).Clone();
    }

    private static async Task<(long Id, long SetId, string Json)> PostAsync(HttpClient c, long vid, object body)
    {
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", body);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var e = JsonDocument.Parse(json).RootElement;
        var set = e.GetProperty("tyreSetId");
        return (e.GetProperty("id").GetInt64(), set.ValueKind == JsonValueKind.Null ? 0 : set.GetInt64(), json);
    }

    [Fact]
    public async Task NewTyres_CreatesSetAndRetiresPrevious()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Old set");
        await PostAsync(c, vid, Swap("2026-03-01", tyreSetId: a));

        var (_, setId, json) = await PostAsync(c, vid, Swap("2026-06-01", new { brand = "Nokian", model = "Snowproof 2", size = "205/55 R16", season = "Winter", isNew = true }));
        Assert.DoesNotContain("\"tyres\"", json);
        var s = await SetAsync(c, vid, setId);
        Assert.Equal("Nokian Snowproof 2", s.GetProperty("name").GetString());
        Assert.Equal("Nokian", s.GetProperty("brand").GetString());
        Assert.Equal("Snowproof 2", s.GetProperty("model").GetString());
        Assert.Equal("205/55 R16", s.GetProperty("size").GetString());
        Assert.Equal("Winter", s.GetProperty("season").GetString());
        Assert.False(s.GetProperty("retired").GetBoolean());
        Assert.True((await SetAsync(c, vid, a)).GetProperty("retired").GetBoolean());
    }

    [Fact]
    public async Task OwnedTyres_CreatesSetRetiresNothing()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Old set");
        await PostAsync(c, vid, Swap("2026-03-01", tyreSetId: a));
        var (_, setId, _) = await PostAsync(c, vid, Swap("2026-06-01", new { brand = "Michelin", season = "Summer", isNew = false }));
        Assert.NotEqual(a, setId);
        Assert.False((await SetAsync(c, vid, a)).GetProperty("retired").GetBoolean());
    }

    [Fact]
    public async Task NewTyres_RetiresInitialSet_WhenNoEarlierMount()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Initial");
        (await c.PutAsJsonAsync($"/api/vehicles/{vid}", new { name = "Car", initialTyreSetId = a }, ApiJson.Options)).EnsureSuccessStatusCode();
        await PostAsync(c, vid, Swap("2026-06-01", new { season = "Summer", isNew = true }));
        Assert.True((await SetAsync(c, vid, a)).GetProperty("retired").GetBoolean());
    }

    [Fact]
    public async Task DerivedName_WhenNoBrand()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var (_, s1, _) = await PostAsync(c, vid, Swap("2026-06-01", new { season = "Summer", isNew = true }));
        var (_, s2, _) = await PostAsync(c, vid, Swap("2026-07-01", new { season = "AllSeason", isNew = false }));
        var (_, s3, _) = await PostAsync(c, vid, Swap("2026-08-01", new { model = "Alpin 6", season = "Winter", isNew = false }));
        Assert.Equal("Summer tyres", (await SetAsync(c, vid, s1)).GetProperty("name").GetString());
        Assert.Equal("All-season tyres", (await SetAsync(c, vid, s2)).GetProperty("name").GetString());
        Assert.Equal("Alpin 6", (await SetAsync(c, vid, s3)).GetProperty("name").GetString());
    }


    [Fact]
    public async Task Update_OnUnlinkedSwap_AttachesSet_AndIgnoresItsOwnStaleRow()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Set A");
        await PostAsync(c, vid, Swap("2026-01-01", tyreSetId: a));
        var (id, _, _) = await PostAsync(c, vid, Swap("2026-01-15"));
        var put = await c.PutAsJsonAsync($"/api/vehicles/{vid}/expenses/{id}", Swap("2026-03-01", new { brand = "Pirelli", season = "Summer", isNew = true }), ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var newId = JsonDocument.Parse(await put.Content.ReadAsStringAsync()).RootElement.GetProperty("tyreSetId").GetInt64();
        Assert.NotEqual(a, newId);
        Assert.False((await SetAsync(c, vid, newId)).GetProperty("retired").GetBoolean());
        Assert.True((await SetAsync(c, vid, a)).GetProperty("retired").GetBoolean());
    }

    [Fact]
    public async Task Update_WithTyres_WhenAlreadyLinked_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Set A");
        var (id, _, _) = await PostAsync(c, vid, Swap("2026-03-01", tyreSetId: a));
        var put = await c.PutAsJsonAsync($"/api/vehicles/{vid}/expenses/{id}", Swap("2026-03-01", new { season = "Summer", isNew = true }), ApiJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Contains("This record already has a tyre set; pick a set instead, or edit the set's details from the tyre summary.", await put.Content.ReadAsStringAsync());
        Assert.Single(JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/tyres")).RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Tyres_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = await NewSetAsync(c, vid, "Set");
        var t = new { season = "Summer", isNew = true };
        var url = $"/api/vehicles/{vid}/expenses";
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, url, Swap("2026-06-01", t, category: "Fuel", withSwapItem: false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, url, Swap("2026-06-01", t, withSwapItem: false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, url, Swap("2026-06-01", t, tyreSetId: a))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, url, Swap("2026-06-01", new { brand = new string('x', 101), season = "Summer", isNew = true }))).StatusCode);
        Assert.Equal("[]", await c.GetStringAsync(url));
        Assert.Single(JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/tyres")).RootElement.EnumerateArray());
    }

    [Fact]
    public async Task TyreSet_RoundTripsBrandModelSize()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { name = "", brand = "Nokian", model = "WR D4", size = "205/55 R16", season = "Winter" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
        var s = await SetAsync(c, vid, id);
        Assert.Equal("Nokian WR D4", s.GetProperty("name").GetString());
        Assert.Equal("205/55 R16", s.GetProperty("size").GetString());
        var blank = await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { season = "AllSeason" });
        Assert.Equal(HttpStatusCode.Created, blank.StatusCode);
        Assert.Contains("All-season tyres", await blank.Content.ReadAsStringAsync());
        var put = await c.PutAsJsonAsync($"/api/vehicles/{vid}/tyres/{id}", new { name = "", brand = "Hankook", season = "Winter" }, ApiJson.Options);
        Assert.Contains("\"name\":\"Hankook\"", await put.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { size = new string('x', 101), season = "Winter" })).StatusCode);
    }
}
