using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web.Tests.Data;

public class DataLayerTests
{
    private static int CsvTripCount()
    {
        using var r = new StreamReader(TestDb.Fixture("synthetic-trips.csv"));
        return VolvoCsvParser.Parse(r).Trips.Count;
    }

    [Fact]
    public async Task ImportCsv_Twice_SkipsDuplicates()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);
        var expected = CsvTripCount();
        Assert.True(expected > 100);

        ImportResult first, second;
        using (var ctx = db.For(1))
        {
            using var r = new StreamReader(TestDb.Fixture("synthetic-trips.csv"));
            first = await new ImportService(ctx).ImportCsvAsync(vehicleId, "trips.csv", r, default);
        }
        using (var ctx = db.For(1))
        {
            using var r = new StreamReader(TestDb.Fixture("synthetic-trips.csv"));
            second = await new ImportService(ctx).ImportCsvAsync(vehicleId, "trips.csv", r, default);
        }

        Assert.Equal(expected, first.Imported);
        Assert.Equal(0, first.Skipped);
        Assert.NotNull(first.FirstTrip);
        Assert.True(first.FirstTrip <= first.LastTrip);
        Assert.Equal(0, second.Imported);
        Assert.Equal(expected, second.Skipped);
        Assert.NotEqual(first.BatchId, second.BatchId);
        using var check = db.For(1);
        Assert.Equal(expected, await check.Trips.CountAsync());
        Assert.Equal(2, await check.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task ImportCsv_ForeignVehicle_Throws()
    {
        using var db = new TestDb();
        var otherVehicle = db.SeedVehicle(2);
        using var ctx = db.For(1);
        using var r = new StreamReader(TestDb.Fixture("synthetic-trips.csv"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ImportService(ctx).ImportCsvAsync(otherVehicle, "trips.csv", r, default));
    }

    private static async Task<LegacyMigrationResult> Migrate(TestDb db, long vehicleId)
    {
        using var ctx = db.For(1);
        await using var trips = File.OpenRead(TestDb.Fixture("synthetic-triprecords.json"));
        await using var maint = File.OpenRead(TestDb.Fixture("synthetic-maintenance.json"));
        return await new LegacyMigrationService(ctx).MigrateAsync(vehicleId, trips, maint, default);
    }

    [Fact]
    public async Task LegacyMigration_Twice_IsIdempotent()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);

        var first = await Migrate(db, vehicleId);
        var second = await Migrate(db, vehicleId);

        Assert.Equal(new LegacyMigrationResult(4, 0, 4, 0), first);
        Assert.Equal(new LegacyMigrationResult(0, 4, 0, 4), second);
        using var check = db.For(1);
        Assert.Equal(4, await check.Trips.CountAsync());
        Assert.Equal(4, await check.Expenses.CountAsync());
    }

    [Fact]
    public async Task LegacyMigration_ZeroCostGetsNote()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);
        await Migrate(db, vehicleId);

        using var check = db.For(1);
        var rows = await check.Expenses.OrderBy(e => e.Date).ToListAsync();
        var zero = rows[0];
        Assert.Equal(0m, zero.Amount);
        Assert.Equal("imported — check cost", zero.Note);
        Assert.Equal("CZK", zero.Currency);
        Assert.Equal(ExpenseSource.Migration, zero.Source);
        Assert.Equal(ExpenseCategory.Maintenance, zero.Category);
        Assert.Equal(3, zero.MaintenanceFlags);
        Assert.All(rows.Skip(1), r => Assert.Null(r.Note));
    }

    [Fact]
    public async Task LegacyMigration_MapsTripFields()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);
        await Migrate(db, vehicleId);

        using var check = db.For(1);
        var trips = (await check.Trips.OrderBy(t => t.Start).ToListAsync()).Select(t => t.ToDomain()).ToList();
        Assert.Equal("commute", trips[0].Notes);
        Assert.Equal("", trips[1].Notes);
        Assert.Equal(TimeSpan.FromMinutes(20), trips[0].Duration);
        Assert.Equal(new TimeSpan(1, 1, 30, 0), trips[3].Duration);
    }

    [Fact]
    public void LegacyJsonReader_HandlesBomAndNullStrings()
    {
        using var s = File.OpenRead(TestDb.Fixture("synthetic-triprecords.json"));
        var trips = LegacyJsonReader.ReadTrips(s);
        Assert.Equal(4, trips.Count);
        Assert.Equal("", trips[1].Notes);
        Assert.Equal(new DateTime(2025, 4, 2, 8, 0, 0), trips[2].Start);
        Assert.Equal(129.0, trips[2].DistanceKm);
    }

    [Fact]
    public void LegacyJsonReader_MapsMaintenance()
    {
        using var s = File.OpenRead(TestDb.Fixture("synthetic-maintenance.json"));
        var items = LegacyJsonReader.ReadMaintenance(s);
        Assert.Equal(4, items.Count);
        var first = items[0];
        Assert.Equal(new DateOnly(2025, 3, 10), first.Date);
        Assert.Equal(MaintenanceType.OilChange | MaintenanceType.BrakeFluid, first.Maintenance);
        Assert.Equal("Oil + brake fluid", first.MaintenanceDescription);
        Assert.Equal(1864, first.Odometer);
        Assert.Equal("CZK", first.Currency);
        Assert.Equal(ExpenseSource.Migration, first.Source);
        Assert.Equal(ExpenseCategory.Maintenance, first.Category);
    }

    [Fact]
    public async Task UserScoping_OtherUserSeesNothing()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);
        db.SeedVehicle(2);
        using (var ctx = db.For(1))
        {
            var row = new Trip("Private", new DateTime(2025, 1, 1, 8, 0, 0), 10, "a", new DateTime(2025, 1, 1, 9, 0, 0), 20, "b",
                TimeSpan.FromHours(1), 10, 1, "").ToRow();
            row.VehicleId = vehicleId;
            ctx.Trips.Add(row);
            await ctx.SaveChangesAsync();
        }

        long tripId;
        using (var ctx = db.For(1))
        {
            var t = await ctx.Trips.SingleAsync();
            tripId = t.Id;
            Assert.Equal(1, t.UserId);
        }

        using var other = db.For(2);
        Assert.Empty(await other.Trips.ToListAsync());
        Assert.Null(await other.Trips.FirstOrDefaultAsync(t => t.Id == tripId));
        Assert.Null(await other.Trips.IgnoreQueryFilters().Where(t => t.UserId == 2).FirstOrDefaultAsync());
        Assert.Single(await other.Vehicles.ToListAsync());   // only user 2's own vehicle
        Assert.NotEqual(vehicleId, (await other.Vehicles.SingleAsync()).Id);
        Assert.Equal(2, await other.Users.CountAsync());      // users are not filtered
    }

    [Fact]
    public async Task Parameters_RoundTrip()
    {
        using var db = new TestDb();
        var vehicleId = db.SeedVehicle(1);
        var custom = new AnalysisParameters { ColdStartGapMinutes = 50, LowessSpan = 0.4, HeatmapKmCap = 80, DpfMovingAverageWeeks = 5 };
        using (var ctx = db.For(1))
        {
            (await ctx.Vehicles.SingleAsync(v => v.Id == vehicleId)).ParametersJson = custom.ToJson();
            await ctx.SaveChangesAsync();
        }
        using var check = db.For(1);
        var loaded = Mapping.ParseParameters((await check.Vehicles.SingleAsync(v => v.Id == vehicleId)).ParametersJson);
        Assert.Equal(custom, loaded);
        Assert.Equal(new AnalysisParameters(), Mapping.ParseParameters(new AnalysisParameters().ToJson()));
    }

    [Fact]
    public void Mapping_RoundTripsExpenseAndTyreSet()
    {
        var e = new Expense
        {
            Date = new DateOnly(2025, 5, 5), Category = ExpenseCategory.Fuel, Amount = 1234.56m, Currency = "EUR", HomeAmount = 30000m,
            Odometer = 4000, Note = "n", Litres = 40.5, FullTank = true, Station = "S",
            Maintenance = MaintenanceType.Coolant | MaintenanceType.AirFilter, MaintenanceDescription = "d", TyreSetId = 3, Source = ExpenseSource.Mcp,
            CoversFrom = new DateOnly(2025, 1, 1), CoversTo = new DateOnly(2026, 1, 1),
        };
        Assert.Equal(e, e.ToRow().ToDomain());

        var t = new TyreSet { Name = "Winter", Season = TyreSeason.Winter, Description = "x", PurchaseDate = new DateOnly(2024, 10, 1), Retired = true };
        Assert.Equal(t, t.ToRow().ToDomain());
    }
}
