using System.Net;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class WorkPositionApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Body(object[] work) => new { date = "2026-05-01", category = "Maintenance", amount = 900, currency = "CZK", work, maintenance = Array.Empty<string>() };

    [Fact]
    public async Task PositionAndRubberOnly_RoundTrip()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        var r = await Api.PostAsync(c, url, Body([
            new { type = "BrakePads", part = "Original", position = "Front" },
            new { type = "Suspension", part = "None", position = "Both" },
            new { type = "WiperBladesFront", part = "Aftermarket", brand = "Bosch", model = "AP600U", rubberOnly = true },
            new { type = "WiperBladesRear", part = "Aftermarket", brand = "Bosch", model = "AP600U", rubberOnly = true },
            new { type = "OilChange", part = "None" },
        ]));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var work = JsonDocument.Parse(await c.GetStringAsync(url)).RootElement[0].GetProperty("work");
        Assert.Equal("Front", work[0].GetProperty("position").GetString());
        Assert.Equal("Both", work[1].GetProperty("position").GetString());
        Assert.True(work[2].GetProperty("rubberOnly").GetBoolean());
        Assert.True(work[3].GetProperty("rubberOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, work[4].GetProperty("position").ValueKind);
        Assert.False(work[4].GetProperty("rubberOnly").GetBoolean());
    }

    [Fact]
    public async Task PositionAndRubberOnly_OnlyWhereTheyApply()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var url = $"/api/vehicles/{vid}/expenses";
        foreach (var item in new object[]
        {
            new { type = "OilChange", part = "None", position = "Front" },
            new { type = "BrakePads", part = "None", rubberOnly = true },
            new { type = (string?)null, name = "Polish", part = "None", position = "Rear" },
        })
        {
            var r = await Api.PostAsync(c, url, Body([item]));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
    }
}
