namespace DriverBalance.Core;

public enum DrivingScore { Excellent = 1, Good = 2, Fair = 3, Bad = 4 }

[Flags]
public enum MaintenanceType
{
    Custom = 0,
    OilChange = 1 << 0,
    BrakeFluid = 1 << 1,
    TyreSwap = 1 << 2,
    BrakePads = 1 << 3,
    TransmissionOil = 1 << 4,
    HaldexOil = 1 << 5,
    Coolant = 1 << 6,
    CabinFilter = 1 << 7,
    AirFilter = 1 << 8,
    FuelFilter = 1 << 9,
    ACCleaning = 1 << 10,
    NewTyres = 1 << 11,
}
