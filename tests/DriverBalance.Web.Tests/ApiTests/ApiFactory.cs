using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DriverBalance.Core;
using DriverBalance.Data;
using DriverBalance.Web.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web.Tests.ApiTests;

/// <summary>Reads the acting user from X-Test-User (default 1), replacing the LocalUser scheme.</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers.TryGetValue("X-Test-User", out var v) ? v.ToString() : "1";
        var identity = new ClaimsIdentity([new Claim("sub", id)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

/// <summary>One per test class: its own temp SQLite file and web root, a pinned clock and the test auth scheme.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "driverbalance-test-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "wwwroot"));
        File.WriteAllText(Path.Combine(_dir, "wwwroot", "index.html"), "<html>spa-index</html>");
    }

    public string DocumentsDir => Path.Combine(_dir, "documents");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DriverBalance:DocumentsPath", DocumentsDir);
        builder.UseSetting("DriverBalance:DatabasePath", Path.Combine(_dir, "test.db"));
        builder.UseWebRoot(Path.Combine(_dir, "wwwroot"));
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            ConfigureAuth(s);
        });
    }

    /// <summary>Replaces the LocalUser scheme with the X-Test-User scheme; the security tests override this to keep the real one.</summary>
    protected virtual void ConfigureAuth(IServiceCollection s)
    {
        s.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        s.Configure<AuthenticationOptions>(o =>
        {
            o.DefaultScheme = o.DefaultAuthenticateScheme = o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
        });
    }

    public HttpClient ClientFor(long userId)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        return c;
    }

    /// <summary>Inserts an expense straight into the database, for categories the API refuses to create (Purchase).</summary>
    public void SeedExpense(long vehicleId, DateOnly date, ExpenseCategory category, decimal amount, string? note = null, long userId = 1)
    {
        using var scope = Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
        using var db = new DriverBalanceDbContext(options, new FixedUser(userId));
        db.Expenses.Add(new ExpenseRow { VehicleId = vehicleId, Date = date, Category = category, Amount = amount, Currency = "CZK", Note = note, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    /// <summary>Inserts the user row (the API only seeds user 1).</summary>
    public void EnsureUser(long userId)
    {
        using var scope = Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
        using var db = new DriverBalanceDbContext(options, new FixedUser(userId));
        if (db.Users.Any(u => u.Id == userId)) return;
        db.Users.Add(new UserRow { Id = userId, Name = "user" + userId, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}

public static class Api
{
    public static async Task<long> NewVehicleAsync(HttpClient c, string name = "Test car")
    {
        var r = await c.PostAsJsonAsync("/api/vehicles", new { name }, ApiJson.Options);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<VehicleBody>(ApiJson.Options))!.Id;
    }

    public static async Task<ImportBody> ImportFixtureAsync(HttpClient c, long vid)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(File.OpenRead(TestDb.Fixture("synthetic-trips.csv"))), "file", "synthetic-trips.csv");
        var r = await c.PostAsync($"/api/vehicles/{vid}/import/csv", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ImportBody>(ApiJson.Options))!;
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient c, string url, object body) =>
        c.PostAsJsonAsync(url, body, ApiJson.Options);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<T>(ApiJson.Options))!;

    public sealed record VehicleBody(long Id, string Name, string HomeCurrency, string TimeZone, long? InitialTyreSetId, bool IsDefault, DateOnly? HistoryStart = null, int? HistoryStartOdometer = null);
    public sealed record ImportBody(long BatchId, int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip, string[] Warnings);
    public sealed record ErrorsBody(string[] Errors);
}
