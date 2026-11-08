namespace DriverBalance.Core;

/// <summary>Half-open date range [From, To).</summary>
public sealed record Period(DateOnly From, DateOnly To, string Label, int? StartOdometer = null, int? EndOdometer = null)
{
    public int Days => To.DayNumber - From.DayNumber;

    public bool Contains(DateOnly d) => d >= From && d < To;
}
