using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Data;

public sealed class DriverBalanceDbContext(DbContextOptions<DriverBalanceDbContext> o, ICurrentUser user) : DbContext(o)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<VehicleRow> Vehicles => Set<VehicleRow>();
    public DbSet<TripRow> Trips => Set<TripRow>();
    public DbSet<ExpenseRow> Expenses => Set<ExpenseRow>();
    public DbSet<TyreSetRow> TyreSets => Set<TyreSetRow>();
    public DbSet<ImportBatchRow> ImportBatches => Set<ImportBatchRow>();
    public DbSet<ApiTokenRow> ApiTokens => Set<ApiTokenRow>();

    // Read through the context instance so EF evaluates it per context, not once per model.
    private long CurrentUserId => user.UserId;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<VehicleRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<TripRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<ExpenseRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<TyreSetRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<ImportBatchRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<ApiTokenRow>().HasQueryFilter(x => x.UserId == CurrentUserId);

        b.Entity<VehicleRow>().HasIndex(x => x.UserId);
        b.Entity<TripRow>().HasIndex(x => new { x.VehicleId, x.Start, x.End }).IsUnique();
        b.Entity<TripRow>().HasIndex(x => x.UserId);
        b.Entity<ExpenseRow>().HasIndex(x => new { x.VehicleId, x.Date });
        b.Entity<ExpenseRow>().Property(x => x.Category).HasConversion<string>();
        b.Entity<ExpenseRow>().Property(x => x.Source).HasConversion<string>();
        b.Entity<TyreSetRow>().Property(x => x.Season).HasConversion<string>();
        b.Entity<TyreSetRow>().HasIndex(x => x.VehicleId);
        b.Entity<ImportBatchRow>().HasIndex(x => x.VehicleId);
        b.Entity<ApiTokenRow>().HasIndex(x => x.TokenHash).IsUnique();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUser();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUser();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampUser()
    {
        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
            if (entry.State == EntityState.Added && entry.Entity.UserId == 0)
                entry.Entity.UserId = user.UserId;
    }
}
