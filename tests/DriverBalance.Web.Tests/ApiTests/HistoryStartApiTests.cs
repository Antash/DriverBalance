using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class HistoryStartApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Put(HttpClient c, long vid, object body) =>
        c.PutAsJsonAsync($"/api/vehicles/{vid}", body, ApiJson.Options);

    [Fact]
    public async Task Vehicle_HistoryStart_RoundTripsDefaultsAndClears()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);

        var r = await Put(c, vid, new { name = "Car", historyStart = "2023-05-01", historyStartOdometer = 120 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal("2023-05-01", v.GetProperty("historyStart").GetString());
        Assert.Equal(120, v.GetProperty("historyStartOdometer").GetInt32());

        // date without odometer defaults to 0 (new car)
        await Put(c, vid, new { name = "Car", historyStart = "2023-05-01" });
        v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal(0, v.GetProperty("historyStartOdometer").GetInt32());

        // both null clears
        await Put(c, vid, new { name = "Car" });
        v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal(JsonValueKind.Null, v.GetProperty("historyStart").ValueKind);
        Assert.Equal(JsonValueKind.Null, v.GetProperty("historyStartOdometer").ValueKind);
    }

    [Fact]
    public async Task Vehicle_HistoryStart_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var bad = new Dictionary<string, object>
        {
            ["future date"] = new { name = "Car", historyStart = DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime).AddDays(1).ToString("yyyy-MM-dd") },
            ["negative odometer"] = new { name = "Car", historyStart = "2023-05-01", historyStartOdometer = -1 },
        };
        foreach (var (name, body) in bad)
        {
            var r = await Put(c, vid, body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", historyStart = DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime).ToString("yyyy-MM-dd") })).StatusCode);
    }

    [Fact]
    public async Task Intervals_IncludeOldestPeriodFromHistoryStart()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Put(c, vid, new { name = "Car", historyStart = "2023-05-01", historyStartOdometer = 0 });
        (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new
        {
            date = "2025-02-06", category = "Maintenance", amount = 3000, currency = "CZK",
            maintenance = new[] { "OilChange" }, odometer = 7350,
        })).EnsureSuccessStatusCode();

        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/intervals", new { type = new[] { "OilChange" } });
        var list = await Api.ReadAsync<JsonElement>(r);
        Assert.Equal(2, list.GetArrayLength());
        var oldest = list[1];
        Assert.Equal("2023-05-01", oldest.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal(0, oldest.GetProperty("period").GetProperty("startOdometer").GetInt32());
        var usage = oldest.GetProperty("stats").GetProperty("usage");
        Assert.Equal(7350, usage.GetProperty("odometerKm").GetDouble());
        Assert.Equal(0, usage.GetProperty("tripCount").GetInt32());
    }
}
