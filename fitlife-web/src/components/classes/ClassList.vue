<script setup lang="ts">
import ClassCard from './ClassCard.vue'
import type { Class } from '@/types/Class'

defineProps<{ classes: Class[]; loading?: boolean; isPending: (classId: string) => boolean }>()
const emit = defineEmits<{ book: [classId: string]; cancel: [classId: string] }>()
</script>

<template>
  <div>
    <div
      v-if="loading && classes.length === 0"
      class="grid gap-4 md:grid-cols-2 lg:grid-cols-3"
      aria-busy="true"
      aria-label="Loading classes"
    >
      <div v-for="i in 6" :key="i" class="h-64 animate-pulse rounded-2xl bg-slate-200" />
    </div>
    <p v-else-if="classes.length === 0" class="rounded-2xl bg-white p-8 text-center text-slate-600 ring-1 ring-slate-200">
      No classes match these filters.
    </p>
    <ul v-else class="grid gap-4 md:grid-cols-2 lg:grid-cols-3" :aria-busy="loading">
      <li v-for="classItem in classes" :key="classItem.id" class="flex">
        <ClassCard
          class="w-full"
          :class-data="classItem"
          :pending="isPending(classItem.id)"
          @book="emit('book', $event)"
          @cancel="emit('cancel', $event)"
        />
      </li>
    </ul>
  </div>
</template>
