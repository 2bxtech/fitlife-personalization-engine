<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <header class="bg-white shadow-md">
    <nav class="container mx-auto px-6 py-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center space-x-8">
          <router-link to="/" class="text-2xl font-bold text-primary-600">
            FitLife
          </router-link>
          <div v-if="authStore.isAuthenticated" class="flex space-x-4">
            <router-link to="/dashboard" class="text-gray-700 hover:text-primary-600">
              Dashboard
            </router-link>
            <router-link to="/classes" class="text-gray-700 hover:text-primary-600">
              Classes
            </router-link>
          </div>
        </div>
        
        <div class="flex items-center space-x-4">
          <template v-if="authStore.isAuthenticated">
            <router-link to="/profile" class="text-gray-700 hover:text-primary-600">
              {{ authStore.user?.firstName }}
            </router-link>
            <button @click="handleLogout" class="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition-colors">
              Logout
            </button>
          </template>
          <template v-else>
            <router-link to="/login" class="text-gray-700 hover:text-primary-600">
              Login
            </router-link>
            <router-link to="/register" class="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors">
              Sign Up
            </router-link>
          </template>
        </div>
      </div>
    </nav>
  </header>
</template>
