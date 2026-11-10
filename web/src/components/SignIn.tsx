import { Link } from 'react-router-dom'
import { signInMessage, signInUrl } from '../api/account'

/** Shown to anyone who is not signed in (hosted mode). The server decides who gets a profile; this only starts Google sign-in. */
export default function SignIn({ code }: { code: string | null }) {
  const message = signInMessage(code)
  return (
    <main className="main signin">
      <div className="card signin-card">
        <h1>DriverBalance</h1>
        <p className="muted">How you drive and what your car costs. Invite-only for now.</p>
        {message && <p role="alert" className="field-error">{message}</p>}
        <p>
          <a className="btn" href={signInUrl(window.location.pathname === '/' ? '/' : window.location.pathname + window.location.search)}>
            Sign in with Google
          </a>
        </p>
        <p className="muted">
          <Link to="/privacy">Privacy</Link>
        </p>
      </div>
    </main>
  )
}
