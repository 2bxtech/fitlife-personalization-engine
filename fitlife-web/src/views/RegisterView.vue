<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const formData = ref({
  email: '',
  password: '',
  confirmPassword: '',
  firstName: '',
  lastName: '',
  fitnessLevel: 'Beginner',
  goals: [] as string[],
  preferredClassTypes: [] as string[],
})

const error = ref('')
const loading = ref(false)

const fitnessLevels = ['Beginner', 'Intermediate', 'Advanced']
const availableGoals = ['Weight Loss', 'Muscle Building', 'Endurance', 'Flexibility', 'General Fitness']
const classTypes = ['Yoga', 'Spin', 'HIIT', 'Strength', 'Pilates', 'Boxing', 'Cardio']

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

async function handleRegister() {
  error.value = ''
  
  if (formData.value.password !== formData.value.confirmPassword) {
    error.value = 'Passwords do not match'
    return
  }
  
  if (formData.value.password.length < 8) {
    error.value = 'Password must be at least 8 characters'
    return
  }
  
  loading.value = true
  
  try {
    await authStore.register({
      email: formData.value.email,
      password: formData.value.password,
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      fitnessLevel: formData.value.fitnessLevel,
      goals: formData.value.goals,
      preferredClassTypes: formData.value.preferredClassTypes,
    })
    router.push('/dashboard')
  } catch (e: any) {
    error.value = e.response?.data?.error || 'Registration failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-2xl mx-auto">
      <div class="text-center mb-8">
        <h2 class="text-3xl font-extrabold text-gray-900">
          Create your account
        </h2>
        <p class="mt-2 text-sm text-gray-600">
          Already have an account?
          <router-link to="/login" class="font-medium text-primary-600 hover:text-primary-500">
            Sign in
          </router-link>
        </p>
      </div>
      
      <form class="bg-white shadow-md rounded-lg p-8 space-y-6" @submit.prevent="handleRegister">
        <div v-if="error" class="bg-red-50 border border-red-200 rounded-lg p-4">
          <p class="text-red-800 text-sm">{{ error }}</p>
        </div>

        <!-- Personal Information -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label for="firstName" class="block text-sm font-medium text-gray-700 mb-2">
              First Name
            </label>
            <input
              id="firstName"
              v-model="formData.firstName"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
          
          <div>
            <label for="lastName" class="block text-sm font-medium text-gray-700 mb-2">
              Last Name
            </label>
            <input
              id="lastName"
              v-model="formData.lastName"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
        </div>

        <div>
          <label for="email" class="block text-sm font-medium text-gray-700 mb-2">
            Email address
          </label>
          <input
            id="email"
            v-model="formData.email"
            type="email"
            required
            class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label for="password" class="block text-sm font-medium text-gray-700 mb-2">
              Password
            </label>
            <input
              id="password"
              v-model="formData.password"
              type="password"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
          
          <div>
            <label for="confirmPassword" class="block text-sm font-medium text-gray-700 mb-2">
              Confirm Password
            </label>
            <input
              id="confirmPassword"
              v-model="formData.confirmPassword"
              type="password"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
        </div>

        <!-- Fitness Profile -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-2">
            Fitness Level
          </label>
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
          <label class="block text-sm font-medium text-gray-700 mb-2">
            Fitness Goals (select all that apply)
          </label>
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
          <label class="block text-sm font-medium text-gray-700 mb-2">
            Preferred Class Types
          </label>
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

        <button
          type="submit"
          :disabled="loading"
          class="w-full py-3 px-4 bg-primary-600 text-white font-medium rounded-lg hover:bg-primary-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
        >
          {{ loading ? 'Creating account...' : 'Create account' }}
        </button>
      </form>
    </div>
  </div>
</template>
