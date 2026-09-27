<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import type { Recommendation } from '@/types/Recommendation'
import AppButton from '@/components/ui/AppButton.vue'
import ScoreBreakdown from './ScoreBreakdown.vue'
import { formatClassTime, spotsLeft } from '@/utils/format'

const props = defineProps<{ recommendation: Recommendation; pending?: boolean }>()
const emit = defineEmits<{ book: [classId: string]; cancel: [classId: string] }>()

const expanded = ref(false)
const breakdownId = useId()
const item = computed(() => props.recommendation.class)
const left = computed(() => spotsLeft(item.value.capacity, item.value.currentEnrollment))
const full = computed(() => left.value === 0 && !item.value.isBookedByCurrentUser)
</script>

<template>
  <article
    class="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200 transition-shadow hover:shadow-md"
    :aria-labelledby="`${breakdownId}-title`"
  >
    <div class="flex items-start gap-4">
      <span
        class="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary-50 text-sm font-bold text-primary-800 ring-1 ring-primary-200"
        :aria-label="`Rank ${recommendation.rank}`"
      >
        {{ recommendation.rank }}
      </span>
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
          <h3 :id="`${breakdownId}-title`" class="text-base font-semibold text-slate-900">{{ item.name }}</h3>
          <span class="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">{{ item.type }}</span>
          <span
            v-if="item.isBookedByCurrentUser"
            class="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-800"
          >
            Booked
          </span>
        </div>
        <p class="mt-0.5 text-sm text-slate-600">
          {{ item.instructorName }} · {{ formatClassTime(item.startTime) }} · {{ item.level }}
        </p>

        <p class="mt-3 text-sm font-medium text-slate-900">{{ recommendation.reason }}</p>

        <div class="mt-4 flex flex-wrap items-center gap-3">
          <AppButton
            v-if="item.isBookedByCurrentUser"
            variant="danger"
            size="sm"
            :loading="pending"
            @click="emit('cancel', item.id)"
          >
            {{ pending ? 'Cancelling…' : 'Cancel booking' }}
          </AppButton>
          <AppButton v-else size="sm" :loading="pending" :disabled="full" @click="emit('book', item.id)">
            {{ pending ? 'Booking…' : full ? 'Class full' : 'Book' }}
          </AppButton>
          <span class="text-sm" :class="left <= 3 ? 'font-semibold text-accent-800' : 'text-slate-600'">
            {{ left }} of {{ item.capacity }} spots left
          </span>
          <button
            v-if="recommendation.factors.length"
            type="button"
            class="ml-auto rounded-lg px-2 py-1 text-sm font-semibold text-primary-800 hover:bg-primary-50"
            :aria-expanded="expanded"
            :aria-controls="breakdownId"
            @click="expanded = !expanded"
          >
            {{ expanded ? 'Hide score' : `Why #${recommendation.rank}?` }}
            <span class="font-mono text-slate-500">{{ recommendation.score.toFixed(1) }} pts</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Outside the badge row so the table can use the full card width on phones. -->
    <div v-show="expanded" :id="breakdownId" class="mt-4 border-t border-slate-100 pt-4">
      <ScoreBreakdown :factors="recommendation.factors" :score="recommendation.score" />
    </div>
  </article>
</template>
