using DriverBalance.Data;
using DriverBalance.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://localhost:5180");

builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddDbContext<DriverBalanceDbContext>((sp, o) =>
    o.UseSqlite($"Data Source={DbSetup.ResolvePath(sp.GetRequiredService<IConfiguration>())}"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<LegacyMigrationService>();
builder.Services.AddScoped<VehicleDataLoader>();
builder.Services.AddScoped<AnalysisSessionFactory>();

builder.Services.AddAuthentication(LocalUserHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, LocalUserHandler>(LocalUserHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

await DbSetup.InitializeAsync(app.Services);

if (app.Environment.WebRootPath is { } webRoot && Directory.Exists(webRoot))
    app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<ApiErrorFilter>();
VehicleEndpoints.Map(api);
ImportEndpoints.Map(api);
AnalyticsEndpoints.Map(api);
TripEndpoints.Map(api);
ExpenseEndpoints.Map(api);
TyreEndpoints.Map(api);
ExportEndpoints.Map(api);

// SPA fallback: anything that is not an API or MCP path gets the front end.
app.MapFallback(async ctx =>
{
    var file = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/mcp") || !file.Exists)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.SendFileAsync(file);
});

app.Run();

public partial class Program;
