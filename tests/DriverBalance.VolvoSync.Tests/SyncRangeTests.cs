using DriverBalance.VolvoSync;

namespace DriverBalance.VolvoSync.Tests;

public class SyncRangeTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    [Fact]
    public void Explicit_from_wins_over_last_trip()
    {
        var r = SyncRange.Compute(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 5), Today);
        Assert.Equal(new DateOnly(2026, 10, 1), r.From);
        Assert.Equal(Today, r.To);
    }

    [Fact]
    public void Starts_two_days_before_last_trip()
    {
        var r = SyncRange.Compute(null, new DateOnly(2026, 11, 5), Today);
        Assert.Equal(new DateOnly(2026, 11, 3), r.From);
        Assert.Equal(Today, r.To);
    }

    [Fact]
    public void Falls_back_to_ninety_days_without_trips()
    {
        var r = SyncRange.Compute(null, null, Today);
        Assert.Equal(Today.AddDays(-90), r.From);
    }

    [Fact]
    public void From_never_passes_today()
    {
        var r = SyncRange.Compute(Today.AddDays(5), null, Today);
        Assert.Equal(Today, r.From);
        Assert.Equal(Today, r.To);
    }
}

public class ExportNamingTests
{
    [Fact]
    public void Export_file_name_contains_range()
        => Assert.Equal("volvo-journal-2026-11-03_2026-11-09.csv",
            ExportNaming.ExportFileName(new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 9)));

    [Fact]
    public void Failure_screenshot_name_is_sortable_timestamp()
        => Assert.Equal("failure-20261109-143005.png", ExportNaming.FailureScreenshotName(new DateTime(2026, 11, 9, 14, 30, 5)));
}

public class OptionsTests
{
    [Fact]
    public void No_arguments_means_sync_with_defaults()
    {
        var o = Options.Parse([]);
        Assert.Equal("sync", o.Command);
        Assert.Equal("http://localhost:5180", o.AppUrl);
        Assert.Equal(1, o.VehicleId);
        Assert.Null(o.From);
        Assert.Null(o.CarId);
        Assert.False(o.Headless);
        Assert.False(o.KeepOpen);
    }

    [Fact]
    public void Parses_command_and_options()
    {
        var o = Options.Parse(["sync", "--app", "http://localhost:9999/", "--vehicle", "3", "--from", "2026-10-01", "--headless", "--keep-open", "--car-id", "abc"]);
        Assert.Equal("sync", o.Command);
        Assert.Equal("http://localhost:9999", o.AppUrl);
        Assert.Equal(3, o.VehicleId);
        Assert.Equal(new DateOnly(2026, 10, 1), o.From);
        Assert.True(o.Headless);
        Assert.True(o.KeepOpen);
        Assert.Equal("abc", o.CarId);
        Assert.Null(o.CodeFile);
    }

    [Fact]
    public void Parses_code_file()
    {
        var o = Options.Parse(["sync", "--code-file", @"C:\tmp\volvo-code.txt"]);
        Assert.Equal(@"C:\tmp\volvo-code.txt", o.CodeFile);
    }

    [Fact]
    public void Reads_a_six_digit_code_from_the_text()
    {
        Assert.Equal("123456", CodeSource.Extract(" 123456\r\n"));
        Assert.Equal("654321", CodeSource.Extract("Your code: 654321"));
        Assert.Null(CodeSource.Extract("12345"));
        Assert.Null(CodeSource.Extract(""));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("set-credentials")]
    [InlineData("clear-credentials")]
    public void Recognises_commands(string command)
        => Assert.Equal(command, Options.Parse([command]).Command);

    [Fact]
    public void Options_before_command_are_fine()
    {
        var o = Options.Parse(["--headless", "login"]);
        Assert.Equal("login", o.Command);
        Assert.True(o.Headless);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("sync", "--nope")]
    [InlineData("sync", "--vehicle", "x")]
    [InlineData("sync", "--from", "yesterday")]
    [InlineData("sync", "--app")]
    [InlineData("sync", "--headed")]
    public void Rejects_bad_input(params string[] args)
        => Assert.Throws<UsageException>(() => Options.Parse(args));

    [Fact]
    public void Help_flag_is_detected()
        => Assert.True(Options.Parse(["--help"]).Help);
}

public class CredentialStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dbvs-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Round_trips_and_is_not_plain_text()
    {
        var path = Path.Combine(_dir, "creds.bin");
        var store = new CredentialStore(path);
        store.Save(new Credentials("me@example.com", "s3cret-Pässword"));

        var raw = File.ReadAllBytes(path);
        Assert.DoesNotContain("s3cret", System.Text.Encoding.UTF8.GetString(raw));
        Assert.DoesNotContain("me@example.com", System.Text.Encoding.UTF8.GetString(raw));

        var loaded = store.Load();
        Assert.Equal(new Credentials("me@example.com", "s3cret-Pässword"), loaded);
    }

    [Fact]
    public void Load_returns_null_when_missing_and_delete_removes()
    {
        var path = Path.Combine(_dir, "creds.bin");
        var store = new CredentialStore(path);
        Assert.Null(store.Load());
        store.Save(new Credentials("a@b.c", "p"));
        Assert.True(store.Delete());
        Assert.Null(store.Load());
        Assert.False(store.Delete());
    }

    [Fact]
    public void ToString_hides_password()
        => Assert.DoesNotContain("pw123", new Credentials("a@b.c", "pw123").ToString());
}

public class RangeChangeTests
{
    private static readonly DateRange R = new(new DateOnly(2026, 10, 9), new DateOnly(2026, 11, 9));

    [Fact]
    public void No_change_needed_when_inputs_already_hold_the_range()
        => Assert.False(SyncRange.NeedsChange("2026-10-09", "2026-11-09", R));

    [Theory]
    [InlineData("2026-10-01", "2026-11-09")]
    [InlineData("2026-10-09", "2026-11-08")]
    [InlineData("", "")]
    public void Change_needed_otherwise(string start, string end)
        => Assert.True(SyncRange.NeedsChange(start, end, R));
}
