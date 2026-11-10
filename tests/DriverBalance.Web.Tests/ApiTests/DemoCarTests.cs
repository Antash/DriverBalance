using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriverBalance.Data;
using DriverBalance.Web.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class DemoCarTests(ITestOutputHelper output) : IDisposable
{
    private readonly ApiFactory _f = new();

    public void Dispose() => _f.Dispose();

    /// <summary>A user with no car (local mode seeds one for user 1 only).</summary>
    private HttpClient Fresh(long userId)
    {
        _f.EnsureUser(userId);
        return _f.ClientFor(userId);
    }

    private sealed record DemoBody(long Id, string Name, bool IsDemo, bool IsDefault);

    private async Task<DemoBody> CreateDemoAsync(HttpClient c)
    {
        var sw = Stopwatch.StartNew();
        var r = await c.PostAsJsonAsync("/api/vehicles/demo", new { }, ApiJson.Options);
        output.WriteLine($"POST /api/vehicles/demo took {sw.ElapsedMilliseconds} ms");
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return await Api.ReadAsync<DemoBody>(r);
    }

    private (int Trips, int Expenses, int Tyres, int Batches, int Vehicles) Counts(long vid)
    {
        using var scope = _f.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
        using var db = new DriverBalanceDbContext(options, new FixedUser(0));
        return (db.Trips.IgnoreQueryFilters().Count(t => t.VehicleId == vid), db.Expenses.IgnoreQueryFilters().Count(e => e.VehicleId == vid),
            db.TyreSets.IgnoreQueryFilters().Count(t => t.VehicleId == vid), db.ImportBatches.IgnoreQueryFilters().Count(b => b.VehicleId == vid),
            db.Vehicles.IgnoreQueryFilters().Count(v => v.Id == vid));
    }

    [Fact]
    public async Task Create_demo_returns_demo_vehicle_with_data()
    {
        var c = Fresh(10);
        var demo = await CreateDemoAsync(c);
        Assert.True(demo.IsDemo);
        Assert.True(demo.IsDefault);
        Assert.StartsWith("Demo", demo.Name);

        var trips = await c.GetFromJsonAsync<JsonElement>($"/api/vehicles/{demo.Id}/trips", ApiJson.Options);
        Assert.True(trips.GetProperty("total").GetInt32() > 0);
        var expenses = await c.GetFromJsonAsync<JsonElement>($"/api/vehicles/{demo.Id}/expenses", ApiJson.Options);
        Assert.True(expenses.GetArrayLength() > 0);
        var (t, e, ty, b, _) = Counts(demo.Id);
        Assert.True(t > 0 && e > 0 && ty > 0 && b == 1);
    }

    [Fact]
    public async Task Demo_refused_when_user_has_a_car()
    {
        var c = Fresh(10);
        await CreateDemoAsync(c);
        var second = await c.PostAsJsonAsync("/api/vehicles/demo", new { }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("You already have a car.", await second.Content.ReadAsStringAsync());

        var c2 = Fresh(11);
        await Api.NewVehicleAsync(c2);
        Assert.Equal(HttpStatusCode.BadRequest, (await c2.PostAsJsonAsync("/api/vehicles/demo", new { }, ApiJson.Options)).StatusCode);
    }

    [Fact]
    public async Task Analytics_work_on_the_demo_car()
    {
        var c = Fresh(10);
        var id = (await CreateDemoAsync(c)).Id;
        var empty = new { };
        var gets = new[] { "ownership", "trips", "expenses", "expenses/places", "tyres", "tyres/summary", "parameters", "odometer/latest" };
        foreach (var g in gets)
        {
            var r = await c.GetAsync($"/api/vehicles/{id}/{g}");
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"GET {g}: {(int)r.StatusCode}");
        }
        var posts = new List<(string Url, object Body)>
        {
            ("overview", empty),
            ("stats", new { periods = new[] { new { from = "2023-01-01", to = "2026-11-09", label = "all" } } }),
            ("calendar", new { level = "months" }),
            ("intervals", new { type = "OilChange" }),
        };
        foreach (var n in new[] { "monthly-score", "heatmap", "fuel-trend", "score-map", "gap-histogram", "when-you-drive", "penalty-curve",
                     "seasonality", "places", "routes", "dpf-health", "money-monthly", "money-totals", "money-cumulative", "timeline" })
            posts.Add(($"charts/{n}", empty));
        foreach (var (url, body) in posts)
        {
            var r = await Api.PostAsync(c, $"/api/vehicles/{id}/analytics/{url}", body);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"POST {url}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }
        var own = await c.GetFromJsonAsync<JsonElement>($"/api/vehicles/{id}/ownership", ApiJson.Options);
        Assert.NotEqual(JsonValueKind.Null, own.ValueKind);
    }

    [Fact]
    public async Task Delete_demo_removes_everything()
    {
        var c = Fresh(10);
        var id = (await CreateDemoAsync(c)).Id;
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        Assert.Equal((0, 0, 0, 0, 0), Counts(id));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_real_car_is_refused()
    {
        var c = Fresh(10);
        var id = await Api.NewVehicleAsync(c);
        var r = await c.DeleteAsync($"/api/vehicles/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Only the demo car can be deleted.", await r.Content.ReadAsStringAsync());
        Assert.Equal(1, Counts(id).Vehicles);
    }

    [Fact]
    public async Task Other_user_cannot_delete_demo()
    {
        var c = Fresh(10);
        var id = (await CreateDemoAsync(c)).Id;
        var other = Fresh(11);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync("/api/vehicles/99999")).StatusCode);
        Assert.Equal(1, Counts(id).Vehicles);
    }

    [Fact]
    public async Task Adding_a_real_car_removes_the_demo()
    {
        var c = Fresh(10);
        var demo = await CreateDemoAsync(c);
        var realId = await Api.NewVehicleAsync(c, "My car");
        var list = await Api.ReadAsync<List<DemoBody>>(await c.GetAsync("/api/vehicles"));
        var only = Assert.Single(list);
        Assert.Equal(realId, only.Id);
        Assert.True(only.IsDefault);
        Assert.False(only.IsDemo);
        Assert.Equal((0, 0, 0, 0, 0), Counts(demo.Id));
    }

    [Fact]
    public async Task Deleting_default_demo_makes_remaining_car_default()
    {
        var c = Fresh(10);
        var demo = await CreateDemoAsync(c);
        long realId = 0;
        using (var scope = _f.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
            using var db = new DriverBalanceDbContext(options, new FixedUser(10));
            var row = new VehicleRow { UserId = 10, Name = "Real", FuelType = "", IsDefault = false };
            db.Vehicles.Add(row);
            db.SaveChanges();
            realId = row.Id;
        }
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/vehicles/{demo.Id}")).StatusCode);
        var list = await Api.ReadAsync<List<DemoBody>>(await c.GetAsync("/api/vehicles"));
        var only = Assert.Single(list);
        Assert.Equal(realId, only.Id);
        Assert.True(only.IsDefault);
    }
}
