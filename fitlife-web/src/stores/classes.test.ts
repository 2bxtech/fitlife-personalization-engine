import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useClassStore } from './classes'

// Mock classService
vi.mock('@/services/classService', () => ({
  classService: {
    getClasses: vi.fn(),
    getClassById: vi.fn(),
    bookClass: vi.fn(),
    cancelBooking: vi.fn(),
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
  availableSpots: 15,
  averageRating: 4.5,
  totalRatings: 42,
  weeklyBookings: 25,
  isActive: true,
  isBookedByCurrentUser: false,
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
    expect(store.classes[0]!.name).toBe('Yoga Flow')
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

  it('bookClass updates the affected class from the response', async () => {
    const { classService } = await import('@/services/classService')
    const bookedClass = {
      ...mockClass,
      currentEnrollment: 16,
      availableSpots: 14,
      isBookedByCurrentUser: true,
    }
    vi.mocked(classService.bookClass).mockResolvedValueOnce({
      classData: bookedClass,
      message: 'Class booked successfully',
    })

    const store = useClassStore()
    store.classes = [mockClass]
    const message = await store.bookClass('c1')

    expect(classService.bookClass).toHaveBeenCalledWith('c1')
    expect(store.classes[0]).toEqual(bookedClass)
    expect(message).toBe('Class booked successfully')
    expect(store.actionClassId).toBeNull()
  })

  it('cancelBooking updates booking state without refetching the list', async () => {
    const { classService } = await import('@/services/classService')
    const bookedClass = {
      ...mockClass,
      currentEnrollment: 16,
      availableSpots: 14,
      isBookedByCurrentUser: true,
    }
    const cancelledClass = {
      ...mockClass,
      isBookedByCurrentUser: false,
    }
    vi.mocked(classService.cancelBooking).mockResolvedValueOnce({
      classData: cancelledClass,
      message: 'Booking cancelled successfully',
    })

    const store = useClassStore()
    store.classes = [bookedClass]
    const message = await store.cancelBooking('c1')

    expect(classService.cancelBooking).toHaveBeenCalledWith('c1')
    expect(store.classes[0]).toEqual(cancelledClass)
    expect(message).toBe('Booking cancelled successfully')
    expect(classService.getClasses).not.toHaveBeenCalled()
  })

  it('bookClass surfaces the API domain message', async () => {
    const { classService } = await import('@/services/classService')
    vi.mocked(classService.bookClass).mockRejectedValueOnce({
      isAxiosError: true,
      response: { data: { message: 'Class is full' } },
    })

    const store = useClassStore()

    await expect(store.bookClass('c1')).rejects.toThrow('Class is full')
    expect(store.error).toBe('Class is full')
    expect(store.actionClassId).toBeNull()
  })
})
