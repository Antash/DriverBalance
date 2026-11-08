using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

/// <summary>Local single-user mode: every request is signed in as user 1.</summary>
public sealed class LocalUserHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LocalUser";
    public const long LocalUserId = 1;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Never sign in a remote caller as the local user, whatever address Kestrel is bound to.
        var ip = Context.Connection.RemoteIpAddress;
        if (ip is null) return Task.FromResult(AuthenticateResult.NoResult());
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (!System.Net.IPAddress.IsLoopback(ip)) return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim("sub", LocalUserId.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
