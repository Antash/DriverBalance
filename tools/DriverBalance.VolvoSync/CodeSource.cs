using System.Text.RegularExpressions;

namespace DriverBalance.VolvoSync;

/// <summary>
/// Where the 6-digit code Volvo e-mails at the start of each session comes from: a file the tool waits for
/// (<c>--code-file</c>, so another process can supply it) or the console.
/// </summary>
internal static partial class CodeSource
{
    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex SixDigits();

    /// <summary>The first 6-digit number in the text, or null.</summary>
    public static string? Extract(string? text) => text is null ? null : SixDigits().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>
    /// The line printed while waiting for the code file. Callers (the Telegram bot) recognise it by its start,
    /// "Volvo sent a 6-digit code by e-mail. Waiting", so keep that part unchanged.
    /// </summary>
    internal static string WaitingLine(string path, TimeSpan wait, DateTime notBeforeUtc) =>
        $"Volvo sent a 6-digit code by e-mail. Waiting up to {wait.TotalMinutes:0} minutes for it in {path} ... "
        + $"Use only a Volvo code e-mail received after {notBeforeUtc:yyyy-MM-dd HH:mm:ss} UTC (when this sync started); older codes are rejected.";

    /// <param name="notBeforeUtc">When this sync started: code e-mails from before it belong to earlier runs and no longer work.</param>
    public static async Task<string> GetAsync(string? codeFile, TimeSpan wait, DateTime notBeforeUtc)
    {
        if (codeFile is null)
        {
            if (Console.IsInputRedirected)
                throw new SyncException("Volvo asked for the e-mailed code, but there is no console to type it into. Use --code-file or run in a terminal.");
            Console.Write($"Volvo sent a 6-digit code to your e-mail (use the one received after {notBeforeUtc.ToLocalTime():HH:mm:ss}). Enter it: ");
            return Extract(Console.ReadLine()) ?? throw new SyncException("That is not a 6-digit code.");
        }

        var path = Path.GetFullPath(codeFile);
        if (File.Exists(path)) File.Delete(path); // never reuse an old code
        Console.WriteLine(WaitingLine(path, wait, notBeforeUtc));
        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                string? text = null;
                try { text = await File.ReadAllTextAsync(path); } catch (IOException) { /* still being written */ }
                if (Extract(text) is { } code) { File.Delete(path); return code; }
            }
            await Task.Delay(1000);
        }
        throw new SyncException($"No code appeared in {path} within {wait.TotalMinutes:0} minutes.");
    }
}
