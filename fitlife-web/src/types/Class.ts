export interface Class {
  id: string
  name: string
  type: string
  description: string
  instructorId: string
  instructorName: string
  level: string
  startTime: string
  durationMinutes: number
  capacity: number
  currentEnrollment: number
  availableSpots: number
  averageRating: number
  totalRatings: number
  weeklyBookings: number
  isActive: boolean
}

export interface ClassFilter {
  type?: string
  level?: string
  startDate?: string
  page?: number
  pageSize?: number
}
