import { useMemo } from 'react'
import type { PeriodWithStats } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import type { MetricKey } from '../lib/compare'
import { smallMultipleOption } from './intervalOptions'

const PANELS: { key: MetricKey; title: string }[] = [
  { key: 'kmPerDay', title: 'Km per day' },
  { key: 'coldStartsPer100Km', title: 'Cold starts per 100 km' },
  { key: 'dpfIndex', title: 'DPF index' },
  { key: 'tripLPer100', title: 'Trip L/100 km' },
  { key: 'costPerKm', title: 'Cost per km' },
]

/** One mini bar chart per metric across every interval of the type (oldest to newest); the current interval is the full-strength bar. */
export function SmallMultiples({ intervals, currency }: { intervals: PeriodWithStats[]; currency: string }) {
  const opts = useMemo(() => PANELS.map((p) => smallMultipleOption(intervals, p.key, currency)), [intervals, currency])
  return (
    <ChartCard title="Every interval at a glance" hint="Oldest to newest; the darkest bar is the current interval. A missing bar means no data.">
      <div className="multiples">
        {PANELS.map((p, i) => (
          <figure key={p.key} className="multiple">
            <figcaption>{p.title}</figcaption>
            <Chart option={opts[i]} height={150} />
          </figure>
        ))}
      </div>
    </ChartCard>
  )
}
