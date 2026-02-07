export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  fitnessLevel: string
  goals: string[]
  preferredClassTypes: string[]
  segment: string | null
  createdAt: string
  updatedAt?: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  fitnessLevel: string
  goals?: string[]
  preferredClassTypes?: string[]
}

export interface AuthResponse {
  token: string
  user: User
}
