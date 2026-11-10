import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { initials, type Me } from '../api/account'

/** Avatar, name and sign-out. Hosted mode only: locally there is nobody to sign out. */
export default function AccountMenu({ me, onSignedOut }: { me: Me; onSignedOut(): void }) {
  const out = useMutation({ mutationFn: () => api.post<void>('/logout'), onSuccess: onSignedOut })
  return (
    <span className="account-menu">
      {me.pictureUrl
        ? <img className="avatar" src={me.pictureUrl} alt="" referrerPolicy="no-referrer" width={24} height={24} />
        : <span className="avatar avatar-initials" aria-hidden>{initials(me.name)}</span>}
      <Link to="/account" title={me.email ?? undefined}>{me.name}</Link>
      <button className="btn" onClick={() => out.mutate()} disabled={out.isPending}>Sign out</button>
    </span>
  )
}
