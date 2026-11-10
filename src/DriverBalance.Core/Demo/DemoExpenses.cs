namespace DriverBalance.Core;

/// <summary>
/// The scheduled costs of the demo car: purchase, yearly insurance and road tax, services, brake work, wipers and the seasonal
/// tyre swaps. Fuel, parking, washes, AdBlue and tolls come from the driving itself (<see cref="DemoTripSim"/>).
/// </summary>
internal sealed class DemoCosts
{
    public const string SummerKey = "summer";
    public const string WinterKey = "winter";
    public const string Summer2Key = "summer2";

    private const string Size = "225/45 R18";

    private readonly DemoTripSim _sim;
    private readonly Random _rng;
    private readonly DateOnly _purchase;
    private readonly DateOnly _today;
    private readonly List<DemoExpense> _expenses;

    private int _anniversary; // next purchase anniversary (0 = the purchase day itself) still to book
    private DateOnly _lastService;
    private int _lastServiceOdo = DemoData.StartOdometer;
    private double _kmTrigger;
    private int _serviceCount;
    private DateOnly _fluidDue;
    private readonly DateOnly _huDue;
    private readonly DateOnly _newTyresDate;
    private bool _huDone;
    private readonly int _padsAtKm;
    private bool _padsDone, _wipersDone, _newTyresDone, _onWinter, _winterBought;
    private readonly List<(DateOnly Date, bool ToWinter)> _swaps = [];
    private readonly List<DemoTyreSet> _tyreSets = [];

    public IReadOnlyList<DemoTyreSet> TyreSets => _tyreSets;

    public DemoCosts(DemoTripSim sim, Random rng, DateOnly purchase, DateOnly today, List<DemoExpense> expenses)
    {
        _sim = sim;
        _rng = rng;
        _purchase = purchase;
        _today = today;
        _expenses = expenses;
        _lastService = purchase;
        _fluidDue = purchase.AddMonths(36);
        _huDue = purchase.AddMonths(36);
        _kmTrigger = 23500 + rng.Next(0, 1000);
        _padsAtKm = rng.Next(45000, 52000);
        _tyreSets.Add(new DemoTyreSet(SummerKey, "Summer tyres (Michelin Primacy 4)", "Michelin", "Primacy 4", Size, TyreSeason.Summer, purchase, false));
        for (var y = purchase.Year; y <= today.Year; y++)
        {
            _swaps.Add((NextSaturday(new DateOnly(y, 4, 15)), false));
            _swaps.Add((NextSaturday(new DateOnly(y, 10, 25)), true));
        }
        _newTyresDate = LastSpringSwapDone();
    }

