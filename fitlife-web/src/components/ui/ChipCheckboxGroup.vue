<script setup lang="ts">
import { useId } from 'vue'

/**
 * A multi-select rendered as chips. Each chip is a native checkbox inside a
 * fieldset, so the group name, each option's label, and its checked state are
 * exposed to assistive technology without custom ARIA.
 */
defineProps<{ legend: string; options: readonly string[]; hint?: string }>()
const selected = defineModel<string[]>({ required: true })
const id = useId()
</script>

<template>
  <fieldset>
    <legend class="text-sm font-medium text-slate-800">{{ legend }}</legend>
    <p v-if="hint" :id="`${id}-hint`" class="mt-0.5 text-sm text-slate-600">{{ hint }}</p>
    <div class="mt-2 flex flex-wrap gap-2">
      <label
        v-for="option in options"
        :key="option"
        class="relative cursor-pointer rounded-full px-3 py-1.5 text-sm font-medium ring-1 ring-inset transition-colors has-[:checked]:bg-primary-700 has-[:checked]:text-white has-[:checked]:ring-primary-700 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-primary-700"
        :class="'bg-white text-slate-700 ring-slate-300 hover:bg-slate-50'"
      >
        <input
          v-model="selected"
          type="checkbox"
          :value="option"
          class="sr-only"
          :aria-describedby="hint ? `${id}-hint` : undefined"
        />
        {{ option }}
      </label>
    </div>
  </fieldset>
</template>
