import { describe, expect, it } from 'vitest'
import { KM_STEPS } from './kmSteps'

describe('KM_STEPS', () => {
  it('lists the slider steps, 0 meaning no filter', () => {
    expect(KM_STEPS).toEqual([0, 5, 10, 20, 50, 100, 200, 500])
  })
})