    public void OnDay(DateOnly d)
    {
        if (d == _purchase)
        {
            _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Purchase, 58900m, DemoData.StartOdometer, "Car purchase price", place: DemoPlaces.Dealer));
            _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Purchase, 180m, DemoData.StartOdometer, "Registration and plates", place: DemoPlaces.Dealer));
        }

        YearlyCosts(d);
        TyreSwap(d);

        var workday = d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && !_sim.IsPublicHoliday(d) && !_sim.InTravelBlock(d);
        if (!workday) return;

        if ((!_huDone && d >= _huDue) || d >= _fluidDue) Inspection(d);
        else if (!_padsDone && (_sim.Odometer >= _padsAtKm || d >= _purchase.AddMonths(40))) Pads(d);
        else if (!_wipersDone && d >= _purchase.AddMonths(20)) Wipers(d);
        else if ((d.DayNumber - _lastService.DayNumber) >= 355 || _sim.Odometer - _lastServiceOdo >= _kmTrigger) Service(d);
    }

    private decimal Money(double lo, double hi) => Math.Round((decimal)(lo + (hi - lo) * _rng.NextDouble()), 2);

    private void YearlyCosts(DateOnly d)
    {
        var due = _purchase.AddYears(_anniversary);
        if (d != due) return;
        var to = due.AddYears(1).AddDays(-1);
        var odo = d == _purchase ? DemoData.StartOdometer : _sim.Odometer;
        var insurance = Math.Round(905m - 35m * _anniversary + (decimal)(_rng.NextDouble() * 16 - 8), 2);
        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Insurance, insurance, odo, "Liability + fully comprehensive (Haftpflicht + Vollkasko)",
            coversFrom: due, coversTo: to));
        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.TollsVignette, 232m, odo, "Kfz-Steuer", coversFrom: due, coversTo: to));
        _anniversary++;
    }

    // ---- maintenance -------------------------------------------------------------------------------------------

    private static MaintenanceWork Part(MaintenanceType type, string? details = null, WorkPosition? position = null) =>
        new() { Type = type, Part = PartSource.Original, Details = details, Position = position };

    private void Service(DateOnly d)
    {
        _serviceCount++;
        var work = new List<MaintenanceWork> { Part(MaintenanceType.OilChange, "0W-20"), Part(MaintenanceType.OilFilter) };
        var flags = MaintenanceType.OilChange | MaintenanceType.OilFilter;
        var notes = new List<string> { "BMW service: engine oil and filter" };
        var amount = Money(320, 380);

        if (_serviceCount % 2 == 0)
        {
            work.Add(Part(MaintenanceType.AirFilter));
            work.Add(Part(MaintenanceType.CabinFilter));
            flags |= MaintenanceType.AirFilter | MaintenanceType.CabinFilter;
            notes.Add("air and cabin filter");
            amount = Money(420, 480);
        }

        amount += DateDriven(d, work, ref flags, notes);

        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, amount, _sim.Odometer, string.Join(", ", notes),
            place: DemoPlaces.Dealer, flags: flags, work: work));
        _lastService = d;
        _lastServiceOdo = _sim.Odometer;
        _kmTrigger = 23500 + _rng.Next(0, 1000);
    }

    /// <summary>Brake fluid (3 years, then every 2) and HU/AU (36 months): added to a service within 6 weeks of being due.</summary>
    private decimal DateDriven(DateOnly d, List<MaintenanceWork> work, ref MaintenanceType flags, List<string> notes)
    {
        var amount = 0m;
        if (d >= _fluidDue.AddDays(-42))
        {
            work.Add(Part(MaintenanceType.BrakeFluid));
            flags |= MaintenanceType.BrakeFluid;
            notes.Add("brake fluid");
            amount += Money(115, 125);
            _fluidDue = d.AddMonths(24);
        }
        if (!_huDone && d >= _huDue.AddDays(-42))
        {
            work.Add(new MaintenanceWork { Name = "HU/AU (TÜV)", Part = PartSource.None });
            notes.Add("HU/AU (TÜV)");
            amount += Money(135, 145);
            _huDone = true;
        }
        return amount;
    }

    /// <summary>A visit of its own for HU/AU and brake fluid when no service fell close enough to the due date.</summary>
    private void Inspection(DateOnly d)
    {
        var work = new List<MaintenanceWork>();
        var flags = MaintenanceType.Custom;
        var notes = new List<string>();
        var amount = DateDriven(d, work, ref flags, notes);
        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, amount, _sim.Odometer, string.Join(", ", notes),
            place: DemoPlaces.Dealer, flags: flags, work: work));
    }

    private void Pads(DateOnly d)
    {
        _padsDone = true;
        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, Money(520, 580), _sim.Odometer, "Front brake pads and discs",
            place: DemoPlaces.Dealer, flags: MaintenanceType.BrakePads | MaintenanceType.BrakeDiscs,
            work: [Part(MaintenanceType.BrakePads, position: WorkPosition.Front), Part(MaintenanceType.BrakeDiscs, position: WorkPosition.Front)]));
    }

    private void Wipers(DateOnly d)
    {
        _wipersDone = true;
        _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, Money(40, 50), _sim.Odometer, "Front wiper blades",
            place: DemoPlaces.Dealer, flags: MaintenanceType.WiperBladesFront, work: [Part(MaintenanceType.WiperBladesFront)]));
    }

    // ---- tyres -------------------------------------------------------------------------------------------------

    private static DateOnly NextSaturday(DateOnly d) => d.AddDays(((int)DayOfWeek.Saturday - (int)d.DayOfWeek + 7) % 7);

    private void TyreSwap(DateOnly d)
    {
        foreach (var (date, toWinter) in _swaps)
        {
            if (date != d || d <= _purchase.AddDays(14) || toWinter == _onWinter) continue;

            if (toWinter)
            {
                if (!_winterBought)
                {
                    _winterBought = true;
                    _tyreSets.Add(new DemoTyreSet(WinterKey, "Winter tyres + rims (Continental WinterContact TS 870)", "Continental", "WinterContact TS 870", Size, TyreSeason.Winter, d, false));
                    _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Tyres, 1450m, _sim.Odometer, "Winter tyres with rims (Continental WinterContact TS 870)",
                        place: DemoPlaces.TyreShop, tyreSetKey: WinterKey));
                }
                _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, Swap(), _sim.Odometer, "Seasonal tyre change: winter tyres",
                    place: DemoPlaces.TyreShop, flags: MaintenanceType.TyreSwap, tyreSetKey: WinterKey));
                _onWinter = true;
                continue;
            }

            if (!_newTyresDone && d == _newTyresDate)
            {
                _newTyresDone = true;
                _tyreSets[0] = _tyreSets[0] with { Retired = true };
                _tyreSets.Add(new DemoTyreSet(Summer2Key, "Summer tyres 2 (Michelin Primacy 4)", "Michelin", "Primacy 4", Size, TyreSeason.Summer, d, false));
                _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, Money(770, 790), _sim.Odometer, "New summer tyres (Michelin Primacy 4) fitted",
                    place: DemoPlaces.TyreShop, flags: MaintenanceType.TyreSwap | MaintenanceType.NewTyres, tyreSetKey: Summer2Key));
            }
            else
            {
                _expenses.Add(DemoTripSim.Cost(d, ExpenseCategory.Maintenance, Swap(), _sim.Odometer, "Seasonal tyre change: summer tyres",
                    place: DemoPlaces.TyreShop, flags: MaintenanceType.TyreSwap, tyreSetKey: _newTyresDone ? Summer2Key : SummerKey));
            }
            _onWinter = false;
        }
    }

    /// <summary>The last spring swap to summer tyres that actually happens up to today: that is when the new summer set goes on.</summary>
    private DateOnly LastSpringSwapDone()
    {
        var onWinter = false;
        DateOnly last = default;
        foreach (var (date, toWinter) in _swaps)
        {
            if (date <= _purchase.AddDays(14) || date > _today || toWinter == onWinter) continue;
            onWinter = toWinter;
            if (!toWinter) last = date;
        }
        return last;
    }

    private decimal Swap() => Math.Round(Money(60, 80) / 5m) * 5m;
}
