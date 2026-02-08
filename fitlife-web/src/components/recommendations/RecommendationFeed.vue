<script setup lang="ts">
import { onMounted } from 'vue'
import { useRecommendationStore } from '@/stores/recommendations'
import { useAuthStore } from '@/stores/auth'
import ClassCard from '@/components/classes/ClassCard.vue'

const recommendationStore = useRecommendationStore()
const authStore = useAuthStore()

const emit = defineEmits<{
  book: [classId: string]
}>()

onMounted(async () => {
  if (authStore.user) {
    await recommendationStore.fetchRecommendations(authStore.user.id, 10)
  }
})

async function handleRefresh() {
  if (authStore.user) {
    await recommendationStore.refreshRecommendations(authStore.user.id)
  }
}

function handleBook(classId: string) {
  emit('book', classId)
}
</script>

<template>
  <div>
    <div class="flex justify-between items-center mb-6">
      <h2 class="text-2xl font-bold text-gray-800">Recommended For You</h2>
      <button 
        :disabled="recommendationStore.loading" 
        class="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:bg-gray-300 transition-colors"
        @click="handleRefresh"
      >
        {{ recommendationStore.loading ? 'Refreshing...' : 'Refresh' }}
      </button>
    </div>

    <div v-if="recommendationStore.error" class="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
      <p class="text-red-800">{{ recommendationStore.error }}</p>
    </div>

    <div v-if="recommendationStore.loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <div v-for="i in 6" :key="i" class="bg-gray-200 h-64 rounded-lg animate-pulse"></div>
    </div>

    <div v-else-if="recommendationStore.recommendations.length === 0" class="text-center py-12 bg-gray-50 rounded-lg">
      <p class="text-gray-600 text-lg mb-4">No recommendations yet</p>
      <p class="text-gray-500">Start browsing and booking classes to get personalized recommendations!</p>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <ClassCard
        v-for="rec in recommendationStore.recommendations"
        :key="rec.class.id"
        :class-data="rec.class"
        :show-recommendation-reason="true"
        :recommendation-reason="rec.reason"
        @book="handleBook"
      />
    </div>
  </div>
</template>
