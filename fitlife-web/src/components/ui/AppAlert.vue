<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    tone?: 'error' | 'warning' | 'info' | 'success'
    title?: string
  }>(),
  { tone: 'info', title: undefined }
)

const tones = {
  error: 'bg-red-50 text-red-900 ring-red-200',
  warning: 'bg-accent-50 text-accent-900 ring-accent-200',
  info: 'bg-primary-50 text-primary-900 ring-primary-200',
  success: 'bg-emerald-50 text-emerald-900 ring-emerald-200',
}

// Errors interrupt assistive technology; everything else is announced politely.
const role = computed(() => (props.tone === 'error' ? 'alert' : 'status'))
</script>

<template>
  <div :role="role" :class="['flex flex-col gap-3 rounded-xl p-4 ring-1 ring-inset sm:flex-row sm:items-center', tones[tone]]">
    <div class="flex-1 text-sm">
      <p v-if="title" class="font-semibold">{{ title }}</p>
      <div :class="title ? 'mt-1' : ''"><slot /></div>
    </div>
    <div v-if="$slots.action" class="shrink-0">
      <slot name="action" />
    </div>
  </div>
</template>
