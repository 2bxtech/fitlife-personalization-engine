<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useClassStore } from '@/stores/classes'
import { useToast } from '@/composables/useToast'
import ClassFilter from '@/components/classes/ClassFilter.vue'
import ClassList from '@/components/classes/ClassList.vue'
import AppAlert from '@/components/ui/AppAlert.vue'
import AppButton from '@/components/ui/AppButton.vue'
import type { ClassFilter as ClassFilterType } from '@/types/Class'

const classStore = useClassStore()
const toast = useToast()
const activeFilters = ref<ClassFilterType>({})

async function load(filters: ClassFilterType = activeFilters.value) {
  activeFilters.value = filters
  // The store records the error; the view renders it with a retry.
  await classStore.fetchClasses(filters).catch(() => undefined)
}

async function handleAction(classId: string, action: 'book' | 'cancel') {
  try {
    const result =
      action === 'book' ? await classStore.bookClass(classId) : await classStore.cancelBooking(classId)
    toast.success(result.message)
  } catch (error: unknown) {
    toast.error(error instanceof Error ? error.message : 'That did not work. Please try again.')
  }
}

onMounted(() => load())
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <h1 class="text-3xl font-bold tracking-tight text-slate-900">All classes</h1>
    <p class="mt-1 text-slate-600">The full upcoming schedule. Your ranked picks are on the Recommendations page.</p>

    <div class="mt-6"><ClassFilter @filter="load" /></div>

    <AppAlert v-if="classStore.error" tone="error" title="Classes could not be loaded" class="mt-6">
      {{ classStore.error }}
      <template #action><AppButton variant="secondary" size="sm" @click="load()">Try again</AppButton></template>
    </AppAlert>

    <div class="mt-6">
      <ClassList
        :classes="classStore.classes"
        :loading="classStore.loading"
        :is-pending="classStore.isPending"
        @book="handleAction($event, 'book')"
        @cancel="handleAction($event, 'cancel')"
      />
    </div>
  </div>
</template>
