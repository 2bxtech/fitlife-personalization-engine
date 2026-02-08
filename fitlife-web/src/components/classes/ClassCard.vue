<script setup lang="ts">
import { computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'
import { useRecommendationStore } from '@/stores/recommendations'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{
  classData: Class
  showRecommendationReason?: boolean
  recommendationReason?: string
}>()

const emit = defineEmits<{
  book: [classId: string]
}>()

const authStore = useAuthStore()
const recommendationStore = useRecommendationStore()

const availabilityPercent = computed(() => 
  ((props.classData.capacity - props.classData.currentEnrollment) / props.classData.capacity) * 100
)

const availabilityColor = computed(() => {
  if (availabilityPercent.value < 20) return 'text-red-600'
  if (availabilityPercent.value < 50) return 'text-yellow-600'
  return 'text-green-600'
})

const formattedDate = computed(() => {
  const date = new Date(props.classData.startTime)
  return date.toLocaleDateString('en-US', { 
    weekday: 'short', 
    month: 'short', 
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  })
})

async function handleView() {
  const viewedKey = `viewed_${props.classData.id}`
  if (sessionStorage.getItem(viewedKey)) return
  if (authStore.user) {
    sessionStorage.setItem(viewedKey, '1')
    await recommendationStore.trackEvent({
      userId: authStore.user.id,
      itemId: props.classData.id,
      itemType: 'Class',
      eventType: 'View',
      metadata: { source: 'browse' }
    })
  }
}

async function handleBook() {
  emit('book', props.classData.id)
  if (authStore.user) {
    await recommendationStore.trackEvent({
      userId: authStore.user.id,
      itemId: props.classData.id,
      itemType: 'Class',
      eventType: 'Book',
      metadata: { source: 'browse' }
    })
  }
}

onMounted(() => {
  handleView()
})
</script>

<template>
  <div class="bg-white rounded-lg shadow-md p-6 hover:shadow-lg transition-shadow">
    <div class="flex justify-between items-start mb-4">
      <div>
        <h3 class="text-xl font-bold text-gray-800">{{ classData.name }}</h3>
        <p class="text-gray-600 text-sm">{{ classData.instructorName }}</p>
      </div>
      <span class="px-3 py-1 bg-primary-100 text-primary-700 rounded-full text-sm font-semibold">
        {{ classData.type }}
      </span>
    </div>

    <p class="text-gray-700 mb-4 line-clamp-2">{{ classData.description }}</p>

    <div class="grid grid-cols-2 gap-4 mb-4 text-sm">
      <div>
        <span class="text-gray-600">Level:</span>
        <span class="ml-2 font-semibold">{{ classData.level }}</span>
      </div>
      <div>
        <span class="text-gray-600">Rating:</span>
        <span class="ml-2 font-semibold">{{ classData.averageRating.toFixed(1) }} ⭐</span>
      </div>
      <div>
        <span class="text-gray-600">Date:</span>
        <span class="ml-2 font-semibold">{{ formattedDate }}</span>
      </div>
      <div>
        <span class="text-gray-600">Spots:</span>
        <span :class="['ml-2 font-semibold', availabilityColor]">
          {{ classData.capacity - classData.currentEnrollment }} / {{ classData.capacity }}
        </span>
      </div>
    </div>

    <div v-if="showRecommendationReason && recommendationReason" class="mb-4 p-3 bg-blue-50 rounded-lg">
      <p class="text-sm text-blue-800">
        <span class="font-semibold">Why recommended:</span> {{ recommendationReason }}
      </p>
    </div>

    <button 
      @click="handleBook"
      :disabled="classData.currentEnrollment >= classData.capacity"
      class="w-full px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
    >
      {{ classData.currentEnrollment >= classData.capacity ? 'Class Full' : 'Book Now' }}
    </button>
  </div>
</template>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
