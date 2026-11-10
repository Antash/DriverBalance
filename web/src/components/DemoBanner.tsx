import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import type { Vehicle } from '../api/types'
import FirstCar from './FirstCar'
import { Modal } from './Modal'

/** Shown above the pages while the current car is the made-up demo car. */
export default function DemoBanner({ vehicle }: { vehicle: Vehicle }) {
  const qc = useQueryClient()
  const [adding, setAdding] = useState(false)
  const remove = useMutation({
    mutationFn: () => api.del(`/vehicles/${vehicle.id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['vehicles'] }),
  })
  const confirmDelete = () => { if (window.confirm('Delete the demo car and all its data?')) remove.mutate() }
  return (
    <>
      <div className="demo-banner" role="status">
        <span>Demo car with made-up data.</span>
        <span className="spacer" />
        <button className="btn small" onClick={() => setAdding(true)}>Add your real car</button>
        <button className="btn small danger" disabled={remove.isPending} onClick={confirmDelete}>Delete demo car</button>
        {remove.error && <span role="alert" className="field-error">{remove.error instanceof ApiError ? remove.error.message : 'Could not delete the demo car.'}</span>}
      </div>
      {adding && (
        <Modal title="Add your real car" onClose={() => setAdding(false)}>
          <FirstCar allowDemo={false} onDone={() => setAdding(false)} onCancel={() => setAdding(false)} />
        </Modal>
      )}
    </>
  )
}
