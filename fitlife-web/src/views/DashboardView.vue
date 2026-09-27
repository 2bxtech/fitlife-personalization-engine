<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useClassStore } from '@/stores/classes'
import { useRecommendationStore } from '@/stores/recommendations'
import { useToast } from '@/composables/useToast'
import { demoService } from '@/services/demoService'
import type { DemoPersona } from '@/types/Demo'
import RecommendationCard from '@/components/recommendations/RecommendationCard.vue'
import AppAlert from '@/components/ui/AppAlert.vue'
import AppButton from '@/components/ui/AppButton.vue'

const authStore = useAuthStore()
const classStore = useClassStore()
const recommendationStore = useRecommendationStore()
const toast = useToast()

const personas = ref<DemoPersona[]>([])
const switching = ref<string | null>(null)
/** A background reload failed while older results are still shown. */
const staleWarning = ref(false)

const user = computed(() => authStore.user)
const isDemo = computed(() => authStore.personaId !== null)

async function load() {
  if (!user.value) return
  staleWarning.value = false
  try {
    await recommendationStore.fetchRecommendations(user.value.id, 8)
  } catch {
    staleWarning.value = recommendationStore.recommendations.length > 0
  }
}

async function recompute() {
  if (!user.value) return
  staleWarning.value = false
  try {
    await recommendationStore.refreshRecommendations(user.value.id)
  } catch {
    staleWarning.value = recommendationStore.recommendations.length > 0
  }
}

// A booking succeeds or fails on its own. The follow-up reload only re-ranks;
// if it fails, the booking still stands and the member sees a quiet warning.
async function handleAction(classId: string, action: 'book' | 'cancel') {
  try {
    const result =
      action === 'book' ? await classStore.bookClass(classId) : await classStore.cancelBooking(classId)
    // The member switched personas while this was in flight; it belongs to them, not this view.
    if (!result.current) return
    recommendationStore.applyClassUpdate(result.classData)
    toast.success(result.message)
  } catch (error: unknown) {
    toast.error(error instanceof Error ? error.message : 'That did not work. Please try again.')
    return
  }
  void load()
}

async function switchPersona(persona: DemoPersona) {
  if (persona.id === authStore.personaId) return
  switching.value = persona.id
  try {
    await authStore.startDemoSession(persona.id)
    await load()
  } catch {
    toast.error(`Could not switch to ${persona.firstName}. Please try again.`)
  } finally {
    switching.value = null
  }
}

onMounted(async () => {
  void load()
  if (isDemo.value) personas.value = (await demoService.listPersonas().catch(() => null)) ?? []
})
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
      <div>
        <p v-if="isDemo" class="text-sm font-semibold text-primary-800">Demo member</p>
        <h1 class="text-3xl font-bold tracking-tight text-slate-900">Recommended for {{ user?.firstName }}</h1>
        <p class="mt-1 text-slate-600">
          {{ user?.fitnessLevel }} · prefers {{ user?.preferredClassTypes.join(', ') || 'no class types yet' }}
          <template v-if="user?.segment"> · activity profile: {{ user.segment }}</template>
        </p>
      </div>

      <div v-if="personas.length" role="group" aria-label="Switch demo member" class="flex flex-wrap items-center gap-2">
        <span class="text-sm text-slate-600">View as</span>
        <AppButton
          v-for="persona in personas"
          :key="persona.id"
          size="sm"
          :variant="persona.id === authStore.personaId ? 'primary' : 'secondary'"
          :aria-pressed="persona.id === authStore.personaId"
          :loading="switching === persona.id"
          :disabled="switching !== null || classStore.pendingIds.size > 0"
          :data-testid="`switch-${persona.id}`"
          @click="switchPersona(persona)"
        >
          {{ persona.firstName }}
        </AppButton>
      </div>
    </div>

    <div class="mt-6 flex items-center justify-between gap-4">
      <h2 class="text-lg font-semibold text-slate-900">
        Top classes
        <span v-if="recommendationStore.refreshing" class="ml-2 text-sm font-normal text-slate-500" role="status">Updating…</span>
      </h2>
      <AppButton
        variant="secondary"
        size="sm"
        :loading="recommendationStore.refreshing"
        :disabled="recommendationStore.loading"
        @click="recompute"
      >
        Recompute
      </AppButton>
    </div>

    <AppAlert v-if="staleWarning" tone="warning" class="mt-4">
      Showing earlier results; the latest ranking could not be loaded.
      <template #action><AppButton variant="secondary" size="sm" @click="load">Retry</AppButton></template>
    </AppAlert>

    <div v-if="recommendationStore.status === 'loading'" class="mt-4 grid gap-4" aria-busy="true" aria-label="Loading recommendations">
      <div v-for="i in 4" :key="i" class="h-36 animate-pulse rounded-2xl bg-slate-200" />
    </div>

    <AppAlert v-else-if="recommendationStore.status === 'error'" tone="error" title="Recommendations could not be loaded" class="mt-4">
      {{ recommendationStore.error }}
      <template #action><AppButton variant="secondary" size="sm" @click="load">Try again</AppButton></template>
    </AppAlert>

    <div
      v-else-if="recommendationStore.status === 'ready' && recommendationStore.recommendations.length === 0"
      class="mt-4 rounded-2xl bg-white p-8 text-center ring-1 ring-slate-200"
    >
      <p class="font-semibold text-slate-900">No upcoming classes to recommend</p>
      <p class="mt-1 text-sm text-slate-600">Every scheduled class is full or has started.</p>
      <AppButton class="mt-4" variant="secondary" to="/classes">Browse all classes</AppButton>
    </div>

    <ol v-else class="mt-4 grid gap-4" :aria-busy="recommendationStore.refreshing">
      <li v-for="recommendation in recommendationStore.recommendations" :key="recommendation.class.id">
        <RecommendationCard
          :recommendation="recommendation"
          :pending="classStore.isPending(recommendation.class.id)"
          @book="handleAction($event, 'book')"
          @cancel="handleAction($event, 'cancel')"
        />
      </li>
    </ol>
  </div>
</template>
