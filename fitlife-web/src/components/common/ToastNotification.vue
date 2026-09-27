<script setup lang="ts">
import { computed } from 'vue'
import { useToast } from '@/composables/useToast'

const { toasts, removeToast } = useToast()

const typeStyles: Record<string, string> = {
  success: 'bg-white text-slate-900 ring-emerald-300',
  error: 'bg-red-50 text-red-900 ring-red-300',
  warning: 'bg-accent-50 text-accent-900 ring-accent-200',
  info: 'bg-white text-slate-900 ring-slate-300',
}

const iconStyles: Record<string, string> = {
  success: 'bg-emerald-600',
  error: 'bg-red-600',
  warning: 'bg-accent-500',
  info: 'bg-primary-700',
}

// Errors go in an assertive region; everything else is announced politely.
const errors = computed(() => toasts.value.filter((toast) => toast.type === 'error'))
const notices = computed(() => toasts.value.filter((toast) => toast.type !== 'error'))
</script>

<template>
  <div class="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2 sm:inset-x-auto sm:right-4">
    <div role="alert" aria-live="assertive" aria-atomic="false" class="flex w-full flex-col items-end gap-2 sm:w-96" data-testid="toast-alerts">
      <div
        v-for="toast in errors"
        :key="toast.id"
        :class="['toast pointer-events-auto flex w-full items-start gap-3 rounded-xl p-4 text-sm shadow-lg ring-1', typeStyles[toast.type]]"
      >
        <span :class="['mt-1.5 h-2 w-2 shrink-0 rounded-full', iconStyles[toast.type]]" aria-hidden="true" />
        <p class="flex-1 font-medium">{{ toast.message }}</p>
        <button type="button" class="rounded p-0.5 text-slate-500 hover:text-slate-900" @click="removeToast(toast.id)">
          <span class="sr-only">Dismiss</span>
          <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path d="M6.3 5 5 6.3 8.7 10 5 13.7 6.3 15 10 11.3 13.7 15 15 13.7 11.3 10 15 6.3 13.7 5 10 8.7z" /></svg>
        </button>
      </div>
    </div>
    <div role="status" aria-live="polite" aria-atomic="false" class="flex w-full flex-col items-end gap-2 sm:w-96" data-testid="toast-notices">
      <div
        v-for="toast in notices"
        :key="toast.id"
        :class="['toast pointer-events-auto flex w-full items-start gap-3 rounded-xl p-4 text-sm shadow-lg ring-1', typeStyles[toast.type]]"
      >
        <span :class="['mt-1.5 h-2 w-2 shrink-0 rounded-full', iconStyles[toast.type]]" aria-hidden="true" />
        <p class="flex-1 font-medium">{{ toast.message }}</p>
        <button type="button" class="rounded p-0.5 text-slate-500 hover:text-slate-900" @click="removeToast(toast.id)">
          <span class="sr-only">Dismiss</span>
          <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path d="M6.3 5 5 6.3 8.7 10 5 13.7 6.3 15 10 11.3 13.7 15 15 13.7 11.3 10 15 6.3 13.7 5 10 8.7z" /></svg>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.toast {
  animation: toast-in 0.2s ease-out;
}

@keyframes toast-in {
  from {
    transform: translateY(0.5rem);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}
</style>
