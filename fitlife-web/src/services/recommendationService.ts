import api from './api'
import type { Recommendation } from '@/types/Recommendation'
import type { UserEvent, BatchEventsRequest } from '@/types/Event'

export const recommendationService = {
  async getRecommendations(userId: string, limit: number = 10): Promise<Recommendation[]> {
    const response = await api.get<{ success: boolean, data: Recommendation[] }>(
      `/recommendations/${userId}`,
      { params: { limit } }
    )
    return response.data.data
  },

  async refreshRecommendations(userId: string): Promise<Recommendation[]> {
    const response = await api.post<{ success: boolean, data: Recommendation[] }>(
      `/recommendations/${userId}/refresh`
    )
    return response.data.data
  },

  async trackEvent(event: UserEvent): Promise<void> {
    await api.post('/events', event)
  },

  async trackBatchEvents(events: UserEvent[]): Promise<void> {
    await api.post<BatchEventsRequest>('/events/batch', { events })
  },
}
