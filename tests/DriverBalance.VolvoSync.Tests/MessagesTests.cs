using DriverBalance.VolvoSync;

namespace DriverBalance.VolvoSync.Tests;

public class MessagesTests
{
    [Fact]
    public void Waiting_line_keeps_the_prefix_the_bot_matches_and_names_the_cutoff_time()
    {
        var line = CodeSource.WaitingLine(@"C:\x\code.txt", TimeSpan.FromMinutes(5), new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        Assert.StartsWith("Volvo sent a 6-digit code by e-mail. Waiting", line);
        Assert.Contains("received after 2026-01-02 03:04:05 UTC", line);
        Assert.DoesNotContain("\n", line);
    }

    [Fact]
    public void Denied_message_says_it_is_temporary_and_only_mentions_headless_when_headless()
    {
        var headed = VolvoJournal.DeniedMessage(headless: false);
        Assert.Contains("try again in a few minutes", headed);
        Assert.DoesNotContain("headless", headed);
        Assert.Contains("--headless", VolvoJournal.DeniedMessage(headless: true));
    }
}
