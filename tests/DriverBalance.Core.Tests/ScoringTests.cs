namespace DriverBalance.Core.Tests;

public class ScoringTests
{
    private static readonly AnalysisParameters P = new();

    [Theory]
    [InlineData(40, 50, DrivingScore.Excellent)]
    [InlineData(39, 60, DrivingScore.Good)]
    [InlineData(40, 49.9, DrivingScore.Good)]
    [InlineData(30, 35, DrivingScore.Good)]
    [InlineData(29, 60, DrivingScore.Fair)]
    [InlineData(20, 25, DrivingScore.Fair)]
    [InlineData(30, 34.9, DrivingScore.Fair)]
    [InlineData(19, 100, DrivingScore.Bad)]
    [InlineData(60, 24.9, DrivingScore.Bad)]
    public void Score_AtThresholds(int minutes, double speed, DrivingScore expected) =>
        Assert.Equal(expected, Scoring.Score(TimeSpan.FromMinutes(minutes), speed, P));

    private static Trip T(DateTime start, int minutes, double km) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, 0, "");

    [Fact]
    public void BestForGroup_ShortTripsTogetherReachExcellent()
    {
        var d = new DateTime(2025, 1, 1, 8, 0, 0);
        // 3 x 13.33 min => ~40 min total at 55 km/h; each single trip is Bad.
        var minutes = 40.0 / 3;
        double kmEach = 55 * (minutes / 60);
        var trips = new List<Trip>();
        for (int i = 0; i < 3; i++)
        {
            var start = d.AddMinutes(i * 20);
            trips.Add(new Trip("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), kmEach, 0, ""));
        }
        Assert.Equal(DrivingScore.Bad, Scoring.Score(trips[0].Duration, trips[0].AverageSpeedKmh, P));
        Assert.Equal(DrivingScore.Excellent, Scoring.BestForGroup(trips, P));
    }

    [Fact]
    public void BestForGroup_UsesBestSubRun()
    {
        var d = new DateTime(2025, 1, 1, 8, 0, 0);
        var trips = new List<Trip>
        {
            T(d, 10, 2),                 // slow short
            T(d.AddMinutes(15), 45, 60), // 80 km/h, 45 min => Excellent alone
        };
        Assert.Equal(DrivingScore.Excellent, Scoring.BestForGroup(trips, P));
    }
}
