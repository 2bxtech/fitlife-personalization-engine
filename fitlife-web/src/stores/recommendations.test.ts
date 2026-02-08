import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useRecommendationStore } from './recommendations'

// Mock recommendationService
vi.mock('@/services/recommendationService', () => ({
  recommendationService: {
    getRecommendations: vi.fn(),
    refreshRecommendations: vi.fn(),
    trackEvent: vi.fn(),
  },
}))

const mockRec = {
  score: 85.5,
  rank: 1,
  reason: 'Matches your preferred class type',
  generatedAt: '2025-12-01T09:00:00Z',
  class: {
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
  },
}

describe('useRecommendationStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('starts with empty state', () => {
    const store = useRecommendationStore()
    expect(store.recommendations).toEqual([])
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetchRecommendations populates list', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.getRecommendations).mockResolvedValueOnce([mockRec])

    const store = useRecommendationStore()
    await store.fetchRecommendations('u1')

    expect(store.recommendations).toHaveLength(1)
    expect(store.recommendations[0]!.class.name).toBe('Yoga Flow')
    expect(store.loading).toBe(false)
  })

  it('fetchRecommendations passes limit', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.getRecommendations).mockResolvedValueOnce([])

    const store = useRecommendationStore()
    await store.fetchRecommendations('u1', 5)

    expect(recommendationService.getRecommendations).toHaveBeenCalledWith('u1', 5)
  })

  it('refreshRecommendations updates list', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.refreshRecommendations).mockResolvedValueOnce([mockRec])

    const store = useRecommendationStore()
    await store.refreshRecommendations('u1')

    expect(store.recommendations).toHaveLength(1)
  })

  it('trackEvent does not throw on failure', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.trackEvent).mockRejectedValueOnce(new Error('fail'))

    const store = useRecommendationStore()
    // Should not throw — tracking errors are silently caught
    await store.trackEvent({ userId: 'u1', itemId: 'c1', itemType: 'class', eventType: 'View', timestamp: new Date().toISOString() })

    expect(store.error).toBeNull()
  })
})
