import type { DrivingScore } from '../api/types'

/** Date range (to exclusive) every trips chart reads; both undefined means all data. */
export interface RangeProps { from?: string; to?: string }
/** Charts whose points identify a trip open the detail drawer through this. */
export interface TripClickProps { onTrip?(tripId: number): void }

export const SCORES: DrivingScore[] = ['Excellent', 'Good', 'Fair', 'Bad']
export const rangeBody = ({ from, to }: RangeProps) => ({ from, to })
