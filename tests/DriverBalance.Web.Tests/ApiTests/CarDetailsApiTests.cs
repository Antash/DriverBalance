using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class CarDetailsApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Put(HttpClient c, long vid, object body) =>
        c.PutAsJsonAsync($"/api/vehicles/{vid}", body, ApiJson.Options);

    [Fact]
    public async Task CarDetails_RoundTrip_TrimmedAndKeptWhenOmitted()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Put(c, vid, new
        {
            name = "Car",
            details = new
            {
                model = " Volvo V90 ", engine = "D5 AWD", trim = "Momentum", modelYear = 2024,
                exteriorColour = "Crystal White", interior = "Black leather",
                equipment = new[] { "Harman Kardon audio", " Pilot Assist ", "", "Harman Kardon audio" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = (await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"))).GetProperty("details");
        Assert.Equal("Volvo V90", d.GetProperty("model").GetString());
        Assert.Equal(2024, d.GetProperty("modelYear").GetInt32());
        Assert.Equal(["Harman Kardon audio", "Pilot Assist"], d.GetProperty("equipment").EnumerateArray().Select(x => x.GetString()!).ToArray());

        // A save without details keeps them.
        await Put(c, vid, new { name = "Car" });
        d = (await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"))).GetProperty("details");
        Assert.Equal("Momentum", d.GetProperty("trim").GetString());
    }

    [Fact]
    public async Task CarDetails_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        foreach (var details in new object[]
        {
            new { modelYear = 1800 },
            new { model = new string('x', 101) },
            new { equipment = Enumerable.Repeat("x", 61).Select((x, i) => x + i).ToArray() },
            new { equipment = new[] { new string('y', 101) } },
        })
        {
            var r = await Put(c, vid, new { name = "Car", details });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
    }
}
