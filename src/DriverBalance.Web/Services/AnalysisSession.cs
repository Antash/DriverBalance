using DriverBalance.Core;
using DriverBalance.Data;

namespace DriverBalance.Web;

/// <summary>A vehicle's data analysed with the effective parameters (saved ones, or a validated unsaved override).</summary>
public sealed record AnalysisSession(VehicleRow Vehicle, VehicleData Data, AnalysisParameters Parameters, AnalysisContext Context)
{
    public DateOnly Today => Data.Today;

    /// <summary>Earliest of the car-history start, first trip and first expense, if any exist.</summary>
    public DateOnly? DataStart
    {
        get
        {
            var start = DataStartWithoutHistory;
            return Data.HistoryStart is { } h && (start is null || h < start) ? h : start;
        }
    }

    /// <summary>The odometer at <see cref="DataStart"/> when that is the car-history start; otherwise unknown.</summary>
    public int? DataStartOdometer =>
        Data.HistoryStart is { } h && h == DataStart ? Data.HistoryStartOdometer ?? 0 : null;

    private DateOnly? DataStartWithoutHistory
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
