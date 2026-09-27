<script setup lang="ts">
import { computed } from 'vue'
import type { ScoreFactor } from '@/types/Recommendation'

const props = defineProps<{ factors: ScoreFactor[]; score: number }>()

// Largest contributions first; factors that scored nothing stay visible (muted)
// so the breakdown shows every rule that was evaluated, not a curated subset.
const rows = computed(() => [...props.factors].sort((a, b) => b.points - a.points))
const scale = computed(() => Math.max(20, ...props.factors.map((factor) => Math.abs(factor.points))))

function formatPoints(points: number) {
  const rounded = Math.round(points * 10) / 10
  return rounded > 0 ? `+${rounded}` : `${rounded}`
}
</script>

<template>
  <div>
    <table class="w-full text-sm">
      <caption class="sr-only">Score breakdown, total {{ score.toFixed(1) }} points</caption>
      <thead class="sr-only">
        <tr><th scope="col">Factor</th><th scope="col">Why</th><th scope="col">Points</th></tr>
      </thead>
      <tbody>
        <tr v-for="factor in rows" :key="factor.key" :class="factor.points === 0 ? 'text-slate-500' : 'text-slate-800'">
          <th scope="row" class="w-24 py-1.5 pr-3 text-left align-top font-medium sm:w-36">{{ factor.label }}</th>
          <td class="py-1.5 pr-3 align-top">
            <span>{{ factor.detail }}</span>
            <span class="mt-1 block h-1.5 rounded-full bg-slate-100" aria-hidden="true">
              <span
                class="block h-1.5 rounded-full"
                :class="factor.points < 0 ? 'bg-red-500' : 'bg-primary-600'"
                :style="{ width: `${(Math.abs(factor.points) / scale) * 100}%` }"
              />
            </span>
          </td>
          <td class="w-14 py-1.5 text-right align-top font-mono tabular-nums" :class="factor.points < 0 ? 'text-red-700' : ''">
            {{ formatPoints(factor.points) }}
          </td>
        </tr>
      </tbody>
      <tfoot>
        <tr class="border-t border-slate-200 font-semibold text-slate-900">
          <th scope="row" colspan="2" class="pt-2 text-left">Total score</th>
          <td class="pt-2 text-right font-mono tabular-nums">{{ score.toFixed(1) }}</td>
        </tr>
      </tfoot>
    </table>
    <p class="mt-3 text-xs text-slate-500">
      Nine fixed rules, computed by the API for this member. No trained model is involved.
    </p>
  </div>
</template>
