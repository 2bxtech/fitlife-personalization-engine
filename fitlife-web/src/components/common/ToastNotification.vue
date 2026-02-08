<script setup lang="ts">
import { useToast } from '@/composables/useToast'

const { toasts, removeToast } = useToast()

const typeStyles: Record<string, string> = {
  success: 'bg-green-50 border-green-200 text-green-800',
  error: 'bg-red-50 border-red-200 text-red-800',
  warning: 'bg-yellow-50 border-yellow-200 text-yellow-800',
  info: 'bg-blue-50 border-blue-200 text-blue-800',
}

const typeIcons: Record<string, string> = {
  success: '✓',
  error: '✕',
  warning: '⚠',
  info: 'ℹ',
}
</script>

<template>
  <div class="fixed top-4 right-4 z-50 space-y-2 max-w-md">
    <div
      v-for="toast in toasts"
      :key="toast.id"
      :class="['flex items-start gap-3 p-4 rounded-lg border shadow-lg transition-all transform', typeStyles[toast.type]]"
      style="animation: slideIn 0.3s ease-out"
    >
      <span class="text-xl font-bold flex-shrink-0">{{ typeIcons[toast.type] }}</span>
      <p class="flex-1 text-sm font-medium">{{ toast.message }}</p>
      <button
        class="text-gray-500 hover:text-gray-700 flex-shrink-0 ml-2"
        aria-label="Close"
        @click="removeToast(toast.id)"
      >
        ✕
      </button>
    </div>
  </div>
</template>

<style scoped>
@keyframes slideIn {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}
</style>
