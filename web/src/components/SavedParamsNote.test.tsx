import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const state = { dirty: false }
vi.mock('../state/ParamsContext', () => ({ useParams: () => state }))
import { SavedParamsNote } from './SavedParamsNote'

afterEach(cleanup)

describe('SavedParamsNote', () => {
  it('shows only while the parameters are dirty', () => {
    state.dirty = false
    const { rerender } = render(<SavedParamsNote />)
    expect(screen.queryByRole('note')).toBeNull()
    state.dirty = true
    rerender(<SavedParamsNote />)
    expect(screen.getByRole('note').textContent).toContain('Grouping uses saved parameters')
  })
})
