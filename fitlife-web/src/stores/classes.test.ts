import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useClassStore } from './classes'

// Mock classService
vi.mock('@/services/classService', () => ({
  classService: {
    getClasses: vi.fn(),
    getClassById: vi.fn(),
    bookClass: vi.fn(),
  },
}))

const mockClass = {
  id: 'c1',
  name: 'Yoga Flow',
  type: 'Yoga',
  level: 'Intermediate',
  instructorId: 'i1',
  instructorName: 'Sarah',
  description: 'A relaxing yoga class',
  startTime: '2025-12-01T10:00:00Z',
  durationMinutes: 60,
  capacity: 30,
  currentEnrollment: 15,
  averageRating: 4.5,
  isActive: true,
}

describe('useClassStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('starts with empty state', () => {
    const store = useClassStore()
    expect(store.classes).toEqual([])
    expect(store.currentClass).toBeNull()
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchClasses populates classes array', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.getClasses).mockResolvedValueOnce([mockClass])

    const store = useClassStore()
    await store.fetchClasses()

    expect(store.classes).toHaveLength(1)
    expect(store.classes[0].name).toBe('Yoga Flow')
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchClasses passes filters to service', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.getClasses).mockResolvedValueOnce([])

    const store = useClassStore()
    await store.fetchClasses({ type: 'HIIT', level: 'Advanced' })

    expect(classService.getClasses).toHaveBeenCalledWith({ type: 'HIIT', level: 'Advanced' })
  })

  it('fetchClasses sets error on failure', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.getClasses).mockRejectedValueOnce(new Error('Network error'))

    const store = useClassStore()
    await expect(store.fetchClasses()).rejects.toThrow('Network error')
    expect(store.error).toBe('Network error')
    expect(store.loading).toBe(false)
  })

  it('fetchClassById sets currentClass', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.getClassById).mockResolvedValueOnce(mockClass)

    const store = useClassStore()
    await store.fetchClassById('c1')

    expect(store.currentClass).toEqual(mockClass)
  })

  it('bookClass calls service and refreshes classes', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.bookClass).mockResolvedValueOnce(mockClass)
    vi.mocked(classService.getClasses).mockResolvedValueOnce([mockClass])

    const store = useClassStore()
    await store.bookClass('c1')

    expect(classService.bookClass).toHaveBeenCalledWith('c1')
    expect(classService.getClasses).toHaveBeenCalled() // Refreshes list
  })
})
