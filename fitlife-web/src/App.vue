<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Header from '@/components/layout/Header.vue'
import Footer from '@/components/layout/Footer.vue'
import ToastNotification from '@/components/common/ToastNotification.vue'

const route = useRoute()
const router = useRouter()
const main = ref<HTMLElement | null>(null)
const announcement = ref('')

// Single-page navigation does not move focus or announce the new page, so a
// keyboard or screen-reader user would stay on the link they activated. After
// the initial load, move focus to the main landmark and announce each new page.
onMounted(async () => {
  await router.isReady()
  watch(
    () => route.fullPath,
    async () => {
      await nextTick()
      announcement.value = document.title
      main.value?.focus()
    }
  )
})
</script>

<template>
  <a
    href="#main"
    class="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:font-semibold focus:text-primary-800 focus:shadow-lg"
  >
    Skip to main content
  </a>
  <Header />
  <main id="main" ref="main" tabindex="-1" class="flex-grow focus:outline-none">
    <router-view />
  </main>
  <Footer />
  <ToastNotification />
  <p class="sr-only" aria-live="polite" aria-atomic="true" data-testid="route-announcer">{{ announcement }}</p>
</template>
