import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { ExpenseDocument } from '../api/types'
import { errorLines, fmtBytes, invalidateVehicleData } from '../lib/format'

export const DOCUMENT_ACCEPT = 'application/pdf,image/jpeg,image/png,image/webp,image/heic,.pdf,.jpg,.jpeg,.png,.webp,.heic'

export function DocumentList({ vehicleId, documents, pending, onRemoved, onRemovePending }: {
  vehicleId: number | string; documents: ExpenseDocument[]; pending: File[]
  onRemoved(id: number): void; onRemovePending(index: number): void
}) {
  const qc = useQueryClient()
  const remove = useMutation({
    mutationFn: (id: number) => api.del<void>(`/vehicles/${vehicleId}/documents/${id}`),
    onSuccess: async (_r, id) => { onRemoved(id); await invalidateVehicleData(qc, Number(vehicleId)) },
  })
  if (documents.length === 0 && pending.length === 0) return null
  return (
    <div className="wide">
      <ul className="doc-list">
        {documents.map((d) => (
          <li key={d.id} className="row">
            <a href={`/api/vehicles/${vehicleId}/documents/${d.id}`} target="_blank" rel="noopener">{d.fileName}</a>
            <span className="muted">{fmtBytes(d.sizeBytes)}</span>
            <button type="button" className="btn small danger" disabled={remove.isPending}
              onClick={() => { if (window.confirm(`Remove ${d.fileName}?`)) remove.mutate(d.id) }}>Remove</button>
          </li>
        ))}
        {pending.map((f, i) => (
          <li key={`${f.name}-${i}`} className="row">
            <span>{f.name}</span>
            <span className="muted">{fmtBytes(f.size)} · not uploaded yet</span>
            <button type="button" className="btn small" aria-label={`Remove ${f.name} from the list`} onClick={() => onRemovePending(i)}>Remove</button>
          </li>
        ))}
      </ul>
      {remove.isError && <ul className="errors">{errorLines(remove.error).map((m) => <li key={m}>{m}</li>)}</ul>}
    </div>
  )
}
