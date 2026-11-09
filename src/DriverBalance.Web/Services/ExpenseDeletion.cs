using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>Deletes an expense the same way for the REST API and the MCP tool: document rows, orphaned tyre set, then the document folder.</summary>
public static class ExpenseDeletion
{
    /// <summary>Returns false when the expense is not on the vehicle.</summary>
    public static async Task<bool> DeleteAsync(long vid, long id, DriverBalanceDbContext db, DocumentStore documents, CancellationToken ct)
    {
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.VehicleId == vid, ct);
        if (row is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ExpenseDocuments.RemoveRange(await db.ExpenseDocuments.Where(d => d.ExpenseId == id).ToListAsync(ct));
        var setId = row.TyreSetId;
        db.Expenses.Remove(row);
        await db.SaveChangesAsync(ct);
        if (setId is { } sid
            && !await db.Expenses.AnyAsync(x => x.VehicleId == vid && x.TyreSetId == sid, ct)
            && !await db.Vehicles.AnyAsync(v => v.Id == vid && v.InitialTyreSetId == sid, ct))
        {
            var orphan = await db.TyreSets.FirstOrDefaultAsync(t => t.Id == sid && t.VehicleId == vid, ct);
            if (orphan is not null) { db.TyreSets.Remove(orphan); await db.SaveChangesAsync(ct); }
        }
        await tx.CommitAsync(ct);
        documents.DeleteExpenseFolder(vid, id);
        return true;
    }
}
