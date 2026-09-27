import { defineStore } from 'pinia'
import { ref } from 'vue'
import { classService } from '@/services/classService'
import type { Class, ClassFilter } from '@/types/Class'
import axios from 'axios'

export const useClassStore = defineStore('classes', () => {
  const classes = ref<Class[]>([])
  const currentClass = ref<Class | null>(null)
  const loading = ref(false)
  /** Classes with a booking or cancellation in flight; each card tracks its own. */
  const pendingIds = ref<Set<string>>(new Set())
  const error = ref<string | null>(null)

  // Filter changes can overlap; only the newest request may update the list.
  let latestRequest = 0
  // Bumped by reset() (sign-in, persona switch, sign-out). An action that
  // started under an earlier session must not apply its result to this one.
  let generation = 0

  async function fetchClasses(filters?: ClassFilter) {
    const request = ++latestRequest
    loading.value = true
    error.value = null
    try {
      const result = await classService.getClasses(filters)
      if (request === latestRequest) classes.value = result
    } catch (e: unknown) {
      if (request === latestRequest) error.value = getErrorMessage(e, 'Failed to fetch classes')
      throw e
    } finally {
      if (request === latestRequest) loading.value = false
    }
  }

  async function fetchClassById(id: string) {
    loading.value = true
    error.value = null
    try {
      currentClass.value = await classService.getClassById(id)
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Failed to fetch class')
      throw e
    } finally {
      loading.value = false
    }
  }

  function isPending(classId: string) {
    return pendingIds.value.has(classId)
  }

  async function runAction(
    classId: string,
    action: (id: string) => Promise<{ classData: Class; message: string }>,
    fallback: string
  ) {
    const startedIn = generation
    pendingIds.value = new Set(pendingIds.value).add(classId)
    try {
      const result = await action(classId)
      const current = startedIn === generation
      if (current) updateClass(result.classData)
      return { ...result, current }
    } catch (e: unknown) {
      throw Object.assign(new Error(getErrorMessage(e, fallback)), { cause: e })
    } finally {
      if (startedIn === generation) {
        const next = new Set(pendingIds.value)
        next.delete(classId)
        pendingIds.value = next
      }
    }
  }

  /** Books a class; resolves with the updated class and the API's message. */
  function bookClass(classId: string) {
    return runAction(classId, classService.bookClass, 'Failed to book class')
  }

  function cancelBooking(classId: string) {
    return runAction(classId, classService.cancelBooking, 'Failed to cancel booking')
  }

  function updateClass(updatedClass: Class) {
    const index = classes.value.findIndex((classItem) => classItem.id === updatedClass.id)
    if (index >= 0) classes.value[index] = updatedClass
    if (currentClass.value?.id === updatedClass.id) currentClass.value = updatedClass
  }

  function getErrorMessage(error: unknown, fallback: string) {
    if (axios.isAxiosError(error)) {
      const responseMessage = (error.response?.data as { message?: string } | undefined)?.message
      if (responseMessage) return responseMessage
    }
    return error instanceof Error ? error.message : fallback
  }

  function reset() {
    latestRequest++
    generation++
    classes.value = []
    currentClass.value = null
    loading.value = false
    pendingIds.value = new Set()
    error.value = null
  }

  return {
    classes,
    currentClass,
    loading,
    pendingIds,
    error,
    fetchClasses,
    fetchClassById,
    isPending,
    bookClass,
    cancelBooking,
    reset,
  }
})
