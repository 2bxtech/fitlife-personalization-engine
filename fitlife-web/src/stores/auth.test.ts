import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from './auth'

// Mock authService
vi.mock('@/services/authService', () => ({
  authService: {
    login: vi.fn(),
    register: vi.fn(),
  },
}))

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key]
    }),
    clear: vi.fn(() => {
      store = {}
    }),
  }
})()
Object.defineProperty(globalThis, 'localStorage', { value: localStorageMock })

function createJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const body = btoa(JSON.stringify(payload))
  return `${header}.${body}.fakesignature`
}

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorageMock.clear()
    vi.clearAllMocks()
  })

  it('starts unauthenticated when no token in localStorage', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
  })

  it('login stores token and user', async () => {
    const { authService } = await import('@/services/authService')
    const mockUser = {
      id: 'u1',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      fitnessLevel: 'Beginner',
      goals: [],
      preferredClassTypes: [],
      segment: null,
      createdAt: '2025-01-01',
    }
    const mockToken = createJwt({ sub: 'u1', exp: Math.floor(Date.now() / 1000) + 3600 })

    vi.mocked(authService.login).mockResolvedValueOnce({
      token: mockToken,
      user: mockUser,
    })

    const store = useAuthStore()
    await store.login({ email: 'test@example.com', password: 'pass' })

    expect(store.token).toBe(mockToken)
    expect(store.user).toEqual(mockUser)
    expect(store.isAuthenticated).toBe(true)
    expect(localStorageMock.setItem).toHaveBeenCalledWith('token', mockToken)
    expect(localStorageMock.setItem).toHaveBeenCalledWith('user', JSON.stringify(mockUser))
  })

  it('logout clears token and user', async () => {
    const { authService } = await import('@/services/authService')
    const mockToken = createJwt({ sub: 'u1', exp: Math.floor(Date.now() / 1000) + 3600 })
    vi.mocked(authService.login).mockResolvedValueOnce({
      token: mockToken,
      user: { id: 'u1', email: 'a@b.com', firstName: 'A', lastName: 'B', fitnessLevel: 'Beginner', goals: [], preferredClassTypes: [], segment: null, createdAt: '' },
    })

    const store = useAuthStore()
    await store.login({ email: 'a@b.com', password: 'pass' })
    expect(store.isAuthenticated).toBe(true)

    store.logout()
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(localStorageMock.removeItem).toHaveBeenCalledWith('token')
    expect(localStorageMock.removeItem).toHaveBeenCalledWith('user')
  })

  it('isAuthenticated returns false for expired token', async () => {
    const { authService } = await import('@/services/authService')
    const expiredToken = createJwt({ sub: 'u1', exp: Math.floor(Date.now() / 1000) - 60 })
    vi.mocked(authService.login).mockResolvedValueOnce({
      token: expiredToken,
      user: { id: 'u1', email: 'a@b.com', firstName: 'A', lastName: 'B', fitnessLevel: 'Beginner', goals: [], preferredClassTypes: [], segment: null, createdAt: '' },
    })

    const store = useAuthStore()
    await store.login({ email: 'a@b.com', password: 'pass' })

    // Token is set but expired — isAuthenticated should auto-clean
    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(localStorageMock.removeItem).toHaveBeenCalledWith('token')
  })

  it('register stores token and user', async () => {
    const { authService } = await import('@/services/authService')
    const mockToken = createJwt({ sub: 'u2', exp: Math.floor(Date.now() / 1000) + 3600 })
    const mockUser = {
      id: 'u2',
      email: 'new@example.com',
      firstName: 'New',
      lastName: 'User',
      fitnessLevel: 'Intermediate',
      goals: ['Lose weight'],
      preferredClassTypes: ['Yoga'],
      segment: 'Beginner',
      createdAt: '2025-01-01',
    }

    vi.mocked(authService.register).mockResolvedValueOnce({
      token: mockToken,
      user: mockUser,
    })

    const store = useAuthStore()
    await store.register({
      email: 'new@example.com',
      password: 'pass',
      firstName: 'New',
      lastName: 'User',
      fitnessLevel: 'Intermediate',
    })

    expect(store.isAuthenticated).toBe(true)
    expect(store.user?.email).toBe('new@example.com')
  })
})
