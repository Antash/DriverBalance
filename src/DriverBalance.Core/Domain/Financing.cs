namespace DriverBalance.Core;

public enum FinancingFrequency { Monthly = 1, Quarterly = 2, Yearly = 3 }

/// <summary>
/// The car's financing contract, in the home currency. The whole contract counts as a purchase cost:
/// payments x amount + balloon. No interest is modelled and no expenses are generated from it.
/// </summary>
public sealed record Financing(DateOnly Start, decimal AmountPerPayment, FinancingFrequency Frequency, int Payments, decimal Balloon)
{
    public decimal Total => Payments * AmountPerPayment + Balloon;

    public int MonthsPerPayment => Frequency switch { FinancingFrequency.Quarterly => 3, FinancingFrequency.Yearly => 12, _ => 1 };

    /// <summary>Date of the last regular payment; the balloon payment is taken to fall on that date.</summary>
    public DateOnly EndDate => Start.AddMonths((Payments - 1) * MonthsPerPayment);
}
