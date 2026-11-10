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
                exteriorColour = "Crystal White", interior = "Charcoal cloth",
                equipment = new[] { "Harman Kardon audio", " Pilot Assist ", "", "Harman Kardon audio" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = (await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"))).GetProperty("details");
        Assert.Equal("Volvo V90", d.GetProperty("model").GetString());
        Assert.Equal(2024, d.GetProperty("modelYear").GetInt32());
        Assert.Equal(["Harman Kardon audio", "Pilot Assist"], d.GetProperty("equipment").EnumerateArray().Select(x => x.GetString()!).ToArray());

        Assert.Equal(JsonValueKind.Null, d.GetProperty("transmission").ValueKind);
        Assert.False(d.GetProperty("awd").GetBoolean());

        await Put(c, vid, new { name = "Car", details = new { model = "Volvo V90", transmission = "Automatic", awd = true } });
        d = (await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"))).GetProperty("details");
        Assert.Equal("Automatic", d.GetProperty("transmission").GetString());
        Assert.True(d.GetProperty("awd").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, vid, new { name = "Car", details = new { transmission = "Sideways" } })).StatusCode);

        // A save without details keeps them.
        await Put(c, vid, new { name = "Car" });
        d = (await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"))).GetProperty("details");
        Assert.Equal("Automatic", d.GetProperty("transmission").GetString());
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

public sealed class FuelTypeApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Put(HttpClient c, long vid, object body) =>
        c.PutAsJsonAsync($"/api/vehicles/{vid}", body, ApiJson.Options);

    private static async Task<JsonElement> GetVehicle(HttpClient c, long vid) =>
        await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));

    [Theory]
    [InlineData("Diesel", true)]
    [InlineData("Petrol", false)]
    [InlineData("DieselMhev", true)]
    [InlineData("PetrolMhev", false)]
    [InlineData("PetrolPhev", false)]
    [InlineData("Electric", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public async Task FuelType_RoundTrip_AndHasDpf(string? fuel, bool hasDpf)
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", fuelType = fuel })).StatusCode);
        var v = await GetVehicle(c, vid);
        Assert.Equal(string.IsNullOrEmpty(fuel) ? JsonValueKind.Null : JsonValueKind.String, v.GetProperty("fuelType").ValueKind);
        if (!string.IsNullOrEmpty(fuel)) Assert.Equal(fuel, v.GetProperty("fuelType").GetString());
        Assert.Equal(hasDpf, v.GetProperty("hasDpf").GetBoolean());
    }

    [Fact]
    public async Task FuelType_Unknown_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, vid, new { name = "Car", fuelType = "Steam" })).StatusCode);
    }

    [Fact]
    public async Task Overview_DpfHealthNull_WhenPetrol_AndPresentWhenDiesel()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        await Put(c, vid, new { name = "Car", fuelType = "Petrol" });
        var o = await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/overview", new { }));
        Assert.Equal(JsonValueKind.Null, o.GetProperty("dpfHealth").ValueKind);
        await Put(c, vid, new { name = "Car", fuelType = "Diesel" });
        o = await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{vid}/analytics/overview", new { }));
        Assert.Equal(JsonValueKind.Object, o.GetProperty("dpfHealth").ValueKind);
    }
}
