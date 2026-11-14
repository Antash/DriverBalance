using System.Net;
using System.Text.Json.Nodes;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class TripCostMcpTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Client, string Token)> SeedAsync(long userId)
    {
        f.EnsureUser(userId);
        var c = f.ClientFor(userId);
        var vid = await Api.NewVehicleAsync(c);
        await Api.ImportFixtureAsync(c, vid); // trips in 2025
        foreach (var body in new object[]
        {
            new { date = "2025-01-01", category = "Insurance", amount = 12000, currency = "CZK", coversFrom = "2025-01-01", coversTo = "2025-12-31" },
            new { date = "2025-06-01", category = "Fuel", amount = 1500, currency = "CZK", litres = 40.0, fullTank = true },
        })
            Assert.Equal(HttpStatusCode.Created, (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", body)).StatusCode);
        var token = await McpHelpers.CreateTokenAsync(c);
        return (c, token.Token);
    }

    [Fact]
    public async Task ListRecentTrips_IsListed()
    {
        var (_, token) = await SeedAsync(41);
        await using var mcp = await McpHelpers.ConnectAsync(f, token);
        Assert.Contains("list_recent_trips", (await mcp.ListToolsAsync()).Select(t => t.Name));
    }

    [Fact]
    public async Task ListRecentTrips_Default_IsLastTripWithCostAndSummary()
    {
        var (c, token) = await SeedAsync(42);
        await using var mcp = await McpHelpers.ConnectAsync(f, token);
        var r = await McpHelpers.CallAsync(mcp, "list_recent_trips");
        Assert.NotEqual(true, r.IsError);
        var json = McpHelpers.JsonOf(r);

        var trips = json["trips"]!.AsArray();
        Assert.Single(trips);
        var t = trips[0]!;
        Assert.Equal("2025-12-20T14:43:00", (string)t["start"]!); // the fixture's last trip
        var cost = t["cost"]!;
        Assert.Equal("CZK", (string)cost["currency"]!);
        Assert.True((decimal)cost["total"]! > 0);
        Assert.Equal(1, (int)json["count"]!);
        Assert.Equal("trip", (string)json["level"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)json["costModel"]));

        var summary = (string)json["summary"]!;
        Assert.Contains("20/12/2025 14:43", summary);
        Assert.Contains("CZK", summary);
    }

    [Fact]
    public async Task ListRecentTrips_GroupsNewestFirst_WithTotal()
    {
        var (_, token) = await SeedAsync(43);
        await using var mcp = await McpHelpers.ConnectAsync(f, token);
        var json = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "list_recent_trips", new { count = 5, level = "group" }));

        var items = json["trips"]!.AsArray().Select(n => n!).ToList();
        Assert.Equal(5, items.Count);
        var starts = items.Select(n => (string)n["start"]!).ToList();
        Assert.Equal(starts.OrderByDescending(s => s, StringComparer.Ordinal).ToList(), starts);
        Assert.All(items, n => Assert.True((int)n["tripCount"]! >= 1));
        var sum = items.Sum(n => (decimal)n["cost"]!["total"]!);
        Assert.Equal(sum, (decimal)json["totalCost"]!, 2);
    }

    [Fact]
    public async Task ListRecentTrips_DateRangeIsInclusive()
    {
        var (_, token) = await SeedAsync(44);
        await using var mcp = await McpHelpers.ConnectAsync(f, token);
        var json = McpHelpers.JsonOf(await McpHelpers.CallAsync(mcp, "list_recent_trips",
            new { from = "2025-12-20", to = "2025-12-20", count = 50 }));
        var starts = json["trips"]!.AsArray().Select(n => (string)n!["start"]!).ToList();
        Assert.Equal(2, starts.Count);
        Assert.All(starts, s => Assert.StartsWith("2025-12-20", s));
    }

    [Theory]
    [InlineData("level", "journey")]
    [InlineData("from", "20/12/2025")]
    public async Task ListRecentTrips_BadInput_IsError(string name, string value)
    {
        var (_, token) = await SeedAsync(45);
        await using var mcp = await McpHelpers.ConnectAsync(f, token);
        var r = await McpHelpers.CallAsync(mcp, "list_recent_trips", new Dictionary<string, object> { [name] = value });
        Assert.True(r.IsError);
    }
}
