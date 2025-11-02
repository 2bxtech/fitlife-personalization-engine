import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recommendationService } from '@/services/recommendationService'
import type { Recommendation } from '@/types/Recommendation'
import type { UserEvent } from '@/types/Event'

export const useRecommendationStore = defineStore('recommendations', () => {
  const recommendations = ref<Recommendation[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchRecommendations(userId: string, limit: number = 10) {
    loading.value = true
    error.value = null
    try {
      recommendations.value = await recommendationService.getRecommendations(userId, limit)
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch recommendations'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function refreshRecommendations(userId: string) {
    loading.value = true
    error.value = null
    try {
      recommendations.value = await recommendationService.refreshRecommendations(userId)
    } catch (e: any) {
      error.value = e.message || 'Failed to refresh recommendations'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function trackEvent(event: UserEvent) {
    try {
      await recommendationService.trackEvent(event)
    } catch (e: any) {
      console.error('Failed to track event:', e)
      // Don't throw - tracking shouldn't break user experience
    }
  }

  return {
    recommendations,
    loading,
    error,
    fetchRecommendations,
    refreshRecommendations,
    trackEvent,
  }
})
