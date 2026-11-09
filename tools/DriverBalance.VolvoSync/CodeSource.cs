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

    public static async Task<string> GetAsync(string? codeFile, TimeSpan wait)
    {
        if (codeFile is null)
        {
            if (Console.IsInputRedirected)
                throw new SyncException("Volvo asked for the e-mailed code, but there is no console to type it into. Use --code-file or run in a terminal.");
            Console.Write("Volvo sent a 6-digit code to your e-mail. Enter it: ");
            return Extract(Console.ReadLine()) ?? throw new SyncException("That is not a 6-digit code.");
        }

        var path = Path.GetFullPath(codeFile);
        if (File.Exists(path)) File.Delete(path); // never reuse an old code
        Console.WriteLine($"Volvo sent a 6-digit code by e-mail. Waiting up to {wait.TotalMinutes:0} minutes for it in {path} ...");
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
