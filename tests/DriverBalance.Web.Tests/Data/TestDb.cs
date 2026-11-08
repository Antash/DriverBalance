using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web.Tests.Data;

public sealed class FixedUser(long id) : ICurrentUser
{
    public long UserId { get; } = id;
}

/// <summary>SQLite in-memory database kept alive by an open connection, built through the real migrations.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public TestDb()
    {
        _conn.Open();
        using var ctx = For(1);
        ctx.Database.Migrate();
    }

    public DriverBalanceDbContext For(long userId)
    {
        var options = new DbContextOptionsBuilder<DriverBalanceDbContext>().UseSqlite(_conn).Options;
        return new DriverBalanceDbContext(options, new FixedUser(userId));
    }

    /// <summary>Creates the user (if needed) plus one vehicle owned by them; returns the vehicle id.</summary>
    public long SeedVehicle(long userId)
    {
        using var ctx = For(userId);
        if (!ctx.Users.Any(u => u.Id == userId))
            ctx.Users.Add(new UserRow { Id = userId, Name = "user" + userId, CreatedAt = DateTime.UtcNow });
        var v = new VehicleRow { Name = "Test car", FuelType = "Diesel", IsDefault = true, ParametersJson = new AnalysisParameters().ToJson() };
        ctx.Vehicles.Add(v);
        ctx.SaveChanges();
        return v.Id;
    }

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public void Dispose() => _conn.Dispose();
}
