namespace DriverBalance.Core;

/// <summary>Addresses, routes, stations and prices for the demo car. Generic street names in real towns, no real businesses.</summary>
internal static class DemoPlaces
{
    public const string Home = "Hauptstraße 14, 82008 Unterhaching";
    public const string Work = "Sendlinger Straße 21, 80331 München";
    public const string Dealer = "BMW Service München-Süd";
    public const string DealerAddress = "Neuhofener Straße 10, 81379 München";
    public const string TyreShop = "Reifenservice Unterhaching";
    public const string School = "Schulstraße 5, 82008 Unterhaching";
    public const string Supermarket = "Biberger Straße 12, 82008 Unterhaching";

    public sealed record Poi(string Address, double Km);

    public static readonly Poi[] Short =
    [
        new("Biberger Straße 12, 82008 Unterhaching", 2.4),
        new("Gewerbestraße 8, 82041 Oberhaching", 4.8),
        new("Schulstraße 5, 82008 Unterhaching", 3.1),
        new("Lindenstraße 3, 82024 Taufkirchen", 4.2),
        new("Ottobrunner Straße 30, 85521 Ottobrunn", 6.5),
        new("Am Kirchplatz 1, 82024 Taufkirchen", 5.5),
        new("Waldstraße 22, 81739 München", 7.5),
        new("Hauptstraße 62, 82008 Unterhaching", 2.1),
        new("Münchner Straße 18, 82008 Unterhaching", 3.5),
        new("Rosenheimer Straße 40, 81669 München", 8.0),
    ];

    public static readonly Poi[] Outings =
    [
        new("Seestraße 8, 83700 Rottach-Egern", 62),
        new("Uferstraße 3, 83209 Prien am Chiemsee", 85),
        new("Marienplatz 5, 82467 Garmisch-Partenkirchen", 92),
        new("Seelände 2, 83471 Schönau am Königssee", 150),
        new("Mittenwalder Straße 11, 82432 Walchensee", 78),
        new("Am Spitzingsee 4, 83727 Schliersee", 68),
        new("Kemptener Straße 9, 87629 Füssen", 125),
        new("Dorfstraße 6, 83661 Lenggries", 64),
    ];

    /// <summary>A place on a holiday route. Country drives the fuel station brand and price.</summary>
    public sealed record Stop(string Town, string Country, string Address);

    /// <summary>A toll or vignette bought on the way; Fraction is how far into the day's driving it falls.</summary>
    public sealed record Toll(string Note, string Place, decimal BaseAmount, double Fraction);

    public sealed record Journey(string Name, Stop[] Stops, double[] LegKm, string[] LocalPois, Toll[] Out, Toll[] Back, int Days, bool Summer);

    public static readonly Journey Garda = new("Lake Garda",
        [new("Unterhaching", "DE", Home), new("Bozen", "IT", "Via Alto Adige 3, 39100 Bolzano"), new("Lazise", "IT", "Via Roma 12, 37017 Lazise")],
        [305, 170],
        ["Via Verdi 3, 37019 Peschiera del Garda", "Via Roma 20, 37016 Garda", "Via Porto 5, 37011 Bardolino"],
        [new("Austria 10-day vignette", "Kiefersfelden", 11.5m, 0.1), new("Brenner toll", "Brenner", 11.5m, 0.4), new("Italian motorway toll", "Verona Nord", 10m, 0.9)],
        [new("Italian motorway toll", "Verona Nord", 10m, 0.1), new("Brenner toll", "Brenner", 11.5m, 0.6)],
        9, true);

    public static readonly Journey Croatia = new("Croatian coast",
        [new("Unterhaching", "DE", Home), new("Villach", "AT", "Hauptplatz 8, 9500 Villach"), new("Rovinj", "HR", "Ulica Carera 5, 52210 Rovinj")],
        [300, 305],
        ["Trg Kralja 3, 52440 Poreč", "Ulica Pula 9, 52100 Pula", "Obala 12, 52210 Rovinj"],
        [new("Austria 10-day vignette", "Kiefersfelden", 11.5m, 0.1), new("Tauern tunnel toll (A10)", "Flachau", 14m, 0.4),
         new("Slovenia 7-day vignette", "Karawanken", 16m, 0.55), new("Croatian motorway toll", "Istrian Y", 6.5m, 0.95)],
        [new("Croatian motorway toll", "Istrian Y", 6.5m, 0.1), new("Tauern tunnel toll (A10)", "Flachau", 14m, 0.7)],
        9, true);

    public static readonly Journey Hamburg = new("Hamburg",
        [new("Unterhaching", "DE", Home), new("Bamberg", "DE", "Lange Straße 9, 96047 Bamberg"), new("Kassel", "DE", "Königsstraße 33, 34117 Kassel"), new("Hamburg", "DE", "Hafenstraße 12, 20457 Hamburg")],
        [250, 245, 290],
        ["Eppendorfer Weg 5, 20259 Hamburg", "Bahnhofstraße 14, 21029 Hamburg", "Marktplatz 2, 22765 Hamburg"],
        [], [], 5, false);

    public static readonly Journey Cologne = new("Cologne",
        [new("Unterhaching", "DE", Home), new("Würzburg", "DE", "Domstraße 3, 97070 Würzburg"), new("Köln", "DE", "Venloer Straße 40, 50672 Köln")],
        [285, 310],
        ["Hohenzollernring 7, 50672 Köln", "Bonner Straße 21, 50677 Köln", "Rheinufer 3, 50678 Köln"],
        [], [], 5, false);

    /// <summary>Stations near home as (brand, town).</summary>
    public static readonly (string Brand, string Town)[] HomeStations =
    [
        ("Aral", "Unterhaching"), ("Shell", "Oberhaching"), ("JET", "München"), ("Esso", "Taufkirchen"),
        ("Aral", "München"), ("Shell", "Neubiberg"), ("Aral", "Unterhaching"), ("JET", "Ottobrunn"),
    ];

    private static readonly string[] GermanBrands = ["Aral", "Shell", "Esso", "JET"];

    public static string StationBrand(string country, Random rng) => country switch
    {
        "AT" => "OMV",
        "IT" => "Eni",
        "HR" => rng.Next(2) == 0 ? "OMV" : "Shell",
        _ => GermanBrands[rng.Next(GermanBrands.Length)],
    };

    /// <summary>Diesel price per litre (EUR) following the date: 2023 1.70-1.85, 2024 1.62-1.75, 2025 1.58-1.70, 2026 1.60-1.72.</summary>
    public static decimal DieselPrice(DateOnly date, string country, Random rng)
    {
        var (lo, hi) = date.Year switch { <= 2023 => (1.70, 1.85), 2024 => (1.62, 1.75), 2025 => (1.58, 1.70), _ => (1.60, 1.72) };
        var wave = 0.5 + 0.5 * Math.Sin(2 * Math.PI * date.DayOfYear / 365.0 + 1.3);
        var p = Math.Clamp(lo + (hi - lo) * wave + (rng.NextDouble() - 0.5) * 0.03, lo, hi);
        var factor = country switch { "AT" => 0.95, "IT" => 1.05, "HR" => 0.97, _ => 1.0 };
        return Math.Round((decimal)(p * factor), 3);
    }
}
