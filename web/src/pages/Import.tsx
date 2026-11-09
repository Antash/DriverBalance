import { useRef, useState, type DragEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { ImportBatch, ImportResult } from '../api/types'
import { daysAgo, errorLines, invalidateVehicleData, fmtDate, fmtInstant } from '../lib/format'
import { useParams } from '../state/ParamsContext'

function CsvCard({ vehicleId }: { vehicleId: number }) {
  const qc = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [over, setOver] = useState(false)
  const upload = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return api.upload<ImportResult>(`/vehicles/${vehicleId}/import/csv`, form)
    },
    onSuccess: () => invalidateVehicleData(qc, vehicleId),
  })
  const pick = (files: FileList | null) => { if (files?.[0]) upload.mutate(files[0]) }
  const r = upload.data

  return (
    <section className="card">
      <h2>Import Volvo trip CSV</h2>
      <div
        className={`dropzone${over ? ' over' : ''}`}
        role="button" tabIndex={0}
        onClick={() => input.current?.click()}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); input.current?.click() } }}
        onDragOver={(e: DragEvent) => { e.preventDefault(); setOver(true) }}
        onDragLeave={() => setOver(false)}
        onDrop={(e: DragEvent) => { e.preventDefault(); setOver(false); pick(e.dataTransfer.files) }}
      >
        {upload.isPending ? 'Importing…' : 'Drop the CSV here, or click to choose a file'}
      </div>
      <input ref={input} type="file" accept=".csv,text/csv" hidden aria-label="CSV file"
        onChange={(e) => { pick(e.target.files); e.target.value = '' }} />
      {upload.isError && <ul className="errors">{errorLines(upload.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      {r && (
        <div role="status">
          <p><strong>{r.imported}</strong> new trips, <strong>{r.skipped}</strong> duplicates skipped.
            {r.firstTrip && r.lastTrip && <> Range {fmtDate(r.firstTrip)} to {fmtDate(r.lastTrip)}.</>}</p>
          {r.warnings.length > 0 && <ul className="warn">{r.warnings.map((w) => <li key={w}>{w}</li>)}</ul>}
        </div>
      )}
    </section>
  )
}

export default function ImportPage() {
  const { vehicleId, saved } = useParams()
  const { data: batches } = useQuery({
    queryKey: ['import-batches', vehicleId],
    queryFn: () => api.get<ImportBatch[]>(`/vehicles/${vehicleId}/import/batches`),
  })
  // Newest trip overall (not newest batch), so trips from earlier imports count too.
  const { data: last } = useQuery({
    queryKey: ['import-batches', vehicleId, 'last-trip'],
    queryFn: () => api.get<{ lastTrip: string | null }>(`/vehicles/${vehicleId}/import/last-trip`),
  })
  const lastTrip = last?.lastTrip ?? undefined
  const age = lastTrip ? daysAgo(lastTrip) : null

  return (
    <section>
      <h1>Import</h1>
      {last && (
        <p className={age != null && age >= saved.exportReminderDays ? 'warn' : 'muted'}>
          {age == null ? 'No trips imported yet.' : `Last trip: ${age} day${age === 1 ? '' : 's'} ago (${fmtDate(lastTrip)}).`}
        </p>
      )}
      <CsvCard vehicleId={vehicleId} />
      <section className="card">
        <h2>Import history</h2>
        <div className="table-wrap">
          <table className="data">
            <thead><tr><th>Imported</th><th>File</th><th className="num">New</th><th className="num">Duplicates</th><th>Trips from</th><th>Trips to</th></tr></thead>
            <tbody>
              {(batches ?? []).map((b) => (
                <tr key={b.id}>
                  <td>{fmtInstant(b.importedAt)}</td><td>{b.fileName}</td>
                  <td className="num">{b.imported}</td><td className="num">{b.skipped}</td>
                  <td>{fmtDate(b.firstTrip)}</td><td>{fmtDate(b.lastTrip)}</td>
                </tr>
              ))}
              {batches?.length === 0 && <tr><td colSpan={6} className="muted">No imports yet.</td></tr>}
            </tbody>
          </table>
        </div>
      </section>
    </section>
  )
}
