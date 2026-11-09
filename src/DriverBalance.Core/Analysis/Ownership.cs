namespace DriverBalance.Core;

public sealed record PurchaseCost(DateOnly Date, decimal Amount, string? Note);

/// <summary>Cost of ownership in the home currency; numbers are null when they cannot be computed.</summary>
public sealed record OwnershipCosts(
    DateOnly? PurchaseDate, IReadOnlyList<PurchaseCost> PurchaseCosts, decimal PurchaseTotal, decimal? RunningCosts, double? KmDriven,
    double? Months, decimal? EstimatedResaleValue, DateOnly? ResaleEstimateDate, decimal? NetCost, decimal? NetCostPerKm, decimal? NetCostPerMonth);

public static class Ownership
{
    private const double DaysPerMonth = 365.25 / 12;

    /// <summary>Purchase expenses are listed and summed; running costs, km and months run from the purchase date to the end of today.</summary>
    public static OwnershipCosts Compute(AnalysisContext c)
    {
        var data = c.Data;
        var costs = data.Expenses.Where(e => e.Category == ExpenseCategory.Purchase)
            .Select(e => (e, Amount: Money.HomeAmountOf(e, data.HomeCurrency)))
            .Where(x => x.Amount is not null)
            .OrderBy(x => x.e.Date).ThenBy(x => x.e.Id)
            .Select(x => new PurchaseCost(x.e.Date, x.Amount!.Value, x.e.Note))
            .ToList();
        decimal purchaseTotal = costs.Sum(x => x.Amount);

        if (data.PurchaseDate is not { } bought)
            return new OwnershipCosts(null, costs, purchaseTotal, null, null, null, data.EstimatedResaleValue, data.ResaleEstimateDate, null, null, null);

        var period = new Period(bought, data.Today.AddDays(1), "Ownership");
        decimal running = Money.For(period, data.Expenses, data.HomeCurrency, null).Total;
        double? km = c.Timeline.KmIn(period);
        double months = Math.Round((period.To.DayNumber - period.From.DayNumber) / DaysPerMonth, 1);
        decimal net = purchaseTotal + running - (data.EstimatedResaleValue ?? 0m);
        decimal? perKm = km is > 0 ? Money.Round(net / (decimal)km.Value) : null;
        decimal? perMonth = months > 0 ? Money.Round(net / (decimal)months) : null;
        return new OwnershipCosts(bought, costs, purchaseTotal, running, km, months, data.EstimatedResaleValue, data.ResaleEstimateDate, net, perKm, perMonth);
    }
}
