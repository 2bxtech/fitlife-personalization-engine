import api from './api'
import type { LoginRequest, RegisterRequest, AuthResponse } from '@/types/User'

interface ApiResponse<T> {
  success: boolean
  data: T
  message?: string
}

export const authService = {
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/login', credentials)
    return response.data.data
  },

  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/register', data)
    return response.data.data
  },

  async logout(): Promise<void> {
    // Client-side logout (clear token)
    // Backend is stateless JWT
  },
}
