export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  fitnessLevel: string
  goals: string[]
  preferredClassTypes: string[]
  favoriteInstructors: string[]
  segment: string
  createdAt: string
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
