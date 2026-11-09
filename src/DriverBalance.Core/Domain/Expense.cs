namespace DriverBalance.Core;

public enum ExpenseCategory { Fuel, Parking, TollsVignette, Wash, Maintenance, Repair, Tyres, Accessories, Insurance, RoadTax, Fines, Financing, Other }

public enum ExpenseSource { Web, Mcp, Migration }

public sealed record Expense
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public ExpenseCategory Category { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "CZK";
    public decimal? HomeAmount { get; init; }
    public int? Odometer { get; init; }
    public string? Note { get; init; }
    public DateOnly? CoversFrom { get; init; }
    public DateOnly? CoversTo { get; init; }
    public double? Litres { get; init; }
    public bool? FullTank { get; init; }
    public string? Station { get; init; }
    public string? Place { get; init; }
    public MaintenanceType Maintenance { get; init; }
    public string? MaintenanceDescription { get; init; }
    public IReadOnlyList<MaintenanceWork> Work { get; init; } = Array.Empty<MaintenanceWork>();
    public long? TyreSetId { get; init; }
    public ExpenseSource Source { get; init; }
}
