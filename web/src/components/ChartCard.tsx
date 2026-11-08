import type { ReactNode } from 'react'

/** Title, loading/error/empty states and the chart itself. `empty` shows the standard "No trips" message. */
export function ChartCard({ title, hint, loading, error, empty, emptyText = 'No trips in this period', actions, children }: {
  title: string
  hint?: string
  loading?: boolean
  error?: boolean
  empty?: boolean
  emptyText?: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <section className="card">
      <div className="card-head">
        <h2>{title}</h2>
        {actions}
      </div>
      {hint && <p className="muted hint">{hint}</p>}
      {error ? <p className="field-error">Could not load this chart.</p>
        : loading ? <p className="muted">Loading…</p>
        : empty ? <p className="muted empty">{emptyText}</p>
        : children}
    </section>
  )
}
