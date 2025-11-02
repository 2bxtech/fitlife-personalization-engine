<script setup lang="ts">
import { computed } from 'vue'
import ClassCard from './ClassCard.vue'
import type { Class } from '@/types/Class'

const props = defineProps<{
  classes: Class[]
  loading?: boolean
}>()

const emit = defineEmits<{
  book: [classId: string]
}>()

const handleBook = (classId: string) => {
  emit('book', classId)
}
</script>

<template>
  <div>
    <div v-if="loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <div v-for="i in 6" :key="i" class="bg-gray-200 h-64 rounded-lg animate-pulse"></div>
    </div>

    <div v-else-if="classes.length === 0" class="text-center py-12">
      <p class="text-gray-600 text-lg">No classes found matching your criteria</p>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <ClassCard
        v-for="classItem in classes"
        :key="classItem.id"
        :class-data="classItem"
        @book="handleBook"
      />
    </div>
  </div>
</template>
