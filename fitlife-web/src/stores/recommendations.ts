import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { recommendationService } from '@/services/recommendationService'
import type { Recommendation } from '@/types/Recommendation'
import type { Class } from '@/types/Class'
import type { UserEvent } from '@/types/Event'

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

const VIEW_FLUSH_MS = 1500
const MAX_BATCH = 100

export const useRecommendationStore = defineStore('recommendations', () => {
  const recommendations = ref<Recommendation[]>([])
  const status = ref<LoadStatus>('idle')
  /** True while a reload runs with results already on screen. */
  const refreshing = ref(false)
  const error = ref<string | null>(null)

  // Each request takes a sequence number; a response is applied only if no newer
  // request started since, so a slow earlier response cannot overwrite a newer one
  // (for example after switching personas).
  let latestRequest = 0

  const loading = computed(() => status.value === 'loading')

  async function load(fetcher: () => Promise<Recommendation[]>) {
    const request = ++latestRequest
    if (recommendations.value.length === 0) status.value = 'loading'
    else refreshing.value = true
    error.value = null
    try {
      const result = await fetcher()
      if (request !== latestRequest) return
      recommendations.value = result
      status.value = 'ready'
    } catch (e: unknown) {
      if (request !== latestRequest) return
      error.value = e instanceof Error ? e.message : 'Failed to load recommendations'
      // Keep showing the last good results; only an empty list becomes an error state.
      status.value = recommendations.value.length === 0 ? 'error' : 'ready'
      throw e
    } finally {
      if (request === latestRequest) refreshing.value = false
    }
  }

  function fetchRecommendations(userId: string, limit = 10) {
    return load(() => recommendationService.getRecommendations(userId, limit))
  }

  function refreshRecommendations(userId: string) {
    return load(() => recommendationService.refreshRecommendations(userId))
  }

  /** Applies an authoritative class update (e.g. from a booking response) to any card showing it. */
  function applyClassUpdate(updated: Class) {
    for (const recommendation of recommendations.value) {
      if (recommendation.class.id === updated.id) recommendation.class = updated
    }
  }

  // View tracking is batched: rendering a page of cards must not fire one request
  // per card (that exceeded the API's per-IP rate limit).
  const pendingViews = new Map<string, UserEvent>()
  // Views being sent right now; a class is marked tracked only once its send succeeds.
  const sendingViews = new Set<string>()
  let flushTimer: ReturnType<typeof setTimeout> | null = null

  function trackView(userId: string, classId: string, source: string) {
    const key = viewKey(userId, classId)
    if (sessionStorage.getItem(key) || pendingViews.has(classId) || sendingViews.has(key)) return
    pendingViews.set(classId, {
      userId,
      itemId: classId,
      itemType: 'Class',
      eventType: 'View',
      metadata: { source },
    })
    flushTimer ??= setTimeout(flushViews, VIEW_FLUSH_MS)
  }

  async function flushViews() {
    flushTimer = null
    const events = [...pendingViews.values()].slice(0, MAX_BATCH)
    events.forEach((event) => pendingViews.delete(event.itemId))
    if (pendingViews.size > 0) flushTimer = setTimeout(flushViews, VIEW_FLUSH_MS)
    if (events.length === 0) return
    const keys = events.map((event) => viewKey(event.userId, event.itemId))
    keys.forEach((key) => sendingViews.add(key))
    try {
      await recommendationService.trackBatchEvents(events)
      keys.forEach((key) => sessionStorage.setItem(key, '1'))
    } catch (e: unknown) {
      // Tracking must never interrupt the member's flow; unsent views may be retried later.
      console.warn('View tracking failed', e)
    } finally {
      keys.forEach((key) => sendingViews.delete(key))
    }
  }

  function viewKey(userId: string, classId: string) {
    return `viewed_${userId}_${classId}`
  }

  async function trackEvent(event: UserEvent) {
    try {
      await recommendationService.trackEvent(event)
    } catch (e: unknown) {
      console.warn('Event tracking failed', e)
    }
  }

  /** Clears member data, e.g. on sign-out or persona switch, and drops in-flight responses. */
  function reset() {
    latestRequest++
    recommendations.value = []
    status.value = 'idle'
    refreshing.value = false
    error.value = null
    pendingViews.clear()
    if (flushTimer) clearTimeout(flushTimer)
    flushTimer = null
  }

  return {
    recommendations,
    status,
    loading,
    refreshing,
    error,
    fetchRecommendations,
    refreshRecommendations,
    applyClassUpdate,
    trackView,
    flushViews,
    trackEvent,
    reset,
  }
})
