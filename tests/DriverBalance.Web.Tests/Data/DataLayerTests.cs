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

    private static TripRow NewTrip(long vehicleId, long userId = 0) => new Trip("Private", new DateTime(2025, 2, 1, 8, 0, 0), 1, "a",
        new DateTime(2025, 2, 1, 9, 0, 0), 2, "b", TimeSpan.FromHours(1), 1, 0.1, "").ToRow();

    [Fact]
    public async Task Write_AddWithForeignUserId_Throws()
    {
        using var db = new TestDb();
        var v1 = db.SeedVehicle(1);
        using var ctx = db.For(1);
        var row = NewTrip(v1); row.VehicleId = v1; row.UserId = 2;
        ctx.Trips.Add(row);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Write_UpdateOrRemoveForeignStub_Throws()
    {
        using var db = new TestDb();
        db.SeedVehicle(1);
        var v2 = db.SeedVehicle(2);
        long foreignId;
        using (var c2 = db.For(2))
        {
            var r = NewTrip(v2); r.VehicleId = v2; c2.Trips.Add(r);
            await c2.SaveChangesAsync();
            foreignId = r.Id;
        }
        using (var ctx = db.For(1))
        {
            ctx.Trips.Update(new TripRow { Id = foreignId, UserId = 2, VehicleId = v2, Category = "x" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        }
        using (var ctx = db.For(1))
        {
            ctx.Trips.Remove(new TripRow { Id = foreignId, UserId = 2, VehicleId = v2 });
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        }
        using var check = db.For(2);
        Assert.Equal(1, await check.Trips.CountAsync());
    }

    [Fact]
    public async Task Write_UpdateStubWithoutUserId_Throws()
    {
        using var db = new TestDb();
        var v1 = db.SeedVehicle(1);
        using var ctx = db.For(1);
        ctx.Trips.Update(new TripRow { Id = 5, VehicleId = v1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fk_TripOnForeignVehicle_FailsAtDatabase()
    {
        using var db = new TestDb();
        db.SeedVehicle(1);
        var v2 = db.SeedVehicle(2);
        using var ctx = db.For(1);
        var row = NewTrip(v2); row.VehicleId = v2; // UserId stamped as 1, vehicle belongs to 2
        ctx.Trips.Add(row);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fk_DeletingVehicle_RemovesItsRows()
    {
        using var db = new TestDb();
        var v1 = db.SeedVehicle(1);
        using (var ctx = db.For(1))
        {
            using var r = new StreamReader(TestDb.Fixture("synthetic-trips.csv"));
            await new ImportService(ctx).ImportCsvAsync(v1, "t.csv", r, default);
        }
        using (var ctx = db.For(1))
        {
            ctx.Vehicles.Remove(await ctx.Vehicles.SingleAsync(v => v.Id == v1));
            await ctx.SaveChangesAsync();
        }
        using var check = db.For(1);
        Assert.Empty(await check.Trips.ToListAsync());
        Assert.Empty(await check.ImportBatches.ToListAsync());
    }
}
