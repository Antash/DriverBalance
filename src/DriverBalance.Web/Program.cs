using DriverBalance.Data;
using DriverBalance.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var hosting = HostingOptions.From(builder.Configuration);
hosting.Validate(); // Hosted mode refuses to start without Google client id/secret and the admin email
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(hosting));

if (!hosting.IsHosted && string.IsNullOrEmpty(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://localhost:5180");

builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = DocumentEndpoints.RequestLimit);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddDbContext<DriverBalanceDbContext>((sp, o) =>
    o.UseSqlite($"Data Source={DbSetup.ResolvePath(sp.GetRequiredService<IConfiguration>())}"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<VehicleDataLoader>();
builder.Services.AddScoped<AnalysisSessionFactory>();

if (hosting.IsHosted)
{
    // Google sign-in (cookie) and API tokens only. The LocalUser scheme is not registered at all.
    builder.Services.AddHostedAuth(builder.Configuration, hosting);
}
else
{
    builder.Services.AddAuthentication(LocalUserHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, LocalUserHandler>(LocalUserHandler.SchemeName, _ => { })
        .AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(ApiTokenHandler.SchemeName, _ => { });
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("ApiToken", p => p.AddAuthenticationSchemes(ApiTokenHandler.SchemeName).RequireAuthenticatedUser())
        .AddPolicy(HostedAuth.AdminPolicy, p => p.RequireAuthenticatedUser().RequireClaim(HostedAuth.AdminClaim, "true"));
}

// Stateless streamable HTTP: no session affinity, every request is authenticated on its own.
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<CarTools>();

var app = builder.Build();

await DbSetup.InitializeAsync(app.Services, seedVehicle: !hosting.IsHosted);

if (hosting.IsHosted) app.UseForwardedHeaders();

if (app.Environment.WebRootPath is { } webRoot && Directory.Exists(webRoot))
    app.UseStaticFiles();

app.UseCrossSiteGuard();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<ApiErrorFilter>();
VehicleEndpoints.Map(api);
ImportEndpoints.Map(api);
AnalyticsEndpoints.Map(api);
TripEndpoints.Map(api);
ExpenseEndpoints.Map(api);
PurchaseCostEndpoints.Map(api);
TyreEndpoints.Map(api);
DocumentEndpoints.Map(api);
TokenEndpoints.Map(api);
ExportEndpoints.Map(api);
AccountEndpoints.Map(api);
AdminEndpoints.Map(api);

app.MapAuthEndpoints();
app.MapMcp("/mcp").RequireAuthorization("ApiToken");

// SPA fallback: anything that is not an API or MCP path gets the front end.
app.MapFallback(async ctx =>
{
    var file = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/mcp") || ctx.Request.Path.StartsWithSegments("/auth") || !file.Exists)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.SendFileAsync(file);
});

app.Run();

public partial class Program;
