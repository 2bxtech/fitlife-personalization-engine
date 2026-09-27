<script setup lang="ts">
import { computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'
import { useRecommendationStore } from '@/stores/recommendations'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'
import { formatClassTime, spotsLeft } from '@/utils/format'

const props = defineProps<{ classData: Class; pending?: boolean }>()
const emit = defineEmits<{ book: [classId: string]; cancel: [classId: string] }>()

const authStore = useAuthStore()
const recommendationStore = useRecommendationStore()

const left = computed(() => spotsLeft(props.classData.capacity, props.classData.currentEnrollment))
const full = computed(() => left.value === 0 && !props.classData.isBookedByCurrentUser)
const rating = computed(() =>
  props.classData.averageRating > 0 ? `${props.classData.averageRating.toFixed(1)} / 5` : 'No ratings yet'
)

onMounted(() => {
  // Batched by the store: a page of cards produces one tracking request.
  if (authStore.user) recommendationStore.trackView(authStore.user.id, props.classData.id, 'browse')
})
</script>

<template>
  <article class="flex flex-col rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
    <div class="flex items-start justify-between gap-3">
      <div>
        <h3 class="font-semibold text-slate-900">{{ classData.name }}</h3>
        <p class="text-sm text-slate-600">{{ classData.instructorName }}</p>
      </div>
      <span class="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">{{ classData.type }}</span>
    </div>
    <p class="mt-3 flex-1 text-sm text-slate-700">{{ classData.description }}</p>
    <dl class="mt-4 grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
      <dt class="text-slate-500">When</dt>
      <dd class="text-right font-medium text-slate-900">{{ formatClassTime(classData.startTime) }}</dd>
      <dt class="text-slate-500">Level</dt>
      <dd class="text-right font-medium text-slate-900">{{ classData.level }}</dd>
      <dt class="text-slate-500">Rating</dt>
      <dd class="text-right font-medium text-slate-900">{{ rating }}</dd>
      <dt class="text-slate-500">Spots left</dt>
      <dd class="text-right font-medium" :class="left <= 3 ? 'text-accent-800' : 'text-slate-900'">
        {{ left }} of {{ classData.capacity }}
      </dd>
    </dl>
    <div class="mt-4 flex items-center gap-2">
      <span
        v-if="classData.isBookedByCurrentUser"
        class="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-800"
      >
        Booked
      </span>
      <AppButton
        v-if="classData.isBookedByCurrentUser"
        class="ml-auto"
        variant="danger"
        size="sm"
        :loading="pending"
        @click="emit('cancel', classData.id)"
      >
        {{ pending ? 'Cancelling…' : 'Cancel booking' }}
      </AppButton>
      <AppButton v-else class="ml-auto" size="sm" :loading="pending" :disabled="full" @click="emit('book', classData.id)">
        {{ pending ? 'Booking…' : full ? 'Class full' : 'Book' }}
      </AppButton>
    </div>
  </article>
</template>
