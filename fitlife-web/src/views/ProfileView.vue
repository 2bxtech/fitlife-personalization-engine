<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRecommendationStore } from '@/stores/recommendations'
import { useToast } from '@/composables/useToast'
import { userService } from '@/services/userService'
import AppButton from '@/components/ui/AppButton.vue'
import AppAlert from '@/components/ui/AppAlert.vue'
import ChipCheckboxGroup from '@/components/ui/ChipCheckboxGroup.vue'
import { CLASS_TYPES, FITNESS_GOALS, FITNESS_LEVELS } from '@/constants/profile'

const authStore = useAuthStore()
const recommendationStore = useRecommendationStore()
const toast = useToast()

const user = computed(() => authStore.user)
const saving = ref(false)
const error = ref<string | null>(null)
const saved = ref(false)

function snapshot() {
  return {
    fitnessLevel: user.value?.fitnessLevel ?? 'Beginner',
    goals: [...(user.value?.goals ?? [])],
    preferredClassTypes: [...(user.value?.preferredClassTypes ?? [])],
  }
}

const form = ref(snapshot())
const dirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(snapshot()))

async function save() {
  if (!user.value) return
  // The member may sign out or switch persona while this is in flight; a
  // response for an earlier session must not overwrite the current identity.
  const session = authStore.token
  saving.value = true
  error.value = null
  saved.value = false
  try {
    const updated = await userService.updatePreferences(user.value.id, form.value)
    if (authStore.token !== session || !user.value) return
    authStore.setUser({ ...user.value, ...updated })
    form.value = snapshot()
    saved.value = true
    toast.success('Preferences saved')
    // Re-rank in the background; the dashboard also reloads when opened.
    void recommendationStore.refreshRecommendations(user.value.id).catch(() => undefined)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Your preferences could not be saved.'
  } finally {
    saving.value = false
  }
}

function discard() {
  form.value = snapshot()
  error.value = null
}
</script>

<template>
  <div class="mx-auto max-w-3xl px-4 py-8 sm:px-6">
    <h1 class="text-3xl font-bold tracking-tight text-slate-900">Your profile</h1>
    <p class="mt-1 text-slate-600">
      Preferences feed the scorer directly: a preferred class type is worth 15 points, and fitness level up to 10.
    </p>

    <section class="mt-6 rounded-2xl bg-white p-5 ring-1 ring-slate-200" aria-labelledby="account-title">
      <h2 id="account-title" class="text-sm font-semibold text-slate-900">Account</h2>
      <dl class="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
        <dt class="text-slate-500">Name</dt>
        <dd class="font-medium text-slate-900">{{ user?.firstName }} {{ user?.lastName }}</dd>
        <dt class="text-slate-500">Email</dt>
        <dd class="font-medium text-slate-900">{{ user?.email }}</dd>
        <dt class="text-slate-500">Activity profile</dt>
        <dd class="font-medium text-slate-900">
          {{ user?.segment ?? 'Not assigned yet' }}
          <span class="block font-normal text-slate-600">Assigned by the scheduler from recent completed classes.</span>
        </dd>
      </dl>
    </section>

    <AppAlert v-if="authStore.personaId" tone="info" class="mt-6">
      This is a demo member. Change anything you like; the next demo session for this member restores the original
      profile.
    </AppAlert>

    <form class="mt-6 space-y-6 rounded-2xl bg-white p-5 ring-1 ring-slate-200" aria-labelledby="prefs-title" @submit.prevent="save">
      <h2 id="prefs-title" class="text-sm font-semibold text-slate-900">Training preferences</h2>

      <div>
        <label for="fitness-level" class="text-sm font-medium text-slate-800">Fitness level</label>
        <select
          id="fitness-level"
          v-model="form.fitnessLevel"
          class="mt-1 block w-full rounded-lg border-0 px-3 py-2 text-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-primary-700 sm:w-64"
        >
          <option v-for="level in FITNESS_LEVELS" :key="level" :value="level">{{ level }}</option>
        </select>
      </div>

      <ChipCheckboxGroup
        v-model="form.preferredClassTypes"
        legend="Preferred class types"
        hint="Classes of these types rank higher."
        :options="CLASS_TYPES"
      />
      <ChipCheckboxGroup
        v-model="form.goals"
        legend="Goals"
        hint="Stored with your profile. The current scorer does not use goals."
        :options="FITNESS_GOALS"
      />

      <AppAlert v-if="error" tone="error">{{ error }}</AppAlert>
      <AppAlert v-else-if="saved" tone="success">
        Saved. <router-link to="/dashboard" class="font-semibold underline">See your updated recommendations</router-link>
      </AppAlert>

      <div class="flex gap-2">
        <AppButton type="submit" :loading="saving" :disabled="!dirty">Save preferences</AppButton>
        <AppButton variant="ghost" :disabled="!dirty || saving" @click="discard">Discard changes</AppButton>
      </div>
    </form>
  </div>
</template>
