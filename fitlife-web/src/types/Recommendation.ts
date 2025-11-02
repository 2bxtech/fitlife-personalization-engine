import type { Class } from './Class'

export interface Recommendation {
  userId: string
  itemId: string
  itemType: string
  score: number
  rank: number
  reason: string
  generatedAt: string
  classDetails?: Class
}
