<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useClassStore } from '@/stores/classes'
import { useToast } from '@/composables/useToast'
import { useRecommendationStore } from '@/stores/recommendations'
import RecommendationFeed from '@/components/recommendations/RecommendationFeed.vue'

const authStore = useAuthStore()
const classStore = useClassStore()
const toast = useToast()
const recommendationStore = useRecommendationStore()

onMounted(async () => {
  // Recommendations are fetched by RecommendationFeed component
})

async function handleBook(classId: string) {
  try {
    const message = await classStore.bookClass(classId)
    await refreshRecommendations()
    toast.success(message)
  } catch (error: any) {
    toast.error(error.message || 'Failed to book class')
  }
}

async function handleCancel(classId: string) {
  try {
    const message = await classStore.cancelBooking(classId)
    await refreshRecommendations()
    toast.success(message)
  } catch (error: unknown) {
    toast.error(
      error instanceof Error ? error.message : 'Failed to cancel booking'
    )
  }
}

async function refreshRecommendations() {
  if (authStore.user) {
    await recommendationStore.fetchRecommendations(authStore.user.id, 10)
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 py-8">
    <div class="container mx-auto px-6">
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">
          Welcome back, {{ authStore.user?.firstName }}! 👋
        </h1>
        <p class="text-gray-600 mt-2">
          Here are your personalized class recommendations
        </p>
      </div>

      <!-- User Stats -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div class="bg-white rounded-lg shadow-md p-6">
          <div class="text-primary-600 text-3xl mb-2">🎯</div>
          <h3 class="text-lg font-semibold text-gray-700">Fitness Level</h3>
          <p class="text-2xl font-bold text-gray-900">{{ authStore.user?.fitnessLevel }}</p>
        </div>
        
        <div class="bg-white rounded-lg shadow-md p-6">
          <div class="text-primary-600 text-3xl mb-2">⭐</div>
          <h3 class="text-lg font-semibold text-gray-700">Segment</h3>
          <p class="text-2xl font-bold text-gray-900">{{ authStore.user?.segment || 'General' }}</p>
        </div>
        
        <div class="bg-white rounded-lg shadow-md p-6">
          <div class="text-primary-600 text-3xl mb-2">💪</div>
          <h3 class="text-lg font-semibold text-gray-700">Preferred Classes</h3>
          <p class="text-sm text-gray-600">{{ authStore.user?.preferredClassTypes.join(', ') || 'None set' }}</p>
        </div>
      </div>

      <!-- Recommendations -->
      <RecommendationFeed
        @book="handleBook"
        @cancel="handleCancel"
      />
    </div>
  </div>
</template>
