<script setup lang="ts">
import { onMounted } from 'vue'
import { useClassStore } from '@/stores/classes'
import ClassFilter from '@/components/classes/ClassFilter.vue'
import ClassList from '@/components/classes/ClassList.vue'
import type { ClassFilter as ClassFilterType } from '@/types/Class'

const classStore = useClassStore()

onMounted(async () => {
  await classStore.fetchClasses()
})

async function handleFilter(filters: ClassFilterType) {
  classStore.setPage(1)
  await classStore.fetchClasses(filters)
}

async function handleBook(classId: string) {
  try {
    await classStore.bookClass(classId)
    alert('Class booked successfully!')
  } catch (error: any) {
    alert(error.message || 'Failed to book class')
  }
}

async function handlePageChange(page: number) {
  classStore.setPage(page)
  await classStore.fetchClasses()
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

      <!-- Pagination -->
      <div v-if="!classStore.loading && classStore.total > classStore.pageSize" class="mt-8 flex justify-center">
        <div class="flex space-x-2">
          <button
            v-for="page in Math.ceil(classStore.total / classStore.pageSize)"
            :key="page"
            @click="handlePageChange(page)"
            :class="[
              'px-4 py-2 rounded-lg transition-colors',
              page === classStore.currentPage
                ? 'bg-primary-600 text-white'
                : 'bg-white text-gray-700 hover:bg-gray-100'
            ]"
          >
            {{ page }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
