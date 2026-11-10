using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DriverBalance.Data;

public sealed class DriverBalanceDbContext(DbContextOptions<DriverBalanceDbContext> o, ICurrentUser user) : DbContext(o)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<InviteRow> Invites => Set<InviteRow>();
    public DbSet<VehicleRow> Vehicles => Set<VehicleRow>();
    public DbSet<TripRow> Trips => Set<TripRow>();
    public DbSet<ExpenseRow> Expenses => Set<ExpenseRow>();
    public DbSet<ExpenseDocumentRow> ExpenseDocuments => Set<ExpenseDocumentRow>();
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
        b.Entity<ExpenseDocumentRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<TyreSetRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<ImportBatchRow>().HasQueryFilter(x => x.UserId == CurrentUserId);
        b.Entity<ApiTokenRow>().HasQueryFilter(x => x.UserId == CurrentUserId);

        b.Entity<UserRow>().HasIndex(x => x.GoogleSubject).IsUnique();
        b.Entity<InviteRow>().HasIndex(x => x.Email).IsUnique();

        b.Entity<VehicleRow>().HasIndex(x => x.UserId);
        b.Entity<TripRow>().HasIndex(x => new { x.VehicleId, x.Start, x.End }).IsUnique();
        b.Entity<TripRow>().HasIndex(x => x.UserId);
        b.Entity<ExpenseRow>().HasIndex(x => new { x.VehicleId, x.Date });
        b.Entity<ExpenseRow>().Property(x => x.Category).HasConversion<string>();
        b.Entity<ExpenseRow>().Property(x => x.Source).HasConversion<string>();
        b.Entity<TyreSetRow>().Property(x => x.Season).HasConversion<string>();
        b.Entity<VehicleRow>().Property(x => x.FinancingFrequency).HasConversion<string>();
        b.Entity<TyreSetRow>().HasIndex(x => x.VehicleId);
        b.Entity<ExpenseDocumentRow>().HasIndex(x => x.ExpenseId);
        b.Entity<ExpenseDocumentRow>().HasOne<ExpenseRow>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ImportBatchRow>().HasIndex(x => x.VehicleId);
        b.Entity<ApiTokenRow>().HasIndex(x => x.TokenHash).IsUnique();

        // Owner integrity: composite keys make a child's UserId match its vehicle's UserId at the DB level.
        b.Entity<VehicleRow>().HasAlternateKey(x => new { x.UserId, x.Id });
        b.Entity<TyreSetRow>().HasAlternateKey(x => new { x.UserId, x.Id });
        b.Entity<TripRow>().HasOne<VehicleRow>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.VehicleId }).HasPrincipalKey(v => new { v.UserId, v.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ExpenseRow>().HasOne<VehicleRow>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.VehicleId }).HasPrincipalKey(v => new { v.UserId, v.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TyreSetRow>().HasOne<VehicleRow>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.VehicleId }).HasPrincipalKey(v => new { v.UserId, v.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ImportBatchRow>().HasOne<VehicleRow>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.VehicleId }).HasPrincipalKey(v => new { v.UserId, v.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TripRow>().HasOne<ImportBatchRow>().WithMany()
            .HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.SetNull);
        // Composite set-null would null UserId too, so deleting a tyre set still referenced by expenses is restricted.
        b.Entity<ExpenseRow>().HasOne<TyreSetRow>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.TyreSetId }).HasPrincipalKey(t => new { t.UserId, t.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VehicleRow>().HasOne<TyreSetRow>().WithMany()
            .HasForeignKey(x => x.InitialTyreSetId).OnDelete(DeleteBehavior.SetNull);

        foreach (var type in new[] { typeof(VehicleRow), typeof(TripRow), typeof(ExpenseRow), typeof(TyreSetRow), typeof(ExpenseDocumentRow), typeof(ImportBatchRow), typeof(ApiTokenRow) })
        {
            var e = b.Entity(type);
            e.HasOne(typeof(UserRow)).WithMany().HasForeignKey(nameof(IUserOwned.UserId)).OnDelete(DeleteBehavior.Cascade);
            e.Property(nameof(IUserOwned.UserId)).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
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

    /// <summary>Stamps new rows and rejects any write touching another user's row (query filters do not apply to SaveChanges).</summary>
    private void StampUser()
    {
        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId == 0)
                entry.Entity.UserId = user.UserId;
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && entry.Entity.UserId != user.UserId)
                throw new InvalidOperationException($"Refusing to write {entry.Metadata.ClrType.Name} owned by user {entry.Entity.UserId} as user {user.UserId}.");
        }
    }
}
