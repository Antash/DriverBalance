using System.Net;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class TripCostApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<long> PostExpenseAsync(HttpClient c, long vid, object body)
    {
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", body);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<long> SeedAsync(HttpClient c)
    {
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        await PostExpenseAsync(c, vid, new { date = "2025-01-01", category = "Insurance", amount = 12000, currency = "CZK", coversFrom = "2025-01-01", coversTo = "2025-12-31" });
        await PostExpenseAsync(c, vid, new { date = "2025-06-01", category = "Fuel", amount = 1500, currency = "CZK", litres = 40.0, fullTank = true });
        return vid;
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{url}: {(int)r.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    [Fact]
    public async Task TripsPage_HasCostOnGroupsAndTrips()
    {
        var c = f.CreateClient();
        var vid = await SeedAsync(c);
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?pageSize=500");

        var groups = page.GetProperty("groups").EnumerateArray().ToList();
        Assert.NotEmpty(groups);
        foreach (var g in groups)
        {
            var cost = g.GetProperty("cost");
            Assert.True(cost.GetProperty("total").GetDecimal() > 0);
            Assert.Equal("CZK", cost.GetProperty("currency").GetString());
            Assert.True(g.TryGetProperty("lPer100", out _));
            Assert.True(g.TryGetProperty("score", out _));
            var trips = g.GetProperty("trips").EnumerateArray().ToList();
            Assert.NotEmpty(trips);
            Assert.True(trips[0].TryGetProperty("startAddress", out _));
            foreach (var t in trips) Assert.Equal(JsonValueKind.Object, t.GetProperty("cost").ValueKind);
            var sum = trips.Sum(t => t.GetProperty("cost").GetProperty("total").GetDecimal());
            Assert.InRange(Math.Abs(cost.GetProperty("total").GetDecimal() - sum), 0m, 0.05m);
        }
    }

    [Fact]
    public async Task TripGet_HasCost()
    {
        var c = f.CreateClient();
        var vid = await SeedAsync(c);
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?pageSize=500");
        var first = page.GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("trips").EnumerateArray())
            .First(t => t.GetProperty("fuelLiters").GetDecimal() > 0);
        var id = first.GetProperty("id").GetInt64();

        var trip = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips/{id}");
        Assert.Equal(id, trip.GetProperty("id").GetInt64());
        var cost = trip.GetProperty("cost");
        Assert.NotEqual(JsonValueKind.Null, cost.GetProperty("pricePerLitre").ValueKind);
        var expected = Math.Round(trip.GetProperty("fuelLiters").GetDecimal() * cost.GetProperty("pricePerLitre").GetDecimal(), 2);
        Assert.InRange(Math.Abs(cost.GetProperty("fuel").GetDecimal() - expected), 0m, 0.05m);
    }

    [Fact]
    public async Task TripGet_OtherVehicle_404()
    {
        var c = f.CreateClient();
        var a = await SeedAsync(c);
        var b = await Api.NewVehicleAsync(c, "Other car");
        var page = await GetJsonAsync(c, $"/api/vehicles/{a}/trips?pageSize=500");
        var id = page.GetProperty("groups")[0].GetProperty("trips")[0].GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/vehicles/{a}/trips/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{b}/trips/{id}")).StatusCode);
    }
}
