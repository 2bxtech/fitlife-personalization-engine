import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useRecommendationStore } from './recommendations'

// Mock recommendationService
vi.mock('@/services/recommendationService', () => ({
  recommendationService: {
    getRecommendations: vi.fn(),
    refreshRecommendations: vi.fn(),
    trackEvent: vi.fn(),
    trackBatchEvents: vi.fn(),
  },
}))

const mockRec = {
  score: 85.5,
  rank: 1,
  reason: 'Matches your preferred class type',
  factors: [],
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
    isBookedByCurrentUser: false,
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

  it('ignores a slower earlier response once a newer request has started', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    let resolveFirst!: (value: (typeof mockRec)[]) => void
    vi.mocked(recommendationService.getRecommendations)
      .mockImplementationOnce(() => new Promise((resolve) => (resolveFirst = resolve)))
      .mockResolvedValueOnce([{ ...mockRec, rank: 2, class: { ...mockRec.class, id: 'newer' } }])

    const store = useRecommendationStore()
    const first = store.fetchRecommendations('sarah')
    await store.fetchRecommendations('mike')
    resolveFirst([mockRec])
    await first

    expect(store.recommendations.map((r) => r.class.id)).toEqual(['newer'])
  })

  it('keeps the current results visible when a reload fails', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.getRecommendations)
      .mockResolvedValueOnce([mockRec])
      .mockRejectedValueOnce(new Error('Network Error'))

    const store = useRecommendationStore()
    await store.fetchRecommendations('u1')
    await expect(store.fetchRecommendations('u1')).rejects.toThrow('Network Error')

    expect(store.recommendations).toHaveLength(1)
    expect(store.status).toBe('ready')
    expect(store.refreshing).toBe(false)
  })

  it('only reports an error state when there is nothing to show', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.getRecommendations).mockRejectedValueOnce(new Error('down'))

    const store = useRecommendationStore()
    await expect(store.fetchRecommendations('u1')).rejects.toThrow()

    expect(store.status).toBe('error')
    expect(store.error).toBe('down')
  })

  it('reset drops an in-flight response for the previous member', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    let resolve!: (value: (typeof mockRec)[]) => void
    vi.mocked(recommendationService.getRecommendations).mockImplementationOnce(
      () => new Promise((r) => (resolve = r))
    )

    const store = useRecommendationStore()
    const pending = store.fetchRecommendations('previous-member')
    store.reset()
    resolve([mockRec])
    await pending

    expect(store.recommendations).toEqual([])
    expect(store.status).toBe('idle')
  })

  it('batches card views into one request and dedupes them per session', async () => {
    vi.useFakeTimers()
    sessionStorage.clear()
    const { recommendationService } = await import('@/services/recommendationService')
    const store = useRecommendationStore()

    for (const id of ['a', 'b', 'c', 'a']) store.trackView('u1', id, 'browse')
    await vi.runAllTimersAsync()

    expect(recommendationService.trackBatchEvents).toHaveBeenCalledTimes(1)
    const sent = vi.mocked(recommendationService.trackBatchEvents).mock.calls[0]![0]
    expect(sent.map((event) => event.itemId)).toEqual(['a', 'b', 'c'])
    expect(recommendationService.trackEvent).not.toHaveBeenCalled()
    vi.useRealTimers()
  })

  it('applies an authoritative class update to the card showing it', async () => {
    const { recommendationService } = await import('@/services/recommendationService')
    vi.mocked(recommendationService.getRecommendations).mockResolvedValueOnce([mockRec])
    const store = useRecommendationStore()
    await store.fetchRecommendations('u1')

    store.applyClassUpdate({ ...mockRec.class, isBookedByCurrentUser: true, currentEnrollment: 16 })

    expect(store.recommendations[0]!.class.isBookedByCurrentUser).toBe(true)
    expect(store.recommendations[0]!.class.currentEnrollment).toBe(16)
  })
})
