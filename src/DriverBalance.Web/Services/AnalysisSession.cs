using DriverBalance.Core;
using DriverBalance.Data;

namespace DriverBalance.Web;

/// <summary>A vehicle's data analysed with the effective parameters (saved ones, or a validated unsaved override).</summary>
public sealed record AnalysisSession(VehicleRow Vehicle, VehicleData Data, AnalysisParameters Parameters, AnalysisContext Context)
{
    public DateOnly Today => Data.Today;

    /// <summary>Earliest first-trip or first-expense date, if the vehicle has any data.</summary>
    public DateOnly? DataStart
    {
        get
        {
            DateOnly? first = Data.Trips.Count > 0 ? DateOnly.FromDateTime(Data.Trips.Min(t => t.Start)) : null;
            DateOnly? firstExpense = Data.Expenses.Count > 0 ? Data.Expenses.Min(e => e.Date) : null;
            if (first is null) return firstExpense;
            return firstExpense is { } e && e < first ? e : first;
        }
    }

    /// <summary>Missing bounds mean all data: data start (or today) up to and including today.</summary>
    public Period Range(DateOnly? from, DateOnly? to, string label = "All time")
    {
        var period = new Period(from ?? DataStart ?? Today, to ?? Today.AddDays(1), label);
        if (period.To < period.From) throw new ApiValidationException("'from' must not be after 'to'.");
        return period;
    }
}

public sealed class AnalysisSessionFactory(VehicleDataLoader loader)
{
    public async Task<AnalysisSession> OpenAsync(long vid, AnalysisParameters? overrideParameters, CancellationToken ct)
    {
        var (row, data, saved) = await loader.LoadAsync(vid, ct); // 404 comes before parameter validation
        var effective = saved;
        if (overrideParameters is not null)
        {
            var errors = overrideParameters.Validate();
            if (errors.Count > 0) throw new ApiValidationException(errors);
            effective = overrideParameters;
        }
        return new AnalysisSession(row, data, effective, AnalysisContext.Create(data, effective));
    }
}
