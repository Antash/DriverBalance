using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class TripFilterApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public TripFilterApiTests(ApiFactory f)
    {
        _f = f;
        _f.EnsureUser(2);
    }

    private async Task<(HttpClient C, long Vid)> SeedAsync()
    {
        var c = _f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2025-01-01", category = "Insurance", amount = 12000, currency = "CZK", coversFrom = "2025-01-01", coversTo = "2025-12-31" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (c, vid);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{url}: {(int)r.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static List<JsonElement> Groups(JsonElement page) => page.GetProperty("groups").EnumerateArray().ToList();

    [Fact]
    public async Task List_Defaults_Unchanged()
    {
        var (c, vid) = await SeedAsync();
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?pageSize=500");
        var groups = Groups(page);
        Assert.NotEmpty(groups);
        Assert.Equal(page.GetProperty("total").GetInt32(), groups.Count);
        var starts = groups.Select(g => g.GetProperty("start").GetDateTime()).ToList();
        Assert.Equal(starts.OrderByDescending(x => x), starts);
        // Group level: some group holds several trips (cold-start grouping, not one item per trip).
        Assert.Contains(groups, g => g.GetProperty("trips").GetArrayLength() > 1);
        var summary = page.GetProperty("summary");
        Assert.Equal(page.GetProperty("total").GetInt32(), summary.GetProperty("count").GetInt32());
        Assert.Equal("CZK", summary.GetProperty("currency").GetString());
        Assert.InRange(Math.Abs(summary.GetProperty("km").GetDouble() - groups.Sum(g => g.GetProperty("distanceKm").GetDouble())), 0, 0.01);
        Assert.InRange(Math.Abs(summary.GetProperty("cost").GetDecimal() - groups.Sum(g => g.GetProperty("cost").GetProperty("total").GetDecimal())), 0m, 0.05m);
    }

    [Fact]
    public async Task List_MinKm_GroupLevel()
    {
        var (c, vid) = await SeedAsync();
        var all = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?pageSize=500");
        var filtered = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?minKm=50&pageSize=500");
        var groups = Groups(filtered);
        Assert.NotEmpty(groups);
        Assert.All(groups, g => Assert.True(g.GetProperty("distanceKm").GetDouble() >= 50));
        var s = filtered.GetProperty("summary");
        Assert.True(s.GetProperty("count").GetInt32() < all.GetProperty("summary").GetProperty("count").GetInt32());

        var paged = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?minKm=50&pageSize=5");
        Assert.True(Groups(paged).Count <= 5);
        Assert.Equal(filtered.GetProperty("total").GetInt32(), paged.GetProperty("total").GetInt32());
        var ps = paged.GetProperty("summary");
        Assert.Equal(s.GetProperty("count").GetInt32(), ps.GetProperty("count").GetInt32());
        Assert.InRange(Math.Abs(ps.GetProperty("km").GetDouble() - groups.Sum(g => g.GetProperty("distanceKm").GetDouble())), 0, 0.01);
        Assert.InRange(Math.Abs(ps.GetProperty("cost").GetDecimal() - groups.Sum(g => g.GetProperty("cost").GetProperty("total").GetDecimal())), 0m, 0.05m);
    }

    [Fact]
    public async Task List_TripLevel_FlatOneTripItems()
    {
        var (c, vid) = await SeedAsync();
        var groupPage = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?pageSize=500");
        int tripCount = Groups(groupPage).Sum(g => g.GetProperty("trips").GetArrayLength());
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?level=Trip&pageSize=500");
        var items = Groups(page);
        Assert.All(items, g => Assert.Equal(1, g.GetProperty("trips").GetArrayLength()));
        Assert.Equal(tripCount, page.GetProperty("total").GetInt32());
        Assert.Equal(tripCount, items.Count);
        var lower = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?level=trip&pageSize=500");
        Assert.Equal(tripCount, lower.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_RouteFilter()
    {
        var (c, vid) = await SeedAsync();
        var routes = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips/routes?level=Trip");
        var first = routes[0];
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?level=Trip&route={first.GetProperty("key").GetString()}&pageSize=500");
        Assert.Equal(first.GetProperty("count").GetInt32(), page.GetProperty("total").GetInt32());
        Assert.Equal(first.GetProperty("count").GetInt32(), page.GetProperty("summary").GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task List_UnknownRoute_Empty()
    {
        var (c, vid) = await SeedAsync();
        var page = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips?route=0000000000000000");
        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Equal(0, page.GetProperty("summary").GetProperty("count").GetInt32());
        Assert.Empty(Groups(page));
    }

    [Theory]
    [InlineData("minKm=-1")]
    [InlineData("minKm=abc")]
    [InlineData("minKm=NaN")]
    [InlineData("level=Journey")]
    public async Task List_BadInput_400(string query)
    {
        var (c, vid) = await SeedAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/vehicles/{vid}/trips?{query}")).StatusCode);
    }

    [Fact]
    public async Task Routes_Shape()
    {
        var (c, vid) = await SeedAsync();
        var routes = (await GetJsonAsync(c, $"/api/vehicles/{vid}/trips/routes?level=Trip")).EnumerateArray().ToList();
        Assert.NotEmpty(routes);
        foreach (var r in routes)
        {
            Assert.Matches(new Regex("^[0-9a-f]{16}$"), r.GetProperty("key").GetString()!);
            Assert.False(string.IsNullOrEmpty(r.GetProperty("from").GetString()));
            Assert.False(string.IsNullOrEmpty(r.GetProperty("to").GetString()));
            Assert.True(r.GetProperty("count").GetInt32() >= 5);
            Assert.False(string.IsNullOrEmpty(r.GetProperty("typical").GetString()));
            Assert.True(r.GetProperty("avgCost").GetDecimal() > 0);
            var share = r.GetProperty("share").GetDouble();
            Assert.InRange(share, 1e-9, 1.0);
            Assert.Equal("CZK", r.GetProperty("currency").GetString());
            Assert.Equal(JsonValueKind.Array, r.GetProperty("via").ValueKind);
        }
    }

    [Fact]
    public async Task Routes_EmptyPeriod()
    {
        var (c, vid) = await SeedAsync();
        var routes = await GetJsonAsync(c, $"/api/vehicles/{vid}/trips/routes?from=2030-01-01&to=2031-01-01");
        Assert.Equal(JsonValueKind.Array, routes.ValueKind);
        Assert.Equal(0, routes.GetArrayLength());
    }

    [Fact]
    public async Task Routes_OtherUsersVehicle_404()
    {
        var (_, vid) = await SeedAsync();
        var u2 = _f.ClientFor(2);
        Assert.Equal(HttpStatusCode.NotFound, (await u2.GetAsync($"/api/vehicles/{vid}/trips/routes")).StatusCode);
    }
}
