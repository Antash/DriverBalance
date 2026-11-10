/** Who is signed in, and the admin lists. Mirrors MeInfo / InviteInfo / AdminUserInfo on the server. */
export interface Me {
  id: number
  name: string
  email: string | null
  pictureUrl: string | null
  isAdmin: boolean
  /** Local: the laptop setup (no sign-in). Hosted: Google sign-in. */
  mode: 'Local' | 'Hosted'
}

export interface InviteInfo {
  email: string
  createdAt: string
  usedAt: string | null
  usedByUserId: number | null
}

export interface AdminUser {
  id: number
  name: string
  email: string | null
  createdAt: string
  lastSignInAt: string | null
  vehicleCount: number
  isAdmin: boolean
  blocked: boolean
}

/** The server sends people back to "/?signin=<code>" when Google sign-in did not lead to a session. */
export function signInMessage(code: string | null | undefined): string | null {
  switch (code) {
    case 'invite-only': return 'This service is invite-only for now. Ask the owner to invite your Google address.'
    case 'blocked': return 'Your access was removed. Ask the owner if you think this is a mistake.'
    case 'failed': return 'Signing in with Google did not work. Please try again.'
    case 'expired': return 'Your session ended. Please sign in again.'
    default: return null
  }
}

/** Where the sign-in button goes: the server starts Google sign-in and brings the person back to `returnTo` (a same-site path). */
export function signInUrl(returnTo: string): string {
  const safe = returnTo.startsWith('/') && !returnTo.startsWith('//') && !returnTo.startsWith('/\\') ? returnTo : '/'
  return `/auth/login?returnUrl=${encodeURIComponent(safe)}`
}

/** Initials for the avatar when Google gave no picture. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  return (parts.length === 1 ? parts[0].slice(0, 2) : parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}
