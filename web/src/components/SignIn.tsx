import { Link } from 'react-router-dom'
import { signInMessage, signInUrl } from '../api/account'
import { LANDING } from '../lib/landing'

/**
 * The start page for anyone who is not signed in (hosted mode): what DriverBalance is, then Google sign-in.
 * The server decides who gets a profile; this only starts the sign-in. index.html has a static copy of this page.
 */
export default function SignIn({ code }: { code: string | null }) {
  const message = signInMessage(code)
  const href = signInUrl(window.location.pathname === '/' ? '/' : window.location.pathname + window.location.search)
  return (
    <div className="landing">
      <header className="landing-top">
        <span className="brand">DriverBalance</span>
        <a className="btn" href={href}>Sign in with Google</a>
      </header>
      <main className="landing-main">
        <section className="landing-hero">
          <h1>{LANDING.title}</h1>
          <p className="landing-intro">{LANDING.intro}</p>
          {message && <p role="alert" className="field-error">{message}</p>}
          <p><a className="btn primary" href={href}>Sign in with Google</a></p>
          <p className="muted">{LANDING.signInNote}</p>
        </section>
        <section className="landing-features">
          {LANDING.features.map((f) => (
            <div className="card" key={f.title}>
              <h2>{f.title}</h2>
              <p>{f.text}</p>
            </div>
          ))}
        </section>
        <section className="card landing-steps">
          <h2>How it works</h2>
          <ol>{LANDING.steps.map((s) => <li key={s}>{s}</li>)}</ol>
        </section>
      </main>
      <footer className="landing-foot muted">
        <p>{LANDING.about}</p>
        <p><Link to="/privacy">Privacy</Link></p>
      </footer>
    </div>
  )
}
