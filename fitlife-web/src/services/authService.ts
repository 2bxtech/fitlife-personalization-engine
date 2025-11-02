import api from './api'
import type { LoginRequest, RegisterRequest, AuthResponse } from '@/types/User'

export const authService = {
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/login', credentials)
    return response.data
  },

  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/register', data)
    return response.data
  },

  async logout(): Promise<void> {
    // Client-side logout (clear token)
    // Backend is stateless JWT
  },
}
