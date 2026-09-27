<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { demoService } from '@/services/demoService'
import { useAuthStore } from '@/stores/auth'
import type { DemoPersona } from '@/types/Demo'
import AppButton from '@/components/ui/AppButton.vue'
import AppAlert from '@/components/ui/AppAlert.vue'

const props = withDefaults(defineProps<{ redirectTo?: string }>(), { redirectTo: '/dashboard' })

const router = useRouter()
const authStore = useAuthStore()
const personas = ref<DemoPersona[] | null>(null)
const status = ref<'loading' | 'ready' | 'unavailable' | 'error'>('loading')
const starting = ref<string | null>(null)
const startError = ref<string | null>(null)

async function load() {
  status.value = 'loading'
  try {
    personas.value = await demoService.listPersonas()
    status.value = personas.value ? 'ready' : 'unavailable'
  } catch {
    status.value = 'error'
  }
}

async function start(persona: DemoPersona) {
  starting.value = persona.id
  startError.value = null
  try {
    await authStore.startDemoSession(persona.id)
    await router.push(props.redirectTo)
  } catch {
    startError.value = `Could not start ${persona.firstName}'s session. The API may be waking up; try again in a moment.`
  } finally {
    starting.value = null
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div v-if="status === 'loading'" class="grid gap-4 md:grid-cols-3" aria-busy="true" aria-label="Loading personas">
      <div v-for="i in 3" :key="i" class="h-48 animate-pulse rounded-2xl bg-slate-200" />
    </div>

    <AppAlert v-else-if="status === 'error'" tone="error" title="Demo personas are unavailable">
      The API did not respond. It may be starting up.
      <template #action><AppButton variant="secondary" size="sm" @click="load">Try again</AppButton></template>
    </AppAlert>

    <AppAlert v-else-if="status === 'unavailable'" tone="info" title="Demo mode is off in this environment">
      <router-link to="/login" class="font-semibold underline">Sign in</router-link> or
      <router-link to="/register" class="font-semibold underline">create an account</router-link> to explore.
    </AppAlert>

    <template v-else>
      <AppAlert v-if="startError" tone="error" class="mb-4">{{ startError }}</AppAlert>
      <ul class="grid gap-4 md:grid-cols-3">
        <li
          v-for="persona in personas"
          :key="persona.id"
          class="flex flex-col rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200"
        >
          <div class="flex items-center gap-3">
            <span class="grid h-11 w-11 place-items-center rounded-full bg-primary-700 text-lg font-bold text-white" aria-hidden="true">
              {{ persona.firstName.charAt(0) }}
            </span>
            <div>
              <h3 class="font-semibold text-slate-900">{{ persona.firstName }}</h3>
              <p class="text-sm text-slate-600">{{ persona.fitnessLevel }} · {{ persona.preferredClassTypes.join(', ') }}</p>
            </div>
          </div>
          <p class="mt-3 text-sm font-medium text-slate-900">{{ persona.headline }}</p>
          <p class="mt-1 flex-1 text-sm text-slate-600">{{ persona.summary }}</p>
          <AppButton
            class="mt-4"
            block
            :loading="starting === persona.id"
            :disabled="starting !== null && starting !== persona.id"
            :data-testid="`persona-${persona.id}`"
            @click="start(persona)"
          >
            {{ starting === persona.id ? 'Starting…' : `Explore as ${persona.firstName}` }}
          </AppButton>
        </li>
      </ul>
    </template>
  </div>
</template>
