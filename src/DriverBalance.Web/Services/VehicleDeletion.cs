using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>Removes one vehicle with everything it owns, and its documents on disk.</summary>
public static class VehicleDeletion
{
    public static async Task DeleteAsync(DriverBalanceDbContext db, DocumentStore documents, long vehicleId, CancellationToken ct)
    {
        var wasDefault = await db.Vehicles.Where(v => v.Id == vehicleId).Select(v => v.IsDefault).FirstOrDefaultAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Expenses first: they restrict deleting the tyre sets they use, and the vehicle delete cascades to both at once.
        await db.Expenses.Where(e => e.VehicleId == vehicleId).ExecuteDeleteAsync(ct);
        await db.Vehicles.Where(v => v.Id == vehicleId).ExecuteDeleteAsync(ct);
        if (wasDefault)
        {
            var next = await db.Vehicles.OrderBy(v => v.Id).FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
        await tx.CommitAsync(ct);

        documents.DeleteVehicleFolder(vehicleId);
    }
}
