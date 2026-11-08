/** Helpers so a data refresh (parameter tweak, new import) does not throw away the user's zoom. */

type Json = Record<string, unknown>
interface ZoomState { start?: number; end?: number }

const xOf = (point: unknown): unknown => {
  if (Array.isArray(point)) return point[0]
  if (point && typeof point === 'object') {
    const v = (point as { value?: unknown }).value
    return Array.isArray(v) ? v[0] : undefined
  }
  return undefined
}

/**
 * Identifies the "shape" of a chart: series names/types plus the time extent of the data.
 * Same key = same chart with refreshed numbers (keep zoom); different key = new range or series (reset zoom).
 */
export function structureKey(option: Json): string {
  const series = (Array.isArray(option.series) ? option.series : []) as Json[]
  let min: string | undefined, max: string | undefined
  let categories = 0 // plain-number series sit on a category axis: their length is the extent
  for (const s of series) {
    for (const p of (Array.isArray(s.data) ? s.data : []) as unknown[]) {
      if (typeof p === 'number') categories++
      const x = xOf(p)
      if (typeof x !== 'string') continue
      if (min === undefined || x < min) min = x
      if (max === undefined || x > max) max = x
    }
  }
  return JSON.stringify([series.map((s) => [s.name ?? null, s.type ?? null]), min ?? null, max ?? null, categories])
}

/** Copies the current dataZoom window (percentages) onto the new option's dataZoom components, by index. */
export function carryZoom(next: Json, current: unknown): Json {
  const nextZoom = next.dataZoom
  if (!Array.isArray(nextZoom) || !Array.isArray(current)) return next
  const merged = nextZoom.map((z: Json, i: number) => {
    const cur = current[i] as ZoomState | undefined
    if (!cur || cur.start == null || cur.end == null) return z
    const { startValue: _s, endValue: _e, ...rest } = z
    return { ...rest, start: cur.start, end: cur.end }
  })
  return { ...next, dataZoom: merged }
}
