using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class PlaceApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Body(string? place, string category = "Maintenance", string date = "2026-05-01") => new
    {
        date, category, amount = 2500, currency = "CZK", place,
    };

    private static async Task<JsonElement> ListFirstAsync(HttpClient c, long vid) =>
        JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/expenses")).RootElement[0].Clone();

    [Fact]
    public async Task Create_RoundTripsPlace_AndUpdateChangesIt()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var r = await Api.PostAsync(c, url, Body("City Garage"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var e = await ListFirstAsync(c, vid);
        Assert.Equal("City Garage", e.GetProperty("place").GetString());

        var put = await c.PutAsJsonAsync($"{url}/{e.GetProperty("id").GetInt64()}", Body("Pneu Servis"), ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Pneu Servis", (await ListFirstAsync(c, vid)).GetProperty("place").GetString());
    }

    [Fact]
    public async Task Place_IsTrimmed_AndBlankBecomesNull()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        await Api.PostAsync(c, url, Body("  City Garage  "));
        Assert.Equal("City Garage", (await ListFirstAsync(c, vid)).GetProperty("place").GetString());
        await Api.PostAsync(c, url, Body("   ", date: "2026-06-01"));
        Assert.Equal(JsonValueKind.Null, (await ListFirstAsync(c, vid)).GetProperty("place").ValueKind);
    }

    [Fact]
    public async Task Place_Over100Chars_Is400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", Body(new string('x', 101)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("errors", await r.Content.ReadAsStringAsync());
        Assert.Equal((HttpStatusCode)201, (await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", Body(new string('x', 100)))).StatusCode);
    }

    [Fact]
    public async Task Fuel_KeepsPlaceNull()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", Body("Shell", "Fuel"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ListFirstAsync(c, vid)).GetProperty("place").ValueKind);
    }

    [Fact]
    public async Task Places_AreRecentFirst_CaseInsensitiveDistinct_KeepingLatestSpelling()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        await Api.PostAsync(c, url, Body("City Garage", date: "2025-01-10"));
        await Api.PostAsync(c, url, Body("Pneu Servis", date: "2025-06-01"));
        await Api.PostAsync(c, url, Body("city garage", date: "2026-01-10"));
        await Api.PostAsync(c, url, Body(null, date: "2026-02-01"));
        var places = JsonSerializer.Deserialize<string[]>(await c.GetStringAsync($"{url}/places"));
        Assert.Equal(["city garage", "Pneu Servis"], places!);
    }
}
