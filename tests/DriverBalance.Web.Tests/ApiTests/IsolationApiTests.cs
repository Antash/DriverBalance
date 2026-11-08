using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class IsolationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public IsolationApiTests(ApiFactory f)
    {
        _f = f;
        _f.EnsureUser(2);
    }

    [Fact]
    public async Task Vehicle_OtherUser_404()
    {
        var u1 = _f.ClientFor(1);
        var u2 = _f.ClientFor(2);
        var vid = await Api.NewVehicleAsync(u1, "Private car");
        (await Api.PostAsync(u1, $"/api/vehicles/{vid}/expenses", new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZK" })).EnsureSuccessStatusCode();

        var own = await Api.ReadAsync<List<Api.VehicleBody>>(await u2.GetAsync("/api/vehicles"));
        Assert.DoesNotContain(own, v => v.Id == vid);

        var empty = new { };
        var statuses = new Dictionary<string, HttpStatusCode>
        {
            ["get vehicle"] = (await u2.GetAsync($"/api/vehicles/{vid}")).StatusCode,
            ["put vehicle"] = (await u2.PutAsJsonAsync($"/api/vehicles/{vid}", new { name = "x" }, ApiJson.Options)).StatusCode,
            ["get expenses"] = (await u2.GetAsync($"/api/vehicles/{vid}/expenses")).StatusCode,
            ["post expense"] = (await Api.PostAsync(u2, $"/api/vehicles/{vid}/expenses", new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZK" })).StatusCode,
            ["delete expense"] = (await u2.DeleteAsync($"/api/vehicles/{vid}/expenses/1")).StatusCode,
            ["get tyres"] = (await u2.GetAsync($"/api/vehicles/{vid}/tyres")).StatusCode,
            ["get trips"] = (await u2.GetAsync($"/api/vehicles/{vid}/trips")).StatusCode,
            ["get parameters"] = (await u2.GetAsync($"/api/vehicles/{vid}/parameters")).StatusCode,
            ["put parameters"] = (await u2.PutAsJsonAsync($"/api/vehicles/{vid}/parameters", empty, ApiJson.Options)).StatusCode,
            ["stats"] = (await Api.PostAsync(u2, $"/api/vehicles/{vid}/analytics/stats", new { periods = Array.Empty<object>() })).StatusCode,
            ["overview"] = (await Api.PostAsync(u2, $"/api/vehicles/{vid}/analytics/overview", empty)).StatusCode,
            ["chart"] = (await Api.PostAsync(u2, $"/api/vehicles/{vid}/analytics/charts/timeline", empty)).StatusCode,
            ["batches"] = (await u2.GetAsync($"/api/vehicles/{vid}/import/batches")).StatusCode,
        };
        foreach (var (name, status) in statuses)
            Assert.True(status == HttpStatusCode.NotFound, $"{name}: {(int)status}");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Category,Start\n"), "file", "x.csv");
        Assert.Equal(HttpStatusCode.NotFound, (await u2.PostAsync($"/api/vehicles/{vid}/import/csv", form)).StatusCode);

        // The owner still sees everything untouched.
        Assert.Equal(HttpStatusCode.OK, (await u1.GetAsync($"/api/vehicles/{vid}")).StatusCode);
    }

    [Fact]
    public async Task Export_ContainsOnlyOwnRows()
    {
        var u1 = _f.ClientFor(1);
        var u2 = _f.ClientFor(2);
        var v1 = await Api.NewVehicleAsync(u1, "u1-car");
        var v2 = await Api.NewVehicleAsync(u2, "u2-car");
        (await Api.PostAsync(u1, $"/api/vehicles/{v1}/expenses", new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZK", note = "u1-note" })).EnsureSuccessStatusCode();
        (await Api.PostAsync(u2, $"/api/vehicles/{v2}/expenses", new { date = "2026-05-01", category = "Fuel", amount = 5, currency = "CZK", note = "u2-note" })).EnsureSuccessStatusCode();
        await Api.ImportFixtureAsync(u2, v2);

        var r1 = await u1.GetAsync("/api/export");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal("attachment", r1.Content.Headers.ContentDisposition?.DispositionType);
        var text1 = await r1.Content.ReadAsStringAsync();
        Assert.Contains("u1-note", text1);
        Assert.DoesNotContain("u2-note", text1);
        Assert.DoesNotContain("u2-car", text1);
        var doc1 = JsonDocument.Parse(text1).RootElement;
        Assert.All(doc1.GetProperty("vehicles").EnumerateArray(), v => Assert.Equal(1, v.GetProperty("userId").GetInt64()));
        Assert.Equal(0, doc1.GetProperty("trips").GetArrayLength());

        var doc2 = JsonDocument.Parse(await (await u2.GetAsync("/api/export")).Content.ReadAsStringAsync()).RootElement;
        Assert.All(doc2.GetProperty("vehicles").EnumerateArray(), v => Assert.Equal(2, v.GetProperty("userId").GetInt64()));
        Assert.True(doc2.GetProperty("trips").GetArrayLength() > 0);
        Assert.DoesNotContain("u1-note", doc2.ToString());
    }
}
