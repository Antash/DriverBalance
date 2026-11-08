namespace DriverBalance.Core;

public enum OdometerSource { TripStart, TripEnd, Expense, Snapshot }

public sealed record OdometerReading(DateTime At, int Km, OdometerSource Source);
