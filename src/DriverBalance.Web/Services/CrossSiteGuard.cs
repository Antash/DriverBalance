namespace DriverBalance.Web;

/// <summary>Blocks state-changing /api requests that a browser sends from another site (ambient local auth must not be usable cross-site).</summary>
public static class CrossSiteGuard
{
    public static readonly string[] DefaultOrigins =
        ["http://localhost:5180", "http://127.0.0.1:5180", "http://localhost:5173", "http://127.0.0.1:5173"];

    public static void UseCrossSiteGuard(this WebApplication app)
    {
        var allowed = (app.Configuration.GetSection("DriverBalance:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
            ? configured : DefaultOrigins).ToHashSet(StringComparer.OrdinalIgnoreCase);

        app.Use(async (ctx, next) =>
        {
            var m = ctx.Request.Method;
            bool safe = HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m);
            if (!safe && ctx.Request.Path.StartsWithSegments("/api"))
            {
                bool crossSite = string.Equals(ctx.Request.Headers["Sec-Fetch-Site"], "cross-site", StringComparison.OrdinalIgnoreCase);
                var origin = ctx.Request.Headers.Origin.ToString();
                if (crossSite || (origin.Length > 0 && !allowed.Contains(origin)))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsJsonAsync(new ErrorsResponse(["Cross-site request blocked"]));
                    return;
                }
            }
            await next();
        });
    }
}
