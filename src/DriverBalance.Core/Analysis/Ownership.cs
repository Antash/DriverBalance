namespace DriverBalance.Core;

public sealed record PurchaseCost(long Id, DateOnly Date, decimal Amount, string? Note);

/// <summary>Financing as shown in the ownership response: the contract plus its total and last payment date.</summary>
public sealed record OwnershipFinancing(DateOnly Start, decimal AmountPerPayment, FinancingFrequency Frequency, int Payments, decimal Balloon, decimal Total, DateOnly EndDate)
{
    public static OwnershipFinancing From(Financing f) => new(f.Start, f.AmountPerPayment, f.Frequency, f.Payments, f.Balloon, f.Total, f.EndDate);
}

/// <summary>
/// Cost of ownership in the home currency; numbers are null when they cannot be computed.
/// <c>PurchaseTotal</c> = lump sums (<c>LumpSumTotal</c>) + the whole financing contract (<c>Financing.Total</c>).
/// </summary>
public sealed record OwnershipCosts(
    DateOnly? PurchaseDate, IReadOnlyList<PurchaseCost> PurchaseCosts, decimal PurchaseTotal, decimal? RunningCosts, double? KmDriven,
    double? Months, decimal? EstimatedResaleValue, DateOnly? ResaleEstimateDate, decimal? NetCost, decimal? NetCostPerKm, decimal? NetCostPerMonth,
    OwnershipFinancing? Financing = null, decimal LumpSumTotal = 0m);

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
            .Select(x => new PurchaseCost(x.e.Id, x.e.Date, x.Amount!.Value, x.e.Note))
            .ToList();
        decimal lumpSums = costs.Sum(x => x.Amount);
        var financing = data.Financing is { } fin ? OwnershipFinancing.From(fin) : null;
        decimal purchaseTotal = lumpSums + (financing?.Total ?? 0m);

        if (data.PurchaseDate is not { } bought)
            return new OwnershipCosts(null, costs, purchaseTotal, null, null, null, data.EstimatedResaleValue, data.ResaleEstimateDate, null, null, null, financing, lumpSums);

        var period = new Period(bought, data.Today.AddDays(1), "Ownership");
        decimal running = Money.For(period, data.Expenses, data.HomeCurrency, null).Total;
        double? km = c.Timeline.KmIn(period);
        double months = Math.Round((period.To.DayNumber - period.From.DayNumber) / DaysPerMonth, 1);
        decimal net = purchaseTotal + running - (data.EstimatedResaleValue ?? 0m);
        decimal? perKm = km is > 0 ? Money.Round(net / (decimal)km.Value) : null;
        decimal? perMonth = months > 0 ? Money.Round(net / (decimal)months) : null;
        return new OwnershipCosts(bought, costs, purchaseTotal, running, km, months, data.EstimatedResaleValue, data.ResaleEstimateDate, net, perKm, perMonth, financing, lumpSums);
    }
}
