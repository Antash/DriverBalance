using System.Net;
using DriverBalance.Web.Tests.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DriverBalance.Web.Tests.ApiTests;

/// <summary>Keeps the real LocalUser scheme; a test middleware fakes the remote address from X-Fake-Remote.</summary>
public sealed class LocalAuthFactory : ApiFactory
{
    protected override void ConfigureAuth(IServiceCollection s) =>
        s.AddTransient<IStartupFilter, FakeRemoteFilter>();

    private sealed class FakeRemoteFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, n) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Fake-Remote", out var ip))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                await n();
            });
            next(app);
        };
    }
}

public sealed class SecurityApiTests(LocalAuthFactory f) : IClassFixture<LocalAuthFactory>
{
    private HttpClient Client(string? remote = "127.0.0.1")
    {
        var c = f.CreateClient();
        if (remote is not null) c.DefaultRequestHeaders.Add("X-Fake-Remote", remote);
        return c;
    }

    [Theory]
    [InlineData("127.0.0.1", HttpStatusCode.OK)]
    [InlineData("::1", HttpStatusCode.OK)]
    [InlineData("::ffff:127.0.0.1", HttpStatusCode.OK)]
    [InlineData("192.168.1.20", HttpStatusCode.Unauthorized)]
    [InlineData("2001:db8::1", HttpStatusCode.Unauthorized)]
    [InlineData("::ffff:192.168.1.20", HttpStatusCode.Unauthorized)]
    public async Task LocalUser_OnlyForLoopback(string remote, HttpStatusCode expected) =>
        Assert.Equal(expected, (await Client(remote).GetAsync("/api/vehicles")).StatusCode);

    [Fact]
    public async Task LocalUser_UnknownRemoteAddress_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("/api/vehicles")).StatusCode);

    private async Task<HttpResponseMessage> ImportAsync(Action<HttpRequestMessage> configure)
    {
        var c = Client();
        var vid = (await Api.ReadAsync<List<Api.VehicleBody>>(await c.GetAsync("/api/vehicles")))[0].Id;
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(File.OpenRead(TestDb.Fixture("synthetic-trips.csv"))), "file", "t.csv");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vid}/import/csv") { Content = form };
        configure(req);
        return await c.SendAsync(req);
    }

    [Fact]
    public async Task Post_EvilOrigin_403() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await ImportAsync(r => r.Headers.Add("Origin", "https://evil.example"))).StatusCode);

    [Fact]
    public async Task Post_CrossSiteFetchMetadata_403() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await ImportAsync(r => r.Headers.Add("Sec-Fetch-Site", "cross-site"))).StatusCode);

    [Fact]
    public async Task Post_AllowedDevOrigin_Ok() =>
        Assert.Equal(HttpStatusCode.OK, (await ImportAsync(r => r.Headers.Add("Origin", "http://localhost:5173"))).StatusCode);

    [Fact]
    public async Task Post_NoOrigin_Ok() =>
        Assert.Equal(HttpStatusCode.OK, (await ImportAsync(_ => { })).StatusCode);

    [Fact]
    public async Task Get_EvilOrigin_NotBlocked()
    {
        var c = Client();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles");
        req.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(req)).StatusCode);
    }
}
