import type { Class } from './Class'

export interface Recommendation {
  rank: number
  score: number
  reason: string
  class: Class
  generatedAt: string
}
