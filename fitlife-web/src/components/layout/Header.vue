<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { onKeyStroke } from '@vueuse/core'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'

const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()
const menuOpen = ref(false)
const menuButton = ref<HTMLButtonElement | null>(null)

const memberLinks = [
  { to: '/dashboard', label: 'Recommendations' },
  { to: '/classes', label: 'Classes' },
  { to: '/profile', label: 'Profile' },
]

function handleLogout() {
  authStore.logout()
  menuOpen.value = false
  router.push('/login')
}

// Close the mobile menu on navigation and on Escape, returning focus to its toggle.
watch(() => route.fullPath, () => (menuOpen.value = false))
onKeyStroke('Escape', () => {
  if (!menuOpen.value) return
  menuOpen.value = false
  menuButton.value?.focus()
})
</script>

<template>
  <header class="sticky top-0 z-40 border-b border-slate-200 bg-white/90 backdrop-blur">
    <nav aria-label="Primary" class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
      <router-link to="/" class="flex items-center gap-2 rounded-lg text-lg font-bold tracking-tight text-slate-900">
        <span class="grid h-8 w-8 place-items-center rounded-lg bg-primary-700 text-white" aria-hidden="true">
          <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round">
            <path d="M4 12h3l2-5 4 10 2-5h5" />
          </svg>
        </span>
        FitLife
      </router-link>

      <div class="hidden items-center gap-1 md:flex">
        <template v-if="authStore.isAuthenticated">
          <router-link
            v-for="link in memberLinks"
            :key="link.to"
            :to="link.to"
            class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 hover:text-slate-900"
            active-class="bg-primary-50 text-primary-800"
          >
            {{ link.label }}
          </router-link>
          <AppButton variant="ghost" size="sm" class="ml-2" @click="handleLogout">Sign out</AppButton>
        </template>
        <template v-else>
          <AppButton variant="ghost" size="sm" to="/login">Sign in</AppButton>
          <AppButton size="sm" to="/register">Create account</AppButton>
        </template>
      </div>

      <button
        ref="menuButton"
        type="button"
        class="rounded-lg p-2 text-slate-700 hover:bg-slate-100 md:hidden"
        :aria-expanded="menuOpen"
        aria-controls="mobile-menu"
        @click="menuOpen = !menuOpen"
      >
        <span class="sr-only">{{ menuOpen ? 'Close menu' : 'Open menu' }}</span>
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path v-if="!menuOpen" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
          <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </nav>

    <div v-show="menuOpen" id="mobile-menu" class="border-t border-slate-200 px-4 pb-4 pt-2 md:hidden">
      <template v-if="authStore.isAuthenticated">
        <router-link
          v-for="link in memberLinks"
          :key="link.to"
          :to="link.to"
          class="block rounded-lg px-3 py-2 font-medium text-slate-700 hover:bg-slate-100"
          active-class="bg-primary-50 text-primary-800"
        >
          {{ link.label }}
        </router-link>
        <button type="button" class="block w-full rounded-lg px-3 py-2 text-left font-medium text-slate-700 hover:bg-slate-100" @click="handleLogout">
          Sign out
        </button>
      </template>
      <template v-else>
        <router-link to="/register" class="block rounded-lg px-3 py-2 font-medium text-primary-800 hover:bg-primary-50">Create account</router-link>
        <router-link to="/login" class="block rounded-lg px-3 py-2 font-medium text-slate-700 hover:bg-slate-100">Sign in</router-link>
      </template>
    </div>
  </header>
</template>
