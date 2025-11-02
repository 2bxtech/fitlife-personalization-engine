import { defineStore } from 'pinia'
import { ref } from 'vue'
import { classService } from '@/services/classService'
import type { Class, ClassFilter } from '@/types/Class'

export const useClassStore = defineStore('classes', () => {
  const classes = ref<Class[]>([])
  const currentClass = ref<Class | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const total = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  async function fetchClasses(filters?: ClassFilter) {
    loading.value = true
    error.value = null
    try {
      const result = await classService.getClasses({
        ...filters,
        page: currentPage.value,
        pageSize: pageSize.value,
      })
      classes.value = result.data
      total.value = result.total
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch classes'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchClassById(id: string) {
    loading.value = true
    error.value = null
    try {
      currentClass.value = await classService.getClassById(id)
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch class'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function bookClass(classId: string) {
    try {
      await classService.bookClass(classId)
      // Refresh the class list
      await fetchClasses()
    } catch (e: any) {
      error.value = e.message || 'Failed to book class'
      throw e
    }
  }

  function setPage(page: number) {
    currentPage.value = page
  }

  return {
    classes,
    currentClass,
    loading,
    error,
    total,
    currentPage,
    pageSize,
    fetchClasses,
    fetchClassById,
    bookClass,
    setPage,
  }
})
