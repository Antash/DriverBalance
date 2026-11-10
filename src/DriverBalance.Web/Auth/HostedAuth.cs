using System.Security.Claims;
using DriverBalance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

/// <summary>Hosted mode: Google sign-in with a long-lived cookie, plus API tokens. The browser never sees a token.</summary>
public static class HostedAuth
{
    public const string Cookie = "DriverBalance.Session";
    public const string External = "DriverBalance.External";
    public const string Smart = "DriverBalance.Smart";
    public const string AdminPolicy = "Admin";
    public const string AdminClaim = "admin";

    private static readonly TimeSpan SessionLength = TimeSpan.FromDays(30);
    private static readonly TimeSpan RecheckEvery = TimeSpan.FromMinutes(5);

    public static void AddHostedAuth(this IServiceCollection services, IConfiguration config, HostingOptions h)
    {
        services.AddScoped<UserProvisioner>();

        // Behind App Service's proxy the request looks like http; the sign-in redirect and Secure cookies need the real scheme.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // Sessions must survive restarts and redeploys: keep the cookie-protection keys next to the database (on persistent storage).
        var keys = config["DriverBalance:KeysPath"];
        var keysDir = string.IsNullOrWhiteSpace(keys)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(DbSetup.ResolvePath(config)))!, "keys")
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(keys));
        Directory.CreateDirectory(keysDir);
        services.AddDataProtection().SetApplicationName("DriverBalance").PersistKeysToFileSystem(new DirectoryInfo(keysDir));

        services.AddAuthentication(Smart)
            .AddPolicyScheme(Smart, "Cookie or API token", o => o.ForwardDefaultSelector = ctx =>
                ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer " + ApiTokens.Prefix, StringComparison.OrdinalIgnoreCase)
                    ? ApiTokenHandler.SchemeName
                    : Cookie)
            .AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(ApiTokenHandler.SchemeName, _ => { })
            .AddCookie(Cookie, o =>
            {
                o.Cookie.Name = "db_session";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = SessionLength;
                o.SlidingExpiration = true;
                // The SPA decides what to show: API callers get a plain status code, never a redirect.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = Revalidate;
            })
            .AddCookie(External, o =>
            {
                o.Cookie.Name = "db_external";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
            {
                o.ClientId = h.GoogleClientId!;
                o.ClientSecret = h.GoogleClientSecret!;
                o.SignInScheme = External;
                o.CallbackPath = "/signin-google";
                o.SaveTokens = false;
                o.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("ApiToken", p => p.AddAuthenticationSchemes(ApiTokenHandler.SchemeName).RequireAuthenticatedUser())
            .AddPolicy(AdminPolicy, p => p.RequireAuthenticatedUser().RequireClaim(AdminClaim, "true"));
    }

    public static ClaimsPrincipal Principal(long userId, bool isAdmin)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()) };
        if (isAdmin) claims.Add(new Claim(AdminClaim, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Cookie));
    }

    /// <summary>At most every 5 minutes: is the user still there and allowed? Removing an invite or deleting a user signs them out.</summary>
    private static async Task Revalidate(CookieValidatePrincipalContext ctx)
    {
        var clock = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();
        if (DateTimeOffset.TryParse(ctx.Properties.GetString("checked"), out var last) && now - last < RecheckEvery) return;

        if (!long.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var id))
        {
            Reject(ctx);
            return;
        }
        var check = await ctx.HttpContext.RequestServices.GetRequiredService<UserProvisioner>().ValidateAsync(id, ctx.HttpContext.RequestAborted);
        if (!check.Valid)
        {
            Reject(ctx);
            return;
        }
        ctx.Properties.SetString("checked", now.ToString("o"));
        ctx.ReplacePrincipal(Principal(id, check.IsAdmin));
        ctx.ShouldRenew = true;
    }

    private static void Reject(CookieValidatePrincipalContext ctx)
    {
        ctx.RejectPrincipal();
        _ = ctx.HttpContext.SignOutAsync(Cookie);
    }

    /// <summary>Only same-site paths: "/..." but not "//host" or "/\host".</summary>
    public static string SafeReturn(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && !url.StartsWith("//") && !url.StartsWith("/\\") && !url.Any(char.IsControl) ? url : "/";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Local mode has no sign-in: send the browser to the app.
        if (!app.Services.GetRequiredService<IOptions<HostingOptions>>().Value.IsHosted)
        {
            app.MapGet("/auth/login", () => Results.Redirect("/"));
            return;
        }

        app.MapGet("/auth/login", (string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/callback?returnUrl=" + Uri.EscapeDataString(SafeReturn(returnUrl)) },
                [GoogleDefaults.AuthenticationScheme]));

        app.MapGet("/auth/callback", async (HttpContext ctx, UserProvisioner provisioner, TimeProvider clock, string? returnUrl) =>
        {
            var external = await ctx.AuthenticateAsync(External);
            await ctx.SignOutAsync(External);
            var p = external.Principal;
            if (!external.Succeeded || p is null) return Results.Redirect("/?signin=failed");

            var identity = new GoogleIdentity(
                p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                p.FindFirstValue(ClaimTypes.Email),
                string.Equals(p.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
                p.FindFirstValue(ClaimTypes.Name),
                p.FindFirstValue("urn:google:picture"));
            var result = await provisioner.SignInAsync(identity, ctx.RequestAborted);
            if (result.Status != SignInStatus.Ok)
                return Results.Redirect("/?signin=" + result.Status switch
                {
                    SignInStatus.InviteOnly => "invite-only",
                    SignInStatus.Blocked => "blocked",
                    _ => "failed",
                });

            var now = clock.GetUtcNow();
            var props = new AuthenticationProperties { IsPersistent = true, IssuedUtc = now, ExpiresUtc = now + SessionLength };
            props.SetString("checked", now.ToString("o"));
            await ctx.SignInAsync(Cookie, Principal(result.UserId, result.IsAdmin), props);
            return Results.Redirect(SafeReturn(returnUrl));
        });
    }
}
