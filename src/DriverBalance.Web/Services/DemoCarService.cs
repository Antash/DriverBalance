using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>Creates the generated example car for the signed-in user, in one transaction.</summary>
public sealed class DemoCarService(DriverBalanceDbContext db, VehicleDataLoader loader, ICurrentUser user)
{
    public async Task<VehicleRow> CreateAsync(CancellationToken ct)
    {
        var today = loader.TodayFor(new VehicleRow { TimeZone = "Europe/Berlin" });
        var car = DemoData.Generate(today);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var hasCar = await db.Vehicles.AnyAsync(ct); // authoritative check, inside the transaction
        if (hasCar) throw new ApiValidationException("You already have a car.");

        var vehicle = new VehicleRow
        {
            Name = car.Name, FuelType = car.FuelType, HomeCurrency = car.HomeCurrency, TimeZone = car.TimeZone,
            ParametersJson = new AnalysisParameters().ToJson(), IsDefault = !hasCar, IsDemo = true,
            HistoryStart = car.HistoryStart, HistoryStartOdometer = car.HistoryStartOdometer, PurchaseDate = car.PurchaseDate,
            EstimatedResaleValue = car.EstimatedResaleValue, ResaleEstimateDate = car.ResaleEstimateDate,
            DetailsJson = new CarDetails(car.Model, car.Engine, car.Trim, car.ModelYear, car.ExteriorColour, car.Interior,
                car.Equipment, car.Transmission, car.Awd).ToJson(),
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        var vehicleId = vehicle.Id;
        var userId = user.UserId;
        db.ChangeTracker.Clear(); // later rows are written by id and user; tracking thousands of them is the slow part

        var tyreRows = car.TyreSets.Select(t => (t.Key, Row: new TyreSetRow
        {
            VehicleId = vehicleId, UserId = userId, Name = t.Name, Brand = t.Brand, Model = t.Model, Size = t.Size, Season = t.Season,
            PurchaseDate = t.PurchaseDate, Retired = t.Retired,
        })).ToList();
        db.TyreSets.AddRange(tyreRows.Select(t => t.Row));
        await db.SaveChangesAsync(ct);
        var tyreIds = tyreRows.ToDictionary(t => t.Key, t => t.Row.Id);
        var initialTyreSetId = tyreIds[car.InitialTyreSetKey];
        await db.Vehicles.Where(v => v.Id == vehicleId).ExecuteUpdateAsync(s => s.SetProperty(v => v.InitialTyreSetId, initialTyreSetId), ct);
        vehicle.InitialTyreSetId = initialTyreSetId;

        var batch = new ImportBatchRow
        {
            VehicleId = vehicleId, UserId = userId, FileName = "Demo data", ImportedAt = DateTime.UtcNow, Imported = car.Trips.Count,
            FirstTrip = car.Trips.Count == 0 ? null : DateOnly.FromDateTime(car.Trips.Min(t => t.Start)),
            LastTrip = car.Trips.Count == 0 ? null : DateOnly.FromDateTime(car.Trips.Max(t => t.Start)),
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        db.Trips.AddRange(car.Trips.Select(t =>
        {
            var row = t.ToRow();
            row.Id = 0; row.VehicleId = vehicleId; row.UserId = userId; row.ImportBatchId = batch.Id;
            return row;
        }));
        await db.SaveChangesAsync(ct);

        db.Expenses.AddRange(car.Expenses.Select(e =>
        {
            var row = new Expense
            {
                Date = e.Date, Category = e.Category, Amount = e.Amount, Currency = car.HomeCurrency, Odometer = e.Odometer, Note = e.Note,
                CoversFrom = e.CoversFrom, CoversTo = e.CoversTo, Litres = e.Litres, FullTank = e.FullTank, Station = e.Station, Place = e.Place,
                Maintenance = e.Flags, Work = e.Work ?? Array.Empty<MaintenanceWork>(), Source = ExpenseSource.Web,
            }.ToRow();
            row.VehicleId = vehicleId; row.UserId = userId;
            row.TyreSetId = e.TyreSetKey is null ? null : tyreIds[e.TyreSetKey];
            return row;
        }));
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return vehicle;
    }
}
