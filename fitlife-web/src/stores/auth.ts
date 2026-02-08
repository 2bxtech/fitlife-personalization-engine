import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authService } from '@/services/authService'
import type { User, LoginRequest, RegisterRequest } from '@/types/User'

function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!))
    return payload.exp * 1000 < Date.now()
  } catch {
    return true
  }
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))
  
  // Safely parse user from localStorage with error handling
  let initialUser: User | null = null
  try {
    const userStr = localStorage.getItem('user')
    if (userStr && userStr !== 'null' && userStr !== 'undefined') {
      initialUser = JSON.parse(userStr)
    }
  } catch (error) {
    console.warn('Failed to parse user from localStorage, clearing...')
    localStorage.removeItem('user')
  }
  const user = ref<User | null>(initialUser)

  const isAuthenticated = computed(() => {
    if (!token.value) return false
    if (isTokenExpired(token.value)) {
      token.value = null
      user.value = null
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      return false
    }
    return true
  })

  async function login(credentials: LoginRequest) {
    const response = await authService.login(credentials)
    token.value = response.token
    user.value = response.user
    localStorage.setItem('token', response.token)
    localStorage.setItem('user', JSON.stringify(response.user))
  }

  async function register(data: RegisterRequest) {
    const response = await authService.register(data)
    token.value = response.token
    user.value = response.user
    localStorage.setItem('token', response.token)
    localStorage.setItem('user', JSON.stringify(response.user))
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem('token')
    localStorage.removeItem('user')
  }

  return {
    token,
    user,
    isAuthenticated,
    login,
    register,
    logout,
  }
})
