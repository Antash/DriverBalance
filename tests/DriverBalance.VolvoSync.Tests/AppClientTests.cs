using System.Net;
using DriverBalance.VolvoSync;

namespace DriverBalance.VolvoSync.Tests;

public class AppClientTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"lastTrip":"2026-11-08"}""") });
        }
    }

    [Fact]
    public async Task Sends_the_token_as_bearer_when_given()
    {
        var h = new Capture();
        using var app = new AppClient("https://example.test", 1, "dbk_secret", h);
        Assert.Equal(new DateOnly(2026, 11, 8), await app.GetLastTripAsync(CancellationToken.None));
        Assert.Equal("Bearer", h.Last!.Headers.Authorization?.Scheme);
        Assert.Equal("dbk_secret", h.Last.Headers.Authorization?.Parameter);
        Assert.Equal("https://example.test/api/vehicles/1/import/last-trip", h.Last.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sends_no_authorization_without_a_token(string? token)
    {
        var h = new Capture();
        using var app = new AppClient("http://localhost:5180", 1, token, h);
        await app.GetLastTripAsync(CancellationToken.None);
        Assert.Null(h.Last!.Headers.Authorization);
    }

    [Fact]
    public void Token_is_read_from_the_environment_variable()
    {
        Assert.Equal("DRIVERBALANCE_TOKEN", AppClient.TokenVariable);
    }
}
