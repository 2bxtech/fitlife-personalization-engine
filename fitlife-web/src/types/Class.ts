export interface Class {
  id: string
  name: string
  description: string
  type: string
  instructorId: string
  instructorName: string
  difficulty: string
  startTime: string
  endTime: string
  capacity: number
  currentEnrollment: number
  averageRating: number
  location: string
  isActive: boolean
}

export interface ClassFilter {
  type?: string
  difficulty?: string
  instructorId?: string
  startDate?: string
  endDate?: string
  page?: number
  pageSize?: number
}
