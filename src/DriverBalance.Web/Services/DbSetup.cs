using DriverBalance.Data;
using DriverBalance.Core;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class DbSetup
{
    public const string ConfigKey = "DriverBalance:DatabasePath";

    public static string ResolvePath(IConfiguration config)
    {
        var configured = config[ConfigKey];
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverBalance", "driverbalance.db")
            : Environment.ExpandEnvironmentVariables(configured);
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        return path;
    }

    private sealed class SeedUser : ICurrentUser
    {
        public long UserId => LocalUserHandler.LocalUserId;
    }

    /// <summary>Applies migrations, then seeds user 1 and a default vehicle when the user has none.</summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
        await using var db = new DriverBalanceDbContext(options, new SeedUser());
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync(u => u.Id == LocalUserHandler.LocalUserId))
            db.Users.Add(new UserRow { Id = LocalUserHandler.LocalUserId, Name = "Local user", CreatedAt = DateTime.UtcNow });
        if (!await db.Vehicles.AnyAsync())
            db.Vehicles.Add(new VehicleRow { Name = "My Volvo", FuelType = "Diesel", IsDefault = true, ParametersJson = new AnalysisParameters().ToJson() });
        await db.SaveChangesAsync();
    }
}
