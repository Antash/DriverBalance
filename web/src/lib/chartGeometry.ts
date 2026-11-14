import type { DrivingScore, GapHistogram, ScoreMapChart } from '../api/types'

type Rect = { score: DrivingScore; x0: number; x1: number; y0: number; y1: number; label?: boolean }

/** The four score zones as disjoint rectangles, derived from the (live) parameters returned with the chart. */
export function zoneRects(z: ScoreMapChart['zones'], xMax: number, yMax: number): Rect[] {
  return [
    // Labels go on the wide strips that reach the right edge: the narrow upright columns are too thin for text.
    { score: 'Fair', x0: z.shortMin, x1: z.mediumMin, y0: z.heavyKmh, y1: yMax },
    { score: 'Fair', x0: z.mediumMin, x1: xMax, y0: z.heavyKmh, y1: z.cityKmh, label: true },
    { score: 'Good', x0: z.mediumMin, x1: z.longMin, y0: z.cityKmh, y1: yMax },
    { score: 'Good', x0: z.longMin, x1: xMax, y0: z.cityKmh, y1: z.mixedKmh, label: true },
    { score: 'Excellent', x0: z.longMin, x1: xMax, y0: z.mixedKmh, y1: yMax, label: true },
  ]
}

/** Position of the threshold on a category axis whose bin i is centred on i (spans i - 0.5 .. i + 0.5). */
export function thresholdPosition(bins: GapHistogram['bins'], minutes: number): number {
  for (let i = 0; i < bins.length; i++) {
    const b = bins[i]
    if (b.to == null) return i
    if (minutes < b.to) return i - 0.5 + Math.max(0, (minutes - b.from) / (b.to - b.from))
  }
  return bins.length - 1
}

