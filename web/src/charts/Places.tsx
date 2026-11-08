import type { PlaceStat } from '../api/types'
import { ChartCard } from '../components/ChartCard'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, type RangeProps } from './shared'

/** Top places by drives starting or ending there. */
export function Places(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<PlaceStat[]>('charts/places', rangeBody(range))
  return (
    <ChartCard title="Top places" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}>
      <div className="table-wrap">
        <table className="data">
          <thead><tr><th>Place</th><th className="num">Starts</th><th className="num">Ends</th></tr></thead>
          <tbody>
            {data?.map((p) => <tr key={p.address}><td>{p.address}</td><td className="num">{p.starts}</td><td className="num">{p.ends}</td></tr>)}
          </tbody>
        </table>
      </div>
    </ChartCard>
  )
}
