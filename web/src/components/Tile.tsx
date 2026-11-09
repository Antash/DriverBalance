/** A KPI tile: label, big value, and an optional muted line under it. */
export function Tile({ name, value, sub }: { name: string; value: string; sub?: string }) {
  return (
    <div className="kpi">
      <div className="kpi-name">{name}</div>
      <div className="kpi-value">{value}</div>
      {sub && <div className="kpi-sub">{sub}</div>}
    </div>
  )
}
