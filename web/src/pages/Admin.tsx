import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import type { AdminUser, InviteInfo } from '../api/account'

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString() : '—')
const errorText = (e: unknown) => (e instanceof ApiError ? e.message : e ? 'Something went wrong.' : null)

/** Admin only: who may sign in, and who has a profile. */
export default function Admin() {
  const qc = useQueryClient()
  const [email, setEmail] = useState('')
  const invites = useQuery({ queryKey: ['admin-invites'], queryFn: () => api.get<InviteInfo[]>('/admin/invites') })
  const users = useQuery({ queryKey: ['admin-users'], queryFn: () => api.get<AdminUser[]>('/admin/users') })
  const refresh = () => Promise.all([qc.invalidateQueries({ queryKey: ['admin-invites'] }), qc.invalidateQueries({ queryKey: ['admin-users'] })])

  const add = useMutation({ mutationFn: () => api.post<InviteInfo>('/admin/invites', { email }), onSuccess: async () => { setEmail(''); await refresh() } })
  const removeInvite = useMutation({
    mutationFn: (i: InviteInfo) => api.del<void>(`/admin/invites/${encodeURIComponent(i.email)}`),
    onSuccess: refresh,
  })
  const removeUser = useMutation({ mutationFn: (id: number) => api.del<void>(`/admin/users/${id}`), onSuccess: refresh })

  return (
    <div>
      <div className="card">
        <h2>Invites</h2>
        <p className="muted">Only these Google addresses can create a profile. Tell people the link yourself.</p>
        <form onSubmit={(e) => { e.preventDefault(); if (email.trim()) add.mutate() }}>
          <input aria-label="Email to invite" type="email" placeholder="name@gmail.com" value={email} onChange={(e) => setEmail(e.target.value)} />{' '}
          <button className="btn" disabled={!email.trim() || add.isPending}>Invite</button>
        </form>
        {errorText(add.error) && <p role="alert" className="field-error">{errorText(add.error)}</p>}
        {errorText(removeInvite.error) && <p role="alert" className="field-error">{errorText(removeInvite.error)}</p>}
        <table className="data">
          <thead><tr><th>Email</th><th>Invited</th><th>Status</th><th /></tr></thead>
          <tbody>
            {(invites.data ?? []).map((i) => (
              <tr key={i.email}>
                <td>{i.email}</td>
                <td>{when(i.createdAt)}</td>
                <td>{i.usedAt ? `Signed up ${when(i.usedAt)}` : 'Not used yet'}</td>
                <td>
                  <button className="btn" onClick={() => {
                    const used = i.usedAt !== null
                    if (!used || window.confirm(`${i.email} has a profile. Removing the invite signs them out and blocks them; their data stays. Continue?`)) removeInvite.mutate(i)
                  }}>{i.usedAt ? 'Block' : 'Remove'}</button>
                </td>
              </tr>
            ))}
            {invites.data?.length === 0 && <tr><td colSpan={4} className="muted">No invites yet.</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h2>Users</h2>
        {errorText(removeUser.error) && <p role="alert" className="field-error">{errorText(removeUser.error)}</p>}
        <table className="data">
          <thead><tr><th>Name</th><th>Email</th><th>Created</th><th>Last sign-in</th><th>Cars</th><th /></tr></thead>
          <tbody>
            {(users.data ?? []).map((u) => (
              <tr key={u.id}>
                <td>{u.name}{u.isAdmin ? ' (admin)' : ''}{u.blocked ? ' (blocked)' : ''}</td>
                <td>{u.email ?? '—'}</td>
                <td>{when(u.createdAt)}</td>
                <td>{when(u.lastSignInAt)}</td>
                <td>{u.vehicleCount}</td>
                <td>
                  {!u.isAdmin && (
                    <button className="btn" onClick={() => {
                      if (window.confirm(`Delete ${u.name} and ALL their data (cars, trips, expenses, documents)? This cannot be undone.`)) removeUser.mutate(u.id)
                    }}>Delete</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
