namespace DriverBalance.Core;

/// <summary>Normalizes addresses (trim, collapse whitespace, case-insensitive) and remembers the most common spelling.</summary>
internal sealed class AddressSpellings
{
    private readonly Dictionary<string, Dictionary<string, int>> _map = new();

    private static string Collapse(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The normal form of an address: collapsed whitespace, lower case.</summary>
    public static string Normalize(string raw) => Collapse(raw).ToLowerInvariant();

    public string Add(string raw)
    {
        string collapsed = Collapse(raw);
        string key = collapsed.ToLowerInvariant();
        if (!_map.TryGetValue(key, out var d)) _map[key] = d = new Dictionary<string, int>();
        d[collapsed] = d.GetValueOrDefault(collapsed) + 1;
        return key;
    }

    public string Display(string key) => _map[key].MaxBy(kv => kv.Value).Key;
}
