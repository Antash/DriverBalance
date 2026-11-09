namespace DriverBalance.Core;

public sealed record VehicleData(
    IReadOnlyList<Trip> Trips,
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<TyreSet> TyreSets,
    string HomeCurrency,
    DateOnly Today,
    long? InitialTyreSetId = null,
    DateOnly? HistoryStart = null,
    int? HistoryStartOdometer = null,
    DateOnly? PurchaseDate = null,
    decimal? EstimatedResaleValue = null,
    DateOnly? ResaleEstimateDate = null,
    Financing? Financing = null);
