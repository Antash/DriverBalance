# Legacy console characterization output

`legacy-output.txt` is the verbatim stdout of the legacy console (`DriverBalance/Program.cs`)
run on `tests/fixtures/synthetic-trips.csv` (119 synthetic trips) with
`tests/fixtures/synthetic-maintenance.json`. Menu and prompt text is kept; Task 3 parses it with regexes.

## How it was produced

Fresh empty temp dir containing `appsettings.json` (copy of `DriverBalance/appsettings.json`) and
`maintenancerecords.json` (copy of `synthetic-maintenance.json`); no `triprecords.json`.
The project was built first (`dotnet build DriverBalance`) so build warnings stay out of stdout.

```
cd <tempdir>
CSV="$(cygpath -w "$REPO/tests/fixtures/synthetic-trips.csv")"   # Windows path, as runprintf '1
%s
3
4
10
11
0
' "$CSV" \
  | dotnet run --no-build --project <repo>/DriverBalance > legacy-output.txt
```

Stdin: `1` + CSV path (import), `3` summary, `4` analyze trips, `10` report since last per type,
`11` all maintenance periods, `0` exit.

## Notes for tests

- Period end of the current interval = run date + 1 (run on 2026-11-08, shows `2026-11-09`); ignore it in tests.
- Number/date culture of the output: machine culture en-US. Decimal separator is `.`, no thousands
  separators, dates like `1/6/2025 9:10:00 AM` (M/d/yyyy h:mm:ss tt). Period lines are `Period: yyyy-MM-dd to yyyy-MM-dd`.
- CSV import uses explicit cs-CZ parsing, independent of machine culture.
- Fixture alignment: no trip occurs on a maintenance date, and no cold-start chain (gap < 45 min)
  spans midnight into a maintenance date (checked by script). Maintenance odometers are the end
  odometer of the last trip before each date: 1864, 3269, 4493, 5148.
- Trip 2025-07-12 22:50 to 07-13 00:25 spans midnight on its own (not a maintenance date).
- Normalisation: the `Imported 119 new records from '...'` line (line 15) originally held the machine's
  absolute CSV path; it was replaced by `<repo>/tests/fixtures/synthetic-trips.csv`. This line is
  machine-specific; ignore it in tests. Everything else is verbatim.
