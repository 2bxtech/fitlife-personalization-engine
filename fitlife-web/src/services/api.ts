import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import { router } from '@/router'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!))
    return payload.exp * 1000 < Date.now()
  } catch {
    return true
  }
}

/** Ends the session with an explanation and a way back to where the member was. */
function endSession() {
  const authStore = useAuthStore()
  const wasDemo = authStore.personaId !== null
  authStore.expireSession()
  const current = router.currentRoute.value
  if (wasDemo) void router.push('/')
  else void router.push({ path: '/login', query: { redirect: current.fullPath } })
}

// Request interceptor: attach the JWT; end an expired session before sending.
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      if (isTokenExpired(authStore.token)) {
        endSession()
        return Promise.reject(new axios.Cancel('Session expired'))
      }
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor: the server rejected the credentials of an active session.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && useAuthStore().token) endSession()
    return Promise.reject(error)
  }
)

export default api
