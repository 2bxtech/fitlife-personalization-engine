<script setup lang="ts">
import { onMounted } from 'vue'
import { useClassStore } from '@/stores/classes'
import { useToast } from '@/composables/useToast'
import ClassFilter from '@/components/classes/ClassFilter.vue'
import ClassList from '@/components/classes/ClassList.vue'
import type { ClassFilter as ClassFilterType } from '@/types/Class'

const classStore = useClassStore()
const toast = useToast()

onMounted(async () => {
  await classStore.fetchClasses()
})

async function handleFilter(filters: ClassFilterType) {
  await classStore.fetchClasses(filters)
}

async function handleBook(classId: string) {
  try {
    await classStore.bookClass(classId)
    toast.success('Class booked successfully!')
  } catch (error: any) {
    toast.error(error.message || 'Failed to book class')
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 py-8">
    <div class="container mx-auto px-6">
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">Browse Classes</h1>
        <p class="text-gray-600 mt-2">
          Find the perfect class for your fitness journey
        </p>
      </div>

      <ClassFilter @filter="handleFilter" />

      <div v-if="classStore.error" class="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
        <p class="text-red-800">{{ classStore.error }}</p>
      </div>

      <ClassList 
        :classes="classStore.classes" 
        :loading="classStore.loading"
        @book="handleBook"
      />
    </div>
  </div>
</template>
