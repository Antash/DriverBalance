/**
 * A "focus" scale for L/100 km: the band where most points sit (3rd..90th percentile, rounded out to whole litres) is
 * linear and takes most of the axis; values outside it are squashed logarithmically, so a 25 L/100 cold start no longer
 * flattens the 5–9 L/100 cloud. The chart plots `toAxis(v)` on a plain value axis and labels ticks with real litres.
 */
export interface FuelScale {
  lo: number
  hi: number
  toAxis(v: number): number
  min: number
  max: number
  /** Axis positions of the labelled ticks, with their real values. */
  ticks: { at: number; value: number }[]
}

/** Share of the band's height given to each log step (e-fold) beyond it; 0.3 makes 10 -> 30 L about a third of the band. */
const TAIL = 0.3
const NICE_TAIL = [0.5, 1, 2, 3, 15, 20, 30, 40, 50, 75, 100]

function quantile(sorted: number[], q: number): number {
  const i = (sorted.length - 1) * q
  const a = Math.floor(i)
  const b = Math.ceil(i)
  return sorted[a] + (sorted[b] - sorted[a]) * (i - a)
}

export function fuelScale(values: number[]): FuelScale {
  const vs = values.filter((v) => Number.isFinite(v) && v > 0).sort((a, b) => a - b)
  let lo = vs.length ? Math.floor(quantile(vs, 0.03)) : 4
  let hi = vs.length ? Math.ceil(quantile(vs, 0.9)) : 10
  if (hi - lo < 2) { lo = Math.max(0, lo - 1); hi = lo + Math.max(2, hi - lo + 1) }
  lo = Math.max(lo, 0.5)
  const span = hi - lo
  const toAxis = (v: number) =>
    v >= lo && v <= hi ? (v - lo) / span
      : v > hi ? 1 + TAIL * Math.log(v / hi)
      : -TAIL * Math.log(lo / Math.max(v, 0.01))

  const dataMin = vs.length ? Math.min(vs[0], lo) : lo
  const dataMax = vs.length ? Math.max(vs[vs.length - 1], hi) : hi
  const min = toAxis(dataMin) - 0.03
  const max = toAxis(dataMax) + 0.03

  const step = span <= 8 ? 1 : 2
  const ticks: FuelScale['ticks'] = []
  for (let v = lo; v <= hi + 1e-9; v += step) ticks.push({ at: toAxis(v), value: v })
  if (ticks[ticks.length - 1].value !== hi) ticks.push({ at: 1, value: hi })
  // Outside the band: a few round values, never closer than 12 % of the band to a neighbour.
  for (const v of NICE_TAIL) {
    if ((v > hi && v <= dataMax) || (v < lo && v >= dataMin)) {
      const at = toAxis(v)
      if (ticks.every((t) => Math.abs(t.at - at) > 0.12)) ticks.push({ at, value: v })
    }
  }
  ticks.sort((a, b) => a.at - b.at)
  return { lo, hi, toAxis, min, max, ticks }
}
