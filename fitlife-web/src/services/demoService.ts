import api from './api'
import type { AuthResponse } from '@/types/User'
import type { DemoPersona } from '@/types/Demo'

interface ApiResponse<T> {
  success: boolean
  data: T
}

export const demoService = {
  /** Resolves to null when the API has demo mode turned off (404). */
  async listPersonas(): Promise<DemoPersona[] | null> {
    try {
      const response = await api.get<ApiResponse<DemoPersona[]>>('/demo/personas')
      return response.data.data
    } catch (error: unknown) {
      if ((error as { response?: { status?: number } }).response?.status === 404) return null
      throw error
    }
  },

  /** Resets the persona to its canonical state and returns a member session. */
  async startSession(personaId: string): Promise<AuthResponse> {
    const response = await api.post<ApiResponse<AuthResponse>>(`/demo/personas/${personaId}/session`)
    return response.data.data
  },
}
