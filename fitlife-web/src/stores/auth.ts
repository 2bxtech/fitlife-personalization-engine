import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authService } from '@/services/authService'
import { demoService } from '@/services/demoService'
import { useRecommendationStore } from './recommendations'
import { useClassStore } from './classes'
import type { User, LoginRequest, RegisterRequest, AuthResponse } from '@/types/User'

function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!))
    return payload.exp * 1000 < Date.now()
  } catch {
    return true
  }
}

function readStoredUser(): User | null {
  try {
    const userStr = localStorage.getItem('user')
    if (userStr && userStr !== 'null' && userStr !== 'undefined') return JSON.parse(userStr)
  } catch {
    console.warn('Failed to parse user from localStorage, clearing...')
  }
  localStorage.removeItem('user')
  return null
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(readStoredUser())
  // A token whose member record is missing or corrupt cannot render any page.
  const token = ref<string | null>(user.value ? localStorage.getItem('token') : null)
  if (!token.value) localStorage.removeItem('token')
  /** Demo persona the session belongs to, or null for a registered member. */
  const personaId = ref<string | null>(localStorage.getItem('personaId'))
  /** Shown once on the next page after a session ends unexpectedly. */
  const sessionNotice = ref<string | null>(null)

  const isAuthenticated = computed(() => {
    if (!token.value) return false
    if (isTokenExpired(token.value)) {
      clearSession()
      return false
    }
    return true
  })

  function applySession(response: AuthResponse, persona: string | null) {
    // A new identity must never see the previous member's data.
    resetMemberData()
    token.value = response.token
    user.value = response.user
    personaId.value = persona
    sessionNotice.value = null
    localStorage.setItem('token', response.token)
    localStorage.setItem('user', JSON.stringify(response.user))
    if (persona) localStorage.setItem('personaId', persona)
    else localStorage.removeItem('personaId')
  }

  async function login(credentials: LoginRequest) {
    applySession(await authService.login(credentials), null)
  }

  async function register(data: RegisterRequest) {
    applySession(await authService.register(data), null)
  }

  /** Signs in as a synthetic persona; the API resets it to its canonical state first. */
  async function startDemoSession(persona: string) {
    applySession(await demoService.startSession(persona), persona)
  }

  function setUser(updated: User) {
    user.value = updated
    localStorage.setItem('user', JSON.stringify(updated))
  }

  function resetMemberData() {
    useRecommendationStore().reset()
    useClassStore().reset()
  }

  function clearSession() {
    token.value = null
    user.value = null
    personaId.value = null
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    localStorage.removeItem('personaId')
  }

  function logout() {
    resetMemberData()
    clearSession()
  }

  /** Ends a session the server rejected or that timed out, explaining why on the next page. */
  function expireSession() {
    const wasDemo = personaId.value !== null
    logout()
    sessionNotice.value = wasDemo
      ? 'Your demo session ended. Pick a persona to continue.'
      : 'Your session expired. Sign in again to continue.'
  }

  function consumeSessionNotice() {
    const notice = sessionNotice.value
    sessionNotice.value = null
    return notice
  }

  return {
    token,
    user,
    personaId,
    sessionNotice,
    isAuthenticated,
    login,
    register,
    startDemoSession,
    setUser,
    logout,
    expireSession,
    consumeSessionNotice,
  }
})
