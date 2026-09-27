import type { Class } from './Class'

/** One scoring rule's contribution to a recommendation, as computed by the API. */
export interface ScoreFactor {
  key: string
  label: string
  points: number
  detail: string
}

export interface Recommendation {
  rank: number
  score: number
  reason: string
  factors: ScoreFactor[]
  class: Class
  generatedAt: string
}
