import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { BrowserRouter, Link, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { api, ApiError, setUnauthorizedHandler } from './api/client'
import type { Me } from './api/account'
import type { Vehicle } from './api/types'
import AccountMenu from './components/AccountMenu'
import FirstCar from './components/FirstCar'
import { ParamsPanel } from './components/ParamsPanel'
import SignIn from './components/SignIn'
import { ParamsProvider, useParams } from './state/ParamsContext'
import Account from './pages/Account'
import Admin from './pages/Admin'
import Expenses from './pages/Expenses'
import Import from './pages/Import'
import Intervals from './pages/Intervals'
import Maintenance from './pages/Maintenance'
import Overview from './pages/Overview'
import Privacy from './pages/Privacy'
import Settings from './pages/Settings'
import Statistics from './pages/Statistics'
import Trips from './pages/Trips'

const NAV: [string, string][] = [
  ['/', 'Overview'], ['/trips', 'Trips'], ['/statistics', 'Statistics'], ['/intervals', 'Oil intervals'], ['/maintenance', 'Maintenance'],
  ['/expenses', 'Expenses'], ['/import', 'Import'], ['/settings', 'Settings'],
]

const STORE_KEY = 'driverbalance.vehicleId'
function storedVehicleId(): number | null {
  try { const v = localStorage.getItem(STORE_KEY); return v ? Number(v) : null } catch { return null }
}

/** Back to the start page with a clean slate (cached data of the person who just left must not linger). */
const restart = () => window.location.assign('/')

function Shell({ me, vehicles, vehicleId, setVehicleId }: {
  me: Me; vehicles: Vehicle[]; vehicleId: number; setVehicleId(id: number): void
}) {
  const { dirty } = useParams()
  const [open, setOpen] = useState(false)
  const hosted = me.mode === 'Hosted'
  const admin = hosted && me.isAdmin
  return (
    <>
      <header className="header">
        <div className="header-row">
          <span className="brand">DriverBalance</span>
          {vehicles.length > 1 && (
            <select aria-label="Vehicle" value={vehicleId} onChange={(e) => setVehicleId(Number(e.target.value))}>
              {vehicles.map((v) => <option key={v.id} value={v.id}>{v.name}</option>)}
            </select>
          )}
          <span className="spacer" />
          {dirty && <span className="badge">Unsaved — trying changes</span>}
          <button className="btn" onClick={() => setOpen(true)}>Parameters</button>
          {hosted && <AccountMenu me={me} onSignedOut={restart} />}
        </div>
        <nav className="nav">
          {NAV.map(([to, label]) => <NavLink key={to} to={to} end={to === '/'}>{label}</NavLink>)}
          {admin && <NavLink to="/admin">Admin</NavLink>}
        </nav>
      </header>
      <main className="main">
        <Routes>
          <Route path="/" element={<Overview />} />
          <Route path="/trips" element={<Trips />} />
          <Route path="/statistics" element={<Statistics />} />
          <Route path="/intervals" element={<Intervals />} />
          <Route path="/maintenance" element={<Maintenance />} />
          <Route path="/tyres" element={<Navigate to="/maintenance" replace />} />
          <Route path="/expenses" element={<Expenses />} />
          <Route path="/import" element={<Import />} />
          <Route path="/settings" element={<Settings />} />
          <Route path="/account" element={<Account me={me} onDeleted={restart} />} />
          {admin && <Route path="/admin" element={<Admin />} />}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        {hosted && <footer className="footer muted"><Link to="/privacy">Privacy</Link></footer>}
      </main>
      {open && <ParamsPanel onClose={() => setOpen(false)} />}
    </>
  )
}

/** Signed in: the car data and the app itself. */
function Authed({ me }: { me: Me }) {
  const { data: vehicles, isError } = useQuery({ queryKey: ['vehicles'], queryFn: () => api.get<Vehicle[]>('/vehicles') })
  const [chosen, setChosen] = useState<number | null>(storedVehicleId)
  const current = vehicles?.find((v) => v.id === chosen) ?? vehicles?.find((v) => v.isDefault) ?? vehicles?.[0]

  useEffect(() => {
    if (current) try { localStorage.setItem(STORE_KEY, String(current.id)) } catch { /* ignore */ }
  }, [current])

  if (isError) return <p className="pad">Could not reach the API.</p>
  if (!vehicles) return <p className="muted pad">Loading…</p>
  if (!current) return me.mode === 'Hosted' ? <FirstCar /> : <p className="pad">No vehicles yet.</p>

  return (
    <ParamsProvider key={current.id} vehicleId={current.id}>
      <Shell me={me} vehicles={vehicles} vehicleId={current.id} setVehicleId={setChosen} />
    </ParamsProvider>
  )
}

/** Who is this? Hosted and not signed in: the sign-in page. Locally the server always answers, so nothing changes. */
function Gate() {
  const qc = useQueryClient()
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.get<Me>('/me'), retry: false })

  // A session that ends while the app is open: ask again who we are (the answer is 401 and the sign-in page appears).
  useEffect(() => {
    setUnauthorizedHandler(() => { void qc.invalidateQueries({ queryKey: ['me'] }) })
    return () => setUnauthorizedHandler(undefined)
  }, [qc])

  if (me.isPending) return <p className="muted pad">Loading…</p>
  if (me.error instanceof ApiError && me.error.status === 401)
    return <SignIn code={new URLSearchParams(window.location.search).get('signin')} />
  if (me.isError) return <p className="pad">Could not reach the API.</p>
  return <Authed me={me.data} />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/privacy" element={<Privacy />} />
        <Route path="*" element={<Gate />} />
      </Routes>
    </BrowserRouter>
  )
}
