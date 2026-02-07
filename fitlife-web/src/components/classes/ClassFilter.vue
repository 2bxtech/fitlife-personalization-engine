<script setup lang="ts">
import { ref } from 'vue'
import type { ClassFilter } from '@/types/Class'

const emit = defineEmits<{
  filter: [filters: ClassFilter]
}>()

const filters = ref<ClassFilter>({
  type: '',
  level: '',
  startDate: '',
})

const classTypes = ['Yoga', 'Spin', 'HIIT', 'Strength', 'Pilates', 'Boxing', 'Cardio']
const levels = ['Beginner', 'Intermediate', 'Advanced']

function applyFilters() {
  const activeFilters: ClassFilter = {}
  if (filters.value.type) activeFilters.type = filters.value.type
  if (filters.value.level) activeFilters.level = filters.value.level
  if (filters.value.startDate) activeFilters.startDate = filters.value.startDate
  emit('filter', activeFilters)
}

function clearFilters() {
  filters.value = { type: '', level: '', startDate: '' }
  emit('filter', {})
}
</script>

<template>
  <div class="bg-white rounded-lg shadow-md p-6 mb-6">
    <h3 class="text-lg font-semibold mb-4">Filter Classes</h3>
    
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-2">Class Type</label>
        <select v-model="filters.type" class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500">
          <option value="">All Types</option>
          <option v-for="type in classTypes" :key="type" :value="type">{{ type }}</option>
        </select>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-2">Level</label>
        <select v-model="filters.level" class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500">
          <option value="">All Levels</option>
          <option v-for="lvl in levels" :key="lvl" :value="lvl">{{ lvl }}</option>
        </select>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-2">Start Date</label>
        <input 
          v-model="filters.startDate" 
          type="date" 
          class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
        />
      </div>
    </div>

    <div class="flex space-x-4">
      <button @click="applyFilters" class="px-6 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors">
        Apply Filters
      </button>
      <button @click="clearFilters" class="px-6 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition-colors">
        Clear
      </button>
    </div>
  </div>
</template>
