using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class MaintenanceWorkApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Body(object[]? work, string[]? maintenance = null, string? description = null, string category = "Maintenance") => new
    {
        date = "2026-05-01", category, amount = 2500, currency = "CZK", work, maintenance = maintenance ?? [], maintenanceDescription = description,
    };

    private static async Task<JsonElement> ListFirstAsync(HttpClient c, long vid) =>
        JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/expenses")).RootElement[0].Clone();

    [Fact]
    public async Task Create_RoundTripsWorkItems()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var r = await Api.PostAsync(c, url, Body([
            new { type = "OilChange", part = "Aftermarket", brand = "Castrol", model = "Edge 5W-30", details = "5.2 L" },
            new { type = "OilFilter", part = "Original" },
            new { type = (string?)null, name = "Headlight polish", part = "None" },
        ]));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Contains("\"type\":\"OilChange\"", await r.Content.ReadAsStringAsync());

        var e = await ListFirstAsync(c, vid);
        var work = e.GetProperty("work");
        Assert.Equal(3, work.GetArrayLength());
        Assert.Equal("OilChange", work[0].GetProperty("type").GetString());
        Assert.Equal("Aftermarket", work[0].GetProperty("part").GetString());
        Assert.Equal("Castrol", work[0].GetProperty("brand").GetString());
        Assert.Equal("Edge 5W-30", work[0].GetProperty("model").GetString());
        Assert.Equal("5.2 L", work[0].GetProperty("details").GetString());
        Assert.Equal("OilFilter", work[1].GetProperty("type").GetString());
        Assert.Equal("Original", work[1].GetProperty("part").GetString());
        Assert.Equal(JsonValueKind.Null, work[2].GetProperty("type").ValueKind);
        Assert.Equal("Headlight polish", work[2].GetProperty("name").GetString());
        Assert.Equal(["OilChange", "OilFilter"], e.GetProperty("maintenance").EnumerateArray().Select(x => x.GetString()!).ToArray());
    }

    [Fact]
    public async Task Update_FlagsFollowWorkItems()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var created = await Api.PostAsync(c, url, Body([new { type = "OilChange", part = "None" }, new { type = "AirFilter", part = "None" }]));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();

        var put = await c.PutAsJsonAsync($"{url}/{id}", Body([new { type = "OilChange", part = "None" }], ["OilChange", "AirFilter"]), ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var e = await ListFirstAsync(c, vid);
        Assert.Equal(["OilChange"], e.GetProperty("maintenance").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal(1, e.GetProperty("work").GetArrayLength());
    }

    [Fact]
    public async Task Update_PreservesDescription()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var body = Body(null, ["BrakeFluid"], "old");
        var created = await Api.PostAsync(c, url, body);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();

        var put = await c.PutAsJsonAsync($"{url}/{id}", body, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var e = await ListFirstAsync(c, vid);
        Assert.Equal("old", e.GetProperty("maintenanceDescription").GetString());
        Assert.Equal(["BrakeFluid"], e.GetProperty("maintenance").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal(0, e.GetProperty("work").GetArrayLength());
    }

    [Fact]
    public async Task Aftermarket_FieldsClearedWhenNotAftermarket()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", Body([new { type = "BrakePads", part = "Original", brand = "X", model = "Y" }]));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var item = (await ListFirstAsync(c, vid)).GetProperty("work")[0];
        Assert.Equal(JsonValueKind.Null, item.GetProperty("brand").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("model").ValueKind);
    }

    [Fact]
    public async Task Work_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var bad = new Dictionary<string, object>
        {
            ["work on fuel"] = Body([new { type = "OilChange", part = "None" }], category: "Fuel"),
            ["type Custom"] = Body([new { type = "Custom", name = "x", part = "None" }]),
            ["unknown type"] = Body([new { type = "Foo", part = "None" }]),
            ["duplicate type"] = Body([new { type = "OilChange", part = "None" }, new { type = "OilChange", part = "None" }]),
            ["blank custom name"] = Body([new { type = (string?)null, name = "  ", part = "None" }]),
            ["long custom name"] = Body([new { type = (string?)null, name = new string('a', 101), part = "None" }]),
            ["aftermarket no brand"] = Body([new { type = "OilChange", part = "Aftermarket" }]),
            ["long details"] = Body([new { type = "OilChange", part = "None", details = new string('a', 201) }]),
            ["51 items"] = Body(Enumerable.Range(0, 51).Select(i => (object)new { type = (string?)null, name = "item" + i, part = "None" }).ToArray()),
        };
        foreach (var (name, body) in bad)
        {
            var r = await Api.PostAsync(c, url, body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
        Assert.Equal("[]", await c.GetStringAsync(url));
    }

    [Fact]
    public async Task LatestOdometer()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/odometer/latest";
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync(url)).StatusCode);

        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2026-05-01", category = "Fuel", amount = 1000, currency = "CZK", odometer = 30000 });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var got = await c.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
        var j = JsonDocument.Parse(await got.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(30000, j.GetProperty("odometer").GetInt32());
        Assert.Equal("2026-05-01", j.GetProperty("date").GetString());
    }

    [Fact]
    public async Task NullWorkElement_400NotCrash()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", Body(new object[] { null! }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Work item 1 is empty.", await r.Content.ReadAsStringAsync());
    }
}
