<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import PersonaPicker from '@/components/demo/PersonaPicker.vue'
import AppAlert from '@/components/ui/AppAlert.vue'
import AppButton from '@/components/ui/AppButton.vue'

const authStore = useAuthStore()
const notice = ref<string | null>(null)

onMounted(() => {
  notice.value = authStore.consumeSessionNotice()
})

const steps = [
  {
    title: 'Pick a member',
    body: 'Each synthetic persona has a fixed history, so every visitor sees the same starting point.',
  },
  {
    title: 'See why each class ranks where it does',
    body: 'Nine deterministic rules score every upcoming class. Open any card to see each rule’s points.',
  },
  {
    title: 'Book, then switch',
    body: 'Booking updates the card at once. Switch personas to watch the same catalog rank differently.',
  },
]
</script>

<template>
  <div>
    <section class="border-b border-slate-200 bg-gradient-to-b from-primary-50 to-slate-50">
      <div class="mx-auto max-w-6xl px-4 pb-12 pt-14 sm:px-6 sm:pt-20">
        <p class="text-sm font-semibold uppercase tracking-wider text-primary-800">Explainable personalization</p>
        <h1 class="mt-3 max-w-3xl text-4xl font-bold tracking-tight text-slate-900 sm:text-5xl">
          Class recommendations you can audit
        </h1>
        <p class="mt-4 max-w-2xl text-lg text-slate-700">
          FitLife ranks gym classes for each member with fixed, explainable rules, not a black-box model.
          Every recommendation shows the points each rule contributed.
        </p>
        <div class="mt-6 flex flex-wrap gap-3">
          <template v-if="authStore.isAuthenticated">
            <AppButton size="lg" to="/dashboard">Back to your recommendations</AppButton>
          </template>
          <template v-else>
            <AppButton size="lg" :to="{ path: '/', hash: '#personas' }">Try it with a demo member</AppButton>
            <AppButton size="lg" variant="secondary" to="/login">Sign in</AppButton>
          </template>
        </div>
      </div>
    </section>

    <section id="personas" class="mx-auto max-w-6xl scroll-mt-20 px-4 py-12 sm:px-6" aria-labelledby="personas-title">
      <AppAlert v-if="notice" tone="warning" class="mb-6">{{ notice }}</AppAlert>
      <h2 id="personas-title" class="text-2xl font-bold text-slate-900">Explore as a demo member</h2>
      <p class="mt-2 max-w-2xl text-slate-600">
        One click, no sign-up and no personal data. Starting a session resets that member, so any bookings from
        earlier visitors are undone.
      </p>
      <div class="mt-6">
        <PersonaPicker />
      </div>
    </section>

    <section class="mx-auto max-w-6xl px-4 pb-16 sm:px-6" aria-labelledby="how-title">
      <h2 id="how-title" class="text-2xl font-bold text-slate-900">How the demo works</h2>
      <ol class="mt-6 grid gap-4 md:grid-cols-3">
        <li v-for="(step, index) in steps" :key="step.title" class="rounded-2xl bg-white p-5 ring-1 ring-slate-200">
          <span class="text-sm font-semibold text-primary-800">Step {{ index + 1 }}</span>
          <h3 class="mt-1 font-semibold text-slate-900">{{ step.title }}</h3>
          <p class="mt-2 text-sm text-slate-600">{{ step.body }}</p>
        </li>
      </ol>
      <p class="mt-8 text-sm text-slate-600">
        Built with .NET 8, EF Core and SQL Server, Vue 3, Redis, and an optional Kafka pipeline.
        <a
          href="https://github.com/2bxtech/fitlife-personalization-engine#readme"
          class="font-semibold text-primary-800 underline-offset-4 hover:underline"
        >Read the architecture and trade-offs</a>.
      </p>
    </section>
  </div>
</template>
