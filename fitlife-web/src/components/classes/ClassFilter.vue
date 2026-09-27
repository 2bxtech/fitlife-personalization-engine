<script setup lang="ts">
import { ref } from 'vue'
import type { ClassFilter } from '@/types/Class'
import AppButton from '@/components/ui/AppButton.vue'

const emit = defineEmits<{ filter: [filters: ClassFilter] }>()

const filters = ref({ type: '', level: '', startDate: '' })
const classTypes = ['Yoga', 'Pilates', 'HIIT', 'Strength', 'Spin', 'Walking']
const levels = ['Beginner', 'Intermediate', 'Advanced', 'All Levels']
const fieldClass =
  'mt-1 block w-full rounded-lg border-0 bg-white px-3 py-2 text-sm text-slate-900 ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-primary-700'

function applyFilters() {
  const active: ClassFilter = {}
  if (filters.value.type) active.type = filters.value.type
  if (filters.value.level) active.level = filters.value.level
  if (filters.value.startDate) active.startDate = filters.value.startDate
  emit('filter', active)
}

function clearFilters() {
  filters.value = { type: '', level: '', startDate: '' }
  emit('filter', {})
}
</script>

<template>
  <form class="rounded-2xl bg-white p-5 ring-1 ring-slate-200" aria-labelledby="filter-title" @submit.prevent="applyFilters">
    <h2 id="filter-title" class="text-sm font-semibold text-slate-900">Filter classes</h2>
    <div class="mt-3 grid gap-4 sm:grid-cols-3">
      <div>
        <label for="filter-type" class="text-sm font-medium text-slate-700">Class type</label>
        <select id="filter-type" v-model="filters.type" :class="fieldClass">
          <option value="">All types</option>
          <option v-for="type in classTypes" :key="type" :value="type">{{ type }}</option>
        </select>
      </div>
      <div>
        <label for="filter-level" class="text-sm font-medium text-slate-700">Level</label>
        <select id="filter-level" v-model="filters.level" :class="fieldClass">
          <option value="">Any level</option>
          <option v-for="level in levels" :key="level" :value="level">{{ level }}</option>
        </select>
      </div>
      <div>
        <label for="filter-date" class="text-sm font-medium text-slate-700">On or after</label>
        <input id="filter-date" v-model="filters.startDate" type="date" :class="fieldClass" />
      </div>
    </div>
    <div class="mt-4 flex gap-2">
      <AppButton type="submit" size="sm">Apply filters</AppButton>
      <AppButton variant="ghost" size="sm" @click="clearFilters">Clear</AppButton>
    </div>
  </form>
</template>
