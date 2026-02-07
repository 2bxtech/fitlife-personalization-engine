<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRecommendationStore } from '@/stores/recommendations'
import { useToast } from '@/composables/useToast'
import { userService } from '@/services/userService'

const authStore = useAuthStore()
const recommendationStore = useRecommendationStore()
const toast = useToast()

const editing = ref(false)
const saving = ref(false)
const formData = ref({
  firstName: authStore.user?.firstName || '',
  lastName: authStore.user?.lastName || '',
  fitnessLevel: authStore.user?.fitnessLevel || 'Beginner',
  goals: [...(authStore.user?.goals || [])],
  preferredClassTypes: [...(authStore.user?.preferredClassTypes || [])],
})

const fitnessLevels = ['Beginner', 'Intermediate', 'Advanced']
const availableGoals = ['Weight Loss', 'Muscle Building', 'Endurance', 'Flexibility', 'General Fitness']
const classTypes = ['Yoga', 'Spin', 'HIIT', 'Strength', 'Pilates', 'Boxing', 'Cardio']

function startEditing() {
  editing.value = true
  formData.value = {
    firstName: authStore.user?.firstName || '',
    lastName: authStore.user?.lastName || '',
    fitnessLevel: authStore.user?.fitnessLevel || 'Beginner',
    goals: [...(authStore.user?.goals || [])],
    preferredClassTypes: [...(authStore.user?.preferredClassTypes || [])],
  }
}

function cancelEditing() {
  editing.value = false
}

async function saveProfile() {
  if (!authStore.user) return
  saving.value = true
  try {
    const updatedUser = await userService.updatePreferences(authStore.user.id, {
      fitnessLevel: formData.value.fitnessLevel,
      goals: formData.value.goals,
      preferredClassTypes: formData.value.preferredClassTypes,
    })

    // Update local state with server response
    authStore.user.fitnessLevel = updatedUser.fitnessLevel
    authStore.user.goals = updatedUser.goals
    authStore.user.preferredClassTypes = updatedUser.preferredClassTypes
    authStore.user.segment = updatedUser.segment
    localStorage.setItem('user', JSON.stringify(authStore.user))

    // Refresh recommendations since preferences changed
    await recommendationStore.refreshRecommendations(authStore.user.id)

    editing.value = false
    toast.success('Profile updated successfully!')
  } catch (error: any) {
    toast.error(error.message || 'Failed to update profile')
  } finally {
    saving.value = false
  }
}

function toggleGoal(goal: string) {
  const index = formData.value.goals.indexOf(goal)
  if (index > -1) {
    formData.value.goals.splice(index, 1)
  } else {
    formData.value.goals.push(goal)
  }
}

function toggleClassType(type: string) {
  const index = formData.value.preferredClassTypes.indexOf(type)
  if (index > -1) {
    formData.value.preferredClassTypes.splice(index, 1)
  } else {
    formData.value.preferredClassTypes.push(type)
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 py-8">
    <div class="container mx-auto px-6 max-w-4xl">
      <div class="bg-white rounded-lg shadow-md p-8">
        <div class="flex justify-between items-center mb-6">
          <h1 class="text-3xl font-bold text-gray-900">My Profile</h1>
          <button
            v-if="!editing"
            @click="startEditing"
            class="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
          >
            Edit Profile
          </button>
        </div>

        <!-- View Mode -->
        <div v-if="!editing" class="space-y-6">
          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Name</h2>
            <p class="text-lg text-gray-900">{{ authStore.user?.firstName }} {{ authStore.user?.lastName }}</p>
          </div>

          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Email</h2>
            <p class="text-lg text-gray-900">{{ authStore.user?.email }}</p>
          </div>

          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Fitness Level</h2>
            <p class="text-lg text-gray-900">{{ authStore.user?.fitnessLevel }}</p>
          </div>

          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Current Segment</h2>
            <p class="text-lg text-gray-900">{{ authStore.user?.segment || 'General' }}</p>
          </div>

          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Fitness Goals</h2>
            <div class="flex flex-wrap gap-2 mt-2">
              <span
                v-for="goal in authStore.user?.goals"
                :key="goal"
                class="px-3 py-1 bg-primary-100 text-primary-700 rounded-full text-sm"
              >
                {{ goal }}
              </span>
              <span v-if="!authStore.user?.goals?.length" class="text-gray-500">None set</span>
            </div>
          </div>

          <div>
            <h2 class="text-sm font-medium text-gray-500 mb-1">Preferred Class Types</h2>
            <div class="flex flex-wrap gap-2 mt-2">
              <span
                v-for="type in authStore.user?.preferredClassTypes"
                :key="type"
                class="px-3 py-1 bg-primary-100 text-primary-700 rounded-full text-sm"
              >
                {{ type }}
              </span>
              <span v-if="!authStore.user?.preferredClassTypes?.length" class="text-gray-500">None set</span>
            </div>
          </div>
        </div>

        <!-- Edit Mode -->
        <form v-else @submit.prevent="saveProfile" class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-2">First Name</label>
              <input
                v-model="formData.firstName"
                type="text"
                required
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
              />
            </div>
            
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-2">Last Name</label>
              <input
                v-model="formData.lastName"
                type="text"
                required
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
              />
            </div>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">Fitness Level</label>
            <select
              v-model="formData.fitnessLevel"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            >
              <option v-for="level in fitnessLevels" :key="level" :value="level">
                {{ level }}
              </option>
            </select>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">Fitness Goals</label>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="goal in availableGoals"
                :key="goal"
                type="button"
                @click="toggleGoal(goal)"
                :class="[
                  'px-4 py-2 rounded-lg border transition-colors',
                  formData.goals.includes(goal)
                    ? 'bg-primary-600 text-white border-primary-600'
                    : 'bg-white text-gray-700 border-gray-300 hover:border-primary-500'
                ]"
              >
                {{ goal }}
              </button>
            </div>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">Preferred Class Types</label>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="type in classTypes"
                :key="type"
                type="button"
                @click="toggleClassType(type)"
                :class="[
                  'px-4 py-2 rounded-lg border transition-colors',
                  formData.preferredClassTypes.includes(type)
                    ? 'bg-primary-600 text-white border-primary-600'
                    : 'bg-white text-gray-700 border-gray-300 hover:border-primary-500'
                ]"
              >
                {{ type }}
              </button>
            </div>
          </div>

          <div class="flex space-x-4">
            <button
              type="submit"
              :disabled="saving"
              class="px-6 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:bg-gray-300 transition-colors"
            >
              {{ saving ? 'Saving...' : 'Save Changes' }}
            </button>
            <button
              type="button"
              @click="cancelEditing"
              :disabled="saving"
              class="px-6 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition-colors"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
