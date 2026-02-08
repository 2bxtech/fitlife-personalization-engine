import api from './api'
import type { User } from '@/types/User'

interface ApiResponse<T> {
  success: boolean
  data: T
  message?: string
}

export interface UpdatePreferencesRequest {
  fitnessLevel?: string
  goals?: string[]
  preferredClassTypes?: string[]
}

export const userService = {
  async getUser(userId: string): Promise<User> {
    const response = await api.get<ApiResponse<User>>(`/users/${userId}`)
    return response.data.data
  },

  async updatePreferences(userId: string, preferences: UpdatePreferencesRequest): Promise<User> {
    const response = await api.put<ApiResponse<User>>(`/users/${userId}/preferences`, preferences)
    return response.data.data
  },
}
