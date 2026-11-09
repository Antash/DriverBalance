using DriverBalance.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class OwnershipApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Put(HttpClient c, long vid, object body) =>
        c.PutAsJsonAsync($"/api/vehicles/{vid}", body, ApiJson.Options);

    private static string Today => DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime).ToString("yyyy-MM-dd");
    private static string Future => DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime).AddDays(3).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Vehicle_OwnershipFields_RoundTrip()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", purchaseDate = "2025-04-14", estimatedResaleValue = 600000, resaleEstimateDate = "2026-11-01" })).StatusCode);
        var v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal("2025-04-14", v.GetProperty("purchaseDate").GetString());
        Assert.Equal(600000, v.GetProperty("estimatedResaleValue").GetDecimal());
        Assert.Equal("2026-11-01", v.GetProperty("resaleEstimateDate").GetString());

        await Put(c, vid, new { name = "Car" });
        v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal(JsonValueKind.Null, v.GetProperty("purchaseDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, v.GetProperty("estimatedResaleValue").ValueKind);
    }

    [Fact]
    public async Task Vehicle_OwnershipFields_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var bad = new Dictionary<string, object>
        {
            ["future purchase"] = new { name = "Car", purchaseDate = Future },
            ["future resale date"] = new { name = "Car", estimatedResaleValue = 1, resaleEstimateDate = Future },
            ["negative resale"] = new { name = "Car", estimatedResaleValue = -1 },
        };
        foreach (var (name, body) in bad)
        {
            var r = await Put(c, vid, body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", purchaseDate = Today })).StatusCode);
    }

    [Fact]
    public async Task Ownership_Route_ReturnsCostsAndKeepsPurchaseOutOfStats()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Put(c, vid, new { name = "Car", purchaseDate = "2025-04-14", estimatedResaleValue = 600000 });
        // Purchase costs are not accepted through the API (they come with the car), so seed them directly.
        f.SeedExpense(vid, new DateOnly(2025, 4, 14), ExpenseCategory.Purchase, 1000000m, "Car purchase price");
        f.SeedExpense(vid, new DateOnly(2025, 4, 14), ExpenseCategory.Purchase, 30000m, "Warranty");
        foreach (var (date, cat, amount, odo) in new[]
        {
            ("2025-04-14", "Parking", 500, (int?)10000), ("2025-06-01", "Parking", 700, 20000),
        })
            (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date, category = cat, amount, currency = "CZK", odometer = odo })).EnsureSuccessStatusCode();

        var o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        Assert.Equal("2025-04-14", o.GetProperty("purchaseDate").GetString());
        Assert.Equal(2, o.GetProperty("purchaseCosts").GetArrayLength());
        Assert.Equal(1030000, o.GetProperty("purchaseTotal").GetDecimal());
        Assert.Equal(1200, o.GetProperty("runningCosts").GetDecimal());
        Assert.Equal(10000, o.GetProperty("kmDriven").GetDouble());
        Assert.Equal(1030000 + 1200 - 600000, o.GetProperty("netCost").GetDecimal());

        var stats = await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/stats", new { periods = new[] { new { from = "2025-01-01", to = "2026-01-01" } } });
        var money = (await Api.ReadAsync<JsonElement>(stats))[0].GetProperty("money");
        Assert.Equal(1200, money.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Ownership_NoPurchaseDate_NullDerivedValues()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        Assert.Equal(JsonValueKind.Null, o.GetProperty("purchaseDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, o.GetProperty("netCost").ValueKind);
        Assert.Equal(0, o.GetProperty("purchaseTotal").GetDecimal());
    }
}
