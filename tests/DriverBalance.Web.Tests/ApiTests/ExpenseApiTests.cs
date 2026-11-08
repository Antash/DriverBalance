using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class ExpenseApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Valid(object? extra = null) => new { date = "2026-05-01", category = "Fuel", amount = 1000, currency = "CZK", litres = 40.5, fullTank = true };

    [Fact]
    public async Task Expense_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";

        var bad = new Dictionary<string, object>
        {
            ["zero amount"] = new { date = "2026-05-01", category = "Fuel", amount = 0, currency = "CZK" },
            ["negative amount"] = new { date = "2026-05-01", category = "Fuel", amount = -5, currency = "CZK" },
            ["short currency"] = new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZ" },
            ["digit currency"] = new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "C2K" },
            ["zero litres"] = new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZK", litres = 0 },
            ["covers reversed"] = new { date = "2026-05-01", category = "Insurance", amount = 5, currency = "CZK", coversFrom = "2026-06-01", coversTo = "2026-05-01" },
            ["unknown tyre set"] = new { date = "2026-05-01", category = "Tyres", amount = 5, currency = "CZK", tyreSetId = 99999 },
        };
        foreach (var (name, body) in bad)
        {
            var r = await Api.PostAsync(c, url, body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }

        Assert.Equal("[]", await (await c.GetAsync(url)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Expense_Crud_RoundTripsMaintenanceFlagsAsStringArray()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";

        var created = await Api.PostAsync(c, url, new
        {
            date = "2026-05-01", category = "Maintenance", amount = 2500, currency = "czk",
            maintenance = new[] { "OilChange", "BrakeFluid" }, maintenanceDescription = "Service",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadAsStringAsync();
        Assert.Contains("\"maintenance\":[\"OilChange\",\"BrakeFluid\"]", json);
        Assert.Contains("\"currency\":\"CZK\"", json);
        var id = JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt64();

        var put = await c.PutAsJsonAsync($"{url}/{id}", new { date = "2026-05-02", category = "Maintenance", amount = 2600, currency = "CZK", maintenance = Array.Empty<string>() }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Contains("\"maintenance\":[]", await put.Content.ReadAsStringAsync());

        var list = await c.GetAsync($"{url}?from=2026-05-02&to=2026-05-03&category=maintenance");
        Assert.Single(JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.EnumerateArray());
        var empty = await c.GetAsync($"{url}?from=2026-05-03");
        Assert.Empty(JsonDocument.Parse(await empty.Content.ReadAsStringAsync()).RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"{url}/{id}", Valid(), ApiJson.Options)).StatusCode);
    }

    [Fact]
    public async Task Expense_TyreSetOfAnotherVehicle_400()
    {
        var c = f.CreateClient();
        var v1 = await Api.NewVehicleAsync(c);
        var v2 = await Api.NewVehicleAsync(c);
        var set = await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{v1}/tyres", new { name = "Winter", season = "Winter" }));
        var r = await Api.PostAsync(c, $"/api/vehicles/{v2}/expenses",
            new { date = "2026-05-01", category = "Tyres", amount = 5, currency = "CZK", tyreSetId = set.GetProperty("id").GetInt64() });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Tyres_DeleteWhileReferenced_409_ThenAllowedAfterExpenseRemoved()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var created = await Api.PostAsync(c, $"/api/vehicles/{vid}/tyres", new { name = "Winter", season = "Winter" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var setId = (await Api.ReadAsync<JsonElement>(created)).GetProperty("id").GetInt64();
        var exp = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2026-05-01", category = "Tyres", amount = 8000, currency = "CZK", tyreSetId = setId });
        var expId = (await Api.ReadAsync<JsonElement>(exp)).GetProperty("id").GetInt64();

        var blocked = await c.DeleteAsync($"/api/vehicles/{vid}/tyres/{setId}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var errors = (await Api.ReadAsync<Api.ErrorsBody>(blocked)).Errors;
        Assert.Equal(["Tyre set is used by 1 expenses; retire it instead"], errors);

        var retire = await c.PutAsJsonAsync($"/api/vehicles/{vid}/tyres/{setId}", new { name = "Winter", season = "Winter", retired = true }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        var summary = await c.GetAsync($"/api/vehicles/{vid}/tyres/summary");
        var s = JsonDocument.Parse(await summary.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, s.GetProperty("summaries").GetArrayLength());
        Assert.True(s.TryGetProperty("unassignedSpans", out _));

        await c.DeleteAsync($"/api/vehicles/{vid}/expenses/{expId}");
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/vehicles/{vid}/tyres/{setId}")).StatusCode);
    }

    [Fact]
    public async Task Vehicle_CreateUpdateAndDefault()
    {
        var c = f.CreateClient();
        var seeded = await Api.ReadAsync<List<Api.VehicleBody>>(await c.GetAsync("/api/vehicles"));
        Assert.Contains(seeded, v => v.Name == "My Volvo" && v.IsDefault);

        var vid = await Api.NewVehicleAsync(c, "Second");
        var put = await c.PutAsJsonAsync($"/api/vehicles/{vid}",
            new { name = "Renamed", homeCurrency = "EUR", timeZone = "Europe/Berlin", isDefault = true }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await Api.ReadAsync<Api.VehicleBody>(put);
        Assert.Equal(("Renamed", "EUR", "Europe/Berlin", true), (updated.Name, updated.HomeCurrency, updated.TimeZone, updated.IsDefault));

        var all = await Api.ReadAsync<List<Api.VehicleBody>>(await c.GetAsync("/api/vehicles"));
        Assert.Single(all, v => v.IsDefault);

        var bad = await c.PutAsJsonAsync($"/api/vehicles/{vid}", new { name = "", homeCurrency = "EURO", timeZone = "Nowhere/Land" }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await Api.ReadAsync<Api.ErrorsBody>(bad)).Errors.Length >= 3);
    }

    [Fact]
    public async Task Parameters_PutInvalid400_PutValidPersists_Defaults()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var bad = await c.PutAsJsonAsync($"/api/vehicles/{vid}/parameters", new { coldStartGapMinutes = 0 }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(bad)).Errors);

        var ok = await c.PutAsJsonAsync($"/api/vehicles/{vid}/parameters", new { coldStartGapMinutes = 30 }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var got = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/parameters"));
        Assert.Equal(30, got.GetProperty("coldStartGapMinutes").GetInt32());
        var defaults = await Api.ReadAsync<JsonElement>(await c.GetAsync("/api/parameters/defaults"));
        Assert.Equal(45, defaults.GetProperty("coldStartGapMinutes").GetInt32());
    }

    [Fact]
    public async Task ImportLegacy_AcceptsBothFiles_AndIsIdempotent()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);

        async Task<JsonElement> Run()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(File.OpenRead(Data.TestDb.Fixture("synthetic-triprecords.json"))), "trips", "triprecords.json");
            form.Add(new StreamContent(File.OpenRead(Data.TestDb.Fixture("synthetic-maintenance.json"))), "maintenance", "maintenance.json");
            var r = await c.PostAsync($"/api/vehicles/{vid}/import/legacy", form);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return await Api.ReadAsync<JsonElement>(r);
        }

        var first = await Run();
        Assert.True(first.GetProperty("tripsImported").GetInt32() > 0);
        Assert.True(first.GetProperty("expensesImported").GetInt32() > 0);
        var second = await Run();
        Assert.Equal(0, second.GetProperty("tripsImported").GetInt32());
        Assert.Equal(0, second.GetProperty("expensesImported").GetInt32());
    }

    [Fact]
    public async Task Trips_ListGroupsNewestFirst_AndGetById()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var import = await Api.ImportFixtureAsync(c, vid);

        var page = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/trips?page=1&pageSize=3"));
        var groups = page.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(3, groups.Count);
        Assert.True(page.GetProperty("total").GetInt32() > 3);
        Assert.True(string.CompareOrdinal(groups[0].GetProperty("start").GetString(), groups[1].GetProperty("start").GetString()) > 0);

        var tripId = groups[0].GetProperty("trips")[0].GetProperty("id").GetInt64();
        var one = await c.GetAsync($"/api/vehicles/{vid}/trips/{tripId}");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{vid}/trips/0")).StatusCode);

        var batches = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/import/batches"));
        Assert.Equal(import.Imported, batches[0].GetProperty("imported").GetInt32());
    }
}
