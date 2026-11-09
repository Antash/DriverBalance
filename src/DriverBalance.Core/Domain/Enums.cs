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
    OilFilter = 1 << 12,
    BrakeDiscs = 1 << 13,
    WheelAlignment = 1 << 14,
    TimingBelt = 1 << 15,
    AuxiliaryBelt = 1 << 16,
    GlowPlugs = 1 << 17,
    DpfService = 1 << 18,
    Battery = 1 << 19,
    WiperBladesFront = 1 << 20,
    WiperBladesRear = 1 << 21,
    Suspension = 1 << 22,
}
