import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { Me } from '../api/account'

async function downloadExport() {
  const res = await fetch('/api/export')
  if (!res.ok) throw new Error(`Export failed (${res.status})`)
  const url = URL.createObjectURL(await res.blob())
  const a = document.createElement('a')
  a.href = url
  a.download = `driverbalance-export-${new Date().toISOString().slice(0, 10)}.json`
  a.click()
  URL.revokeObjectURL(url)
}

export const DELETE_WORD = 'DELETE'

/** Your data: download it, or delete the account with everything in it. */
export default function Account({ me, onDeleted }: { me: Me; onDeleted(): void }) {
  const [confirm, setConfirm] = useState('')
  const exp = useMutation({ mutationFn: downloadExport })
  const del = useMutation({
    mutationFn: () => api.del<void>('/me', { confirm }),
    onSuccess: onDeleted,
  })
  const canDelete = me.mode === 'Hosted' && !me.isAdmin
  const error = del.error instanceof ApiError ? del.error.message : del.error ? 'Could not delete the account.' : null

  return (
    <div>
      <div className="card">
        <h2>Account</h2>
        <p>{me.name}{me.email ? <span className="muted"> · {me.email}</span> : null}</p>
        <p className="muted">See what is stored and why on the <Link to="/privacy">privacy page</Link>.</p>
      </div>
      <div className="card">
        <h2>Export my data</h2>
        <p className="muted">Everything you have added or imported, as one file.</p>
        <button className="btn" onClick={() => exp.mutate()} disabled={exp.isPending}>Export all data</button>
        {exp.isError && <p role="alert" className="field-error">Export failed.</p>}
      </div>
      <div className="card">
        <h2>Delete my account</h2>
        {me.isAdmin ? (
          <p className="muted">The admin account cannot be deleted. You can still export your data.</p>
        ) : !canDelete ? (
          <p className="muted">There is no account to delete when the app runs locally.</p>
        ) : (
          <>
            <p>
              This removes your cars, trips, expenses, documents and API tokens for good. You can only come back if you are invited again.
              Type <b>{DELETE_WORD}</b> to confirm.
            </p>
            <p>
              <input aria-label="Type DELETE to confirm" value={confirm} onChange={(e) => setConfirm(e.target.value)} autoComplete="off" />{' '}
              <button className="btn" disabled={confirm.trim() !== DELETE_WORD || del.isPending} onClick={() => del.mutate()}>Delete my account</button>
            </p>
            {error && <p role="alert" className="field-error">{error}</p>}
          </>
        )}
      </div>
    </div>
  )
}
