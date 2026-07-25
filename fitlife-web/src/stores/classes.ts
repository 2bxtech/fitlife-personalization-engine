import { defineStore } from 'pinia'
import { ref } from 'vue'
import { classService } from '@/services/classService'
import type { Class, ClassFilter } from '@/types/Class'
import axios from 'axios'

export const useClassStore = defineStore('classes', () => {
  const classes = ref<Class[]>([])
  const currentClass = ref<Class | null>(null)
  const loading = ref(false)
  const actionClassId = ref<string | null>(null)
  const error = ref<string | null>(null)

  async function fetchClasses(filters?: ClassFilter) {
    loading.value = true
    error.value = null
    try {
      classes.value = await classService.getClasses(filters)
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
    actionClassId.value = classId
    error.value = null
    try {
      const result = await classService.bookClass(classId)
      updateClass(result.classData)
      return result.message
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Failed to book class')
      throw Object.assign(new Error(error.value), { cause: e })
    } finally {
      actionClassId.value = null
    }
  }

  async function cancelBooking(classId: string) {
    actionClassId.value = classId
    error.value = null
    try {
      const result = await classService.cancelBooking(classId)
      updateClass(result.classData)
      return result.message
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Failed to cancel booking')
      throw Object.assign(new Error(error.value), { cause: e })
    } finally {
      actionClassId.value = null
    }
  }

  function updateClass(updatedClass: Class) {
    const index = classes.value.findIndex(
      classItem => classItem.id === updatedClass.id
    )
    if (index >= 0) classes.value[index] = updatedClass
    if (currentClass.value?.id === updatedClass.id) {
      currentClass.value = updatedClass
    }
  }

  function getErrorMessage(error: unknown, fallback: string) {
    if (axios.isAxiosError(error)) {
      const responseMessage = (error.response?.data as { message?: string } | undefined)
        ?.message
      if (responseMessage) return responseMessage
    }

    return error instanceof Error ? error.message : fallback
  }

  return {
    classes,
    currentClass,
    loading,
    actionClassId,
    error,
    fetchClasses,
    fetchClassById,
    bookClass,
    cancelBooking,
  }
})
