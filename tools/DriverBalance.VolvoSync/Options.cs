using System.Globalization;

namespace DriverBalance.VolvoSync;

internal sealed class UsageException(string message) : Exception(message);

internal sealed record Options
{
    public string Command { get; init; } = "sync";
    public string AppUrl { get; init; } = "http://localhost:5180";
    public long VehicleId { get; init; } = 1;
    public DateOnly? From { get; init; }
    public bool Headless { get; init; }
    public bool KeepOpen { get; init; }
    public string? CarId { get; init; }
    public bool Help { get; init; }

    private static readonly string[] Commands = ["sync", "login", "set-credentials", "clear-credentials"];

    public static Options Parse(string[] args)
    {
        var o = new Options();
        var commandSeen = false;
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Value()
            {
                if (i + 1 >= args.Length) throw new UsageException($"Option {a} needs a value.");
                return args[++i];
            }
            switch (a)
            {
                case "-h" or "--help": o = o with { Help = true }; break;
                case "--app": o = o with { AppUrl = Value().TrimEnd('/') }; break;
                case "--vehicle":
                    var v = Value();
                    if (!long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var vid)) throw new UsageException($"--vehicle must be a number, got '{v}'.");
                    o = o with { VehicleId = vid };
                    break;
                case "--from":
                    var f = Value();
                    if (!DateOnly.TryParseExact(f, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new UsageException($"--from must be yyyy-MM-dd, got '{f}'.");
                    o = o with { From = d };
                    break;
                case "--headless": o = o with { Headless = true }; break;
                case "--keep-open": o = o with { KeepOpen = true }; break;
                case "--car-id": o = o with { CarId = Value() }; break;
                default:
                    if (a.StartsWith('-')) throw new UsageException($"Unknown option {a}.");
                    if (commandSeen || !Commands.Contains(a)) throw new UsageException($"Unknown command '{a}'. Commands: {string.Join(", ", Commands)}.");
                    commandSeen = true;
                    o = o with { Command = a };
                    break;
            }
        }
        return o;
    }

    public const string HelpText = """
        DriverBalance.VolvoSync - export the Volvo journal and import it into DriverBalance.

        Usage: DriverBalance.VolvoSync [command] [options]

        Commands:
          sync (default)      export trips from Volvo (visible Edge window, opens and closes by itself) and import them into the app
          login               sign in to Volvo by hand (stores the browser session)
          set-credentials     store the Volvo e-mail and password (Windows DPAPI, current user)
          clear-credentials   delete the stored credentials

        Options:
          --app <url>         DriverBalance app (default http://localhost:5180)
          --vehicle <id>      vehicle id (default 1)
          --from <yyyy-MM-dd> first day to export (default: last trip - 2 days, else today - 90 days)
          --car-id <id>       Volvo car id, only used if it cannot be found on the account page
          --headless          run sync without a visible window (usually blocked by Volvo's bot protection)
          --keep-open         debug: wait for Enter before closing the browser
          -h, --help          this text
        """;
}
