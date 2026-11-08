using DriverBalance.Core;

namespace DriverBalance.Core.Tests;

public class VolvoCsvParserTests
{
    private const string Header = "Category,Start,Start odometer,Start address,End,End odometer,End address,Duration,Distance,Fuel,Notes";

    private static ParseResult ParseRows(params string[] rows) =>
        VolvoCsvParser.Parse(new StringReader(string.Join("\n", new[] { Header }.Concat(rows))));

    private static string Row(string duration = "45 min", string startOdo = "7 350") =>
        $"\"Private\",\"14.04.2025 11:45\",\"{startOdo}\",\"Street 1, Praha\",\"14.04.2025 12:30\",\"7 378\",\"Other 2, Brno\",\"{duration}\",\"28,4\",\"2,1\",\"\"";

    [Fact]
    public void Parse_TypicalRow()
    {
        var result = ParseRows(Row());

        var trip = Assert.Single(result.Trips);
        Assert.Empty(result.Warnings);
        Assert.Equal("Private", trip.Category);
        Assert.Equal(new DateTime(2025, 4, 14, 11, 45, 0), trip.Start);
        Assert.Equal(new DateTime(2025, 4, 14, 12, 30, 0), trip.End);
        Assert.Equal(7350, trip.StartOdometer);
        Assert.Equal(7378, trip.EndOdometer);
        Assert.Equal("Street 1, Praha", trip.StartAddress);
        Assert.Equal("Other 2, Brno", trip.EndAddress);
        Assert.Equal(TimeSpan.FromMinutes(45), trip.Duration);
        Assert.Equal(28.4, trip.DistanceKm);
        Assert.Equal(2.1, trip.FuelLiters);
        Assert.Equal("", trip.Notes);
    }

    [Fact]
    public void Parse_HoursAndMinutes()
    {
        var trip = Assert.Single(ParseRows(Row("1 h, 5 min")).Trips);
        Assert.Equal(TimeSpan.FromMinutes(65), trip.Duration);
    }

    [Fact]
    public void Parse_ZeroMinutes()
    {
        var trip = Assert.Single(ParseRows(Row("0 min")).Trips);
        Assert.Equal(TimeSpan.Zero, trip.Duration);
        Assert.Equal(0, trip.AverageSpeedKmh);
    }

    [Fact]
    public void Parse_NbspThousands()
    {
        var trip = Assert.Single(ParseRows(Row(startOdo: "12 345")).Trips);
        Assert.Equal(12345, trip.StartOdometer);
    }

    [Fact]
    public void Parse_BomCrlfBlankLines()
    {
        var text = "﻿" + Header + "\r\n\r\n" + Row() + "\r\n\r\n" + Row("1 h, 5 min") + "\r\n";
        var result = VolvoCsvParser.Parse(new StringReader(text));

        Assert.Equal(2, result.Trips.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_ShortRow_SkippedWithWarning()
    {
        var result = ParseRows("\"Private\",\"14.04.2025 11:45\",\"7 350\"", Row());

        Assert.Single(result.Trips);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Parse_SyntheticFixture_ParsesAllRowsWithoutWarnings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic-trips.csv");
        using var reader = new StreamReader(path);
        var result = VolvoCsvParser.Parse(reader);

        Assert.Empty(result.Warnings);
        Assert.InRange(result.Trips.Count, 100, 140);
        Assert.Equal(2, result.Trips.Count(t => t.Duration == TimeSpan.Zero));
    }

    [Fact]
    public void Parse_BadDateRow_SkippedWithWarning()
    {
        var bad = Row().Replace("14.04.2025 11:45", "not a date");
        var result = ParseRows(Row(), bad);

        Assert.Single(result.Trips);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("99999999999 min")]
    [InlineData("2147483647 h")]
    [InlineData("99999999999 h")]
    public void Parse_OverflowDuration_SkippedWithWarning(string duration)
    {
        var result = ParseRows(Row(), Row(duration));

        Assert.Single(result.Trips);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Validate_Defaults_Valid() =>
        Assert.Empty(new AnalysisParameters().Validate());

    [Fact]
    public void Validate_ShortAboveMedium_Invalid() =>
        Assert.NotEmpty((new AnalysisParameters { ShortDurationMinutes = 31 }).Validate());

    [Fact]
    public void Validate_CityAboveMixed_Invalid() =>
        Assert.NotEmpty((new AnalysisParameters { CitySpeedKmh = 51 }).Validate());

    [Fact]
    public void Validate_GapZero_Invalid() =>
        Assert.NotEmpty((new AnalysisParameters { ColdStartGapMinutes = 0 }).Validate());

    [Theory]
    [InlineData(0, false)]
    [InlineData(1.5, false)]
    [InlineData(1, true)]
    public void Validate_SpanOutOfRange_Invalid(double span, bool valid) =>
        Assert.Equal(valid, new AnalysisParameters { LowessSpan = span }.Validate().Count == 0);
}
