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

    [Theory]
    [InlineData("00:29 h", 29)]
    [InlineData("01:05 h", 65)]
    [InlineData("12:00 h", 720)]
    [InlineData("00:00 h", 0)]
    [InlineData("01:05", 65)]
    public void Parse_ClockStyleDuration(string duration, int minutes)
    {
        var result = ParseRows(Row(duration));

        var trip = Assert.Single(result.Trips);
        Assert.Empty(result.Warnings);
        Assert.Equal(TimeSpan.FromMinutes(minutes), trip.Duration);
    }

    [Fact]
    public void Parse_ClockFallBackMidTrip_AddsTheHourAndMovesStartToNewClock()
    {
        // Started 13:43 on summer time, stopped 13:02 on winter time: really 19 minutes.
        var row = "Unassigned,2026-11-01 13:43,100,\"A\",2026-11-01 13:02,119,\"B\",00:-41 h,19.1,1.2,\"\"";
        var result = ParseRows(row);

        var trip = Assert.Single(result.Trips);
        Assert.Equal(TimeSpan.FromMinutes(19), trip.Duration);
        Assert.Equal(new DateTime(2026, 11, 1, 13, 2, 0), trip.End);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 43, 0), trip.Start);
        Assert.Contains("clock change", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Parse_NegativeDurationOverAnHour_KeptAsZeroWithWarning()
    {
        var result = ParseRows(Row("-01:30 h"));

        var trip = Assert.Single(result.Trips);
        Assert.Equal(TimeSpan.Zero, trip.Duration);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Parse_NewExportRow_IsoDatesAndDotDecimals()
    {
        var row = "Unassigned,2026-01-19 14:52,40265,\"Street 1, Praha 4\",2026-01-19 15:22,40280,\"Other 2, Praha 9\",00:29 h,16.1,0.9,\"\"";
        var result = ParseRows(row);

        var trip = Assert.Single(result.Trips);
        Assert.Empty(result.Warnings);
        Assert.Equal(new DateTime(2026, 1, 19, 14, 52, 0), trip.Start);
        Assert.Equal(40265, trip.StartOdometer);
        Assert.Equal(TimeSpan.FromMinutes(29), trip.Duration);
        Assert.Equal(16.1, trip.DistanceKm);
        Assert.Equal(0.9, trip.FuelLiters);
    }

    [Fact]
    public void Parse_SemicolonExport_RussianHeader()
    {
        var text = "Категория;Начало;Начальные показания (км);Начальный адрес;Окончание;Конечные показания (км);Конечный адрес;Длительность;Расстояние (км);Расход топлива (литры);Заметки пользователя\n"
                 + "Без обозначения;2026-01-14 13:02;100;\"Street 1, Praha 4\";2026-01-14 13:31;112;\"Other 2; Praha 4\";00:29 h;11,4;1,3;\"\"";
        var result = VolvoCsvParser.Parse(new StringReader(text));

        var trip = Assert.Single(result.Trips);
        Assert.Empty(result.Warnings);
        Assert.Equal("Без обозначения", trip.Category);
        Assert.Equal(new DateTime(2026, 1, 14, 13, 2, 0), trip.Start);
        Assert.Equal("Street 1, Praha 4", trip.StartAddress);
        Assert.Equal("Other 2; Praha 4", trip.EndAddress);
        Assert.Equal(TimeSpan.FromMinutes(29), trip.Duration);
        Assert.Equal(11.4, trip.DistanceKm);
        Assert.Equal(1.3, trip.FuelLiters);
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

    [Theory]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("NaN")]
    [InlineData("1e999")]
    public void Parse_NonFiniteNumber_SkippedWithWarning(string bad)
    {
        var result = ParseRows(Row(), Row().Replace("\"28,4\"", $"\"{bad}\""), Row().Replace("\"2,1\"", $"\"{bad}\""));

        Assert.Single(result.Trips);
        Assert.Equal(2, result.Warnings.Count);
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
