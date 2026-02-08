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

// Request interceptor: Add JWT token (skip if expired)
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      if (isTokenExpired(authStore.token)) {
        authStore.logout()
        router.push('/login')
        return Promise.reject(new axios.Cancel('Token expired'))
      }
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor: Handle 401 (logout)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const authStore = useAuthStore()
      authStore.logout()
      router.push('/login')
    }
    return Promise.reject(error)
  }
)

export default api
