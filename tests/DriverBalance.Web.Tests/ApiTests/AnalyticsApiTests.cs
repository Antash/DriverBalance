using System.Net;
using System.Text.Json;
using DriverBalance.Core;
using DriverBalance.Web.Tests.Data;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class AnalyticsApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly string[] ChartNames =
    [
        "monthly-score", "heatmap", "fuel-trend", "score-map", "gap-histogram", "when-you-drive", "penalty-curve",
        "seasonality", "places", "routes", "dpf-health", "money-monthly", "money-totals", "money-cumulative", "timeline",
    ];

    private static readonly Period AllTime = new(new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1), "all");
    private static readonly object StatsAll = new { periods = new[] { new { from = "2025-01-01", to = "2027-01-01", label = "all" } } };

    private static async Task<JsonElement> JsonOf(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{r.RequestMessage?.RequestUri}: {(int)r.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    [Fact]
    public async Task AllAnalyticsEndpoints_EmptyVehicle_Return200()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var a = $"/api/vehicles/{vid}/analytics";

        var calls = new List<(string Url, object Body)>
        {
            ($"{a}/stats", new { periods = new[] { new { from = "2026-01-01", to = "2026-02-01", label = "Jan" } } }),
            ($"{a}/calendar", new { level = "years" }),
            ($"{a}/calendar", new { level = "months", year = 2026 }),
            ($"{a}/calendar", new { level = "weeks", year = 2026, month = 11 }),
            ($"{a}/intervals", new { type = new[] { "OilChange" } }),
            ($"{a}/overview", new { }),
        };
        calls.AddRange(ChartNames.Select(n => ($"{a}/charts/{n}", (object)new { })));

        foreach (var (url, body) in calls)
        {
            var r = await Api.PostAsync(c, url, body);
            var text = await r.Content.ReadAsStringAsync();
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url}: {(int)r.StatusCode} {text}");
            JsonDocument.Parse(text).Dispose();
        }
    }

    [Fact]
    public async Task UnknownChart_Returns404()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/charts/nope", new { });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Stats_InvalidParameters_Returns400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats",
            new { periods = new[] { new { from = "2026-01-01", to = "2026-02-01", label = "Jan" } }, parameters = new { shortDurationMinutes = 90 } });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var body = await Api.ReadAsync<Api.ErrorsBody>(r);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task Stats_ParameterOverride_ChangesResultButNotSaved()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        var url = $"/api/vehicles/{vid}/analytics/stats";
        var periods = new[] { new { from = "2025-01-01", to = "2027-01-01", label = "all" } };

        int ColdStarts(JsonElement e) => e[0].GetProperty("usage").GetProperty("coldStarts").GetInt32();
        var baseline = ColdStarts(await JsonOf(await Api.PostAsync(c, url, new { periods })));
        var overridden = ColdStarts(await JsonOf(await Api.PostAsync(c, url, new { periods, parameters = new { coldStartGapMinutes = 1 } })));
        Assert.True(overridden > baseline, $"expected more cold starts with a 1 min gap ({overridden} vs {baseline})");

        var saved = await JsonOf(await c.GetAsync($"/api/vehicles/{vid}/parameters"));
        Assert.Equal(45, saved.GetProperty("coldStartGapMinutes").GetInt32());
        Assert.Equal(baseline, ColdStarts(await JsonOf(await Api.PostAsync(c, url, new { periods }))));
    }

    [Fact]
    public async Task ImportCsv_ThenStats_MatchesCore()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var import = await Api.ImportFixtureAsync(c, vid);

        using var reader = File.OpenText(TestDb.Fixture("synthetic-trips.csv"));
        var parsed = VolvoCsvParser.Parse(reader);
        Assert.Equal(parsed.Trips.Count, import.Imported);
        var expected = AnalysisContext.Create(
            new VehicleData(parsed.Trips, [], [], "CZK", new DateOnly(2026, 11, 8)), new AnalysisParameters()).Stats(AllTime);

        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats", StatsAll);
        var actual = (await Api.ReadAsync<PeriodStats[]>(r)).Single();

        Assert.Equal(expected.Usage.TripCount, actual.Usage.TripCount);
        Assert.Equal(expected.Usage.TripKm, actual.Usage.TripKm, 6);
        Assert.Equal(expected.Usage.ColdStarts, actual.Usage.ColdStarts);
        Assert.Equal(expected.Usage.ShortGroups, actual.Usage.ShortGroups);
        Assert.Equal(expected.Fuel.TripLiters, actual.Fuel.TripLiters, 6);
        Assert.Equal(expected.Scores.DpfIndex!.Value, actual.Scores.DpfIndex!.Value, 9);
        foreach (var score in Enum.GetValues<DrivingScore>())
            Assert.Equal(expected.Scores.Buckets[score].Groups, actual.Scores.Buckets[score].Groups);
        Assert.Equal(expected.Mix.CityKm, actual.Mix.CityKm, 6);
    }

    [Fact]
    public async Task ScoreMap_ReturnsZonesFromEffectiveParameters()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        var json = await JsonOf(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/charts/score-map",
            new { from = "2025-01-01", to = "2027-01-01", parameters = new { longDurationMinutes = 41 } }));
        Assert.Equal(41, json.GetProperty("zones").GetProperty("longMin").GetDouble());
        Assert.True(json.GetProperty("points").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ScoreMap_HonoursLevel()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        async Task<int> Points(object? options) => (await JsonOf(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/charts/score-map",
            new { from = "2025-01-01", to = "2027-01-01", options }))).GetProperty("points").GetArrayLength();
        int groups = await Points(null);
        int trips = await Points(new { level = "Trip" });
        Assert.True(trips > groups, $"trip level {trips} should exceed group level {groups}");
    }

    [Fact]
    public async Task Timeline_IncludesImportEvents()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var import = await Api.ImportFixtureAsync(c, vid);
        var json = await JsonOf(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/charts/timeline",
            new { from = "2000-01-01", to = "2100-01-01" }));
        var imports = json.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "Import").ToList();
        var ev = Assert.Single(imports);
        Assert.Equal($"Imported {import.Imported} trips", ev.GetProperty("label").GetString());
        Assert.Equal(import.BatchId, ev.GetProperty("refId").GetInt64());
    }

    [Fact]
    public async Task Overview_ReturnsStatsRecentExpensesAndReminders()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid); // trips are from 2025, "today" is pinned to 2026-11-08
        for (int i = 1; i <= 7; i++)
            (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = $"2026-11-0{i}", category = "Parking", amount = 50, currency = "CZK" })).EnsureSuccessStatusCode();
        (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2026-10-02", category = "Other", amount = 10, currency = "EUR" })).EnsureSuccessStatusCode();
        (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2026-10-03", category = "Maintenance", amount = 10, currency = "CZK", maintenance = new[] { "TyreSwap" } })).EnsureSuccessStatusCode();

        var json = await JsonOf(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/overview", new { }));
        Assert.Equal("2026-11", json.GetProperty("thisMonth").GetProperty("period").GetProperty("label").GetString());
        Assert.Equal("2026", json.GetProperty("thisYear").GetProperty("period").GetProperty("label").GetString());
        Assert.Equal(5, json.GetProperty("recentExpenses").GetArrayLength());
        Assert.Equal("2026-11-07", json.GetProperty("recentExpenses")[0].GetProperty("date").GetString());
        var reminders = json.GetProperty("reminders").EnumerateArray().ToDictionary(r => r.GetProperty("kind").GetString()!);
        Assert.Equal("alarm", reminders["export"].GetProperty("severity").GetString());
        Assert.Contains("needs-conversion", reminders.Keys);
        Assert.Contains("unassigned-tyres", reminders.Keys);
        Assert.False(string.IsNullOrWhiteSpace(reminders["export"].GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Calendar_Weeks_ReturnsPeriodAndStatsPairs()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var json = await JsonOf(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/calendar", new { level = "weeks", year = 2026, month = 11 }));
        Assert.Equal(6, json.GetArrayLength());
        Assert.Equal("2026-11-01", json[0].GetProperty("period").GetProperty("from").GetString());
        Assert.True(json[0].TryGetProperty("stats", out _));
    }

    [Fact]
    public async Task SpaFallback_ServesIndexExceptForApiAndMcp()
    {
        var c = f.CreateClient();
        var page = await c.GetAsync("/settings/tokens");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("spa-index", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/mcp/nope")).StatusCode);
    }
}
