using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DriverBalance.Data;

/// <summary>Used only by `dotnet ef` to create migrations.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DriverBalanceDbContext>
{
    private sealed class NoUser : ICurrentUser
    {
        public long UserId => 0;
    }

    public DriverBalanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DriverBalanceDbContext>().UseSqlite("DataSource=design-time.db").Options;
        return new DriverBalanceDbContext(options, new NoUser());
    }
}
