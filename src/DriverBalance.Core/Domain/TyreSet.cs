namespace DriverBalance.Core;

public enum TyreSeason { Summer, Winter, AllSeason }

public sealed record TyreSet
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? Size { get; init; }
    public TyreSeason Season { get; init; }
    public string? Description { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public bool Retired { get; init; }
}
