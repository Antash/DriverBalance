import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BrowserRouter, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { api } from './api/client'
import type { Vehicle } from './api/types'
import { ParamsPanel } from './components/ParamsPanel'
import { ParamsProvider, useParams } from './state/ParamsContext'
import Expenses from './pages/Expenses'
import Import from './pages/Import'
import Intervals from './pages/Intervals'
import Maintenance from './pages/Maintenance'
import Overview from './pages/Overview'
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

function Shell({ vehicles, vehicleId, setVehicleId }: {
  vehicles: Vehicle[]; vehicleId: number; setVehicleId(id: number): void
}) {
  const { dirty } = useParams()
  const [open, setOpen] = useState(false)
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
        </div>
        <nav className="nav">
          {NAV.map(([to, label]) => <NavLink key={to} to={to} end={to === '/'}>{label}</NavLink>)}
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
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      {open && <ParamsPanel onClose={() => setOpen(false)} />}
    </>
  )
}

export default function App() {
  const { data: vehicles, isError } = useQuery({ queryKey: ['vehicles'], queryFn: () => api.get<Vehicle[]>('/vehicles') })
  const [chosen, setChosen] = useState<number | null>(storedVehicleId)
  const current = vehicles?.find((v) => v.id === chosen) ?? vehicles?.find((v) => v.isDefault) ?? vehicles?.[0]

  useEffect(() => {
    if (current) try { localStorage.setItem(STORE_KEY, String(current.id)) } catch { /* ignore */ }
  }, [current])

  if (isError) return <p className="pad">Could not reach the API.</p>
  if (!vehicles) return <p className="muted pad">Loading…</p>
  if (!current) return <p className="pad">No vehicles yet.</p>

  return (
    <BrowserRouter>
      <ParamsProvider key={current.id} vehicleId={current.id}>
        <Shell vehicles={vehicles} vehicleId={current.id} setVehicleId={setChosen} />
      </ParamsProvider>
    </BrowserRouter>
  )
}
