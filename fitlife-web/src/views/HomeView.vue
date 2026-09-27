<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

function navigateToDashboard() {
  if (authStore.isAuthenticated) {
    router.push('/dashboard')
  } else {
    router.push('/register')
  }
}
</script>

<template>
  <div class="min-h-screen bg-gradient-to-br from-primary-50 to-white">
    <div class="container mx-auto px-6 py-20">
      <div class="max-w-4xl mx-auto text-center">
        <h1 class="text-5xl font-bold text-gray-900 mb-6">
          Your Personalized Fitness Journey Starts Here
        </h1>
        <p class="text-xl text-gray-600 mb-12">
          Class recommendations you can audit: every suggestion shows the deterministic rules that ranked it.
        </p>
        
        <button class="px-8 py-4 bg-primary-700 text-white text-lg font-semibold rounded-lg hover:bg-primary-800 transition-colors shadow-lg" @click="navigateToDashboard">
          {{ authStore.isAuthenticated ? 'Go to Dashboard' : 'Get Started' }}
        </button>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-8 mt-20">
          <div class="bg-white p-8 rounded-lg shadow-md">
            <div class="text-4xl mb-4">🎯</div>
            <h3 class="text-xl font-bold mb-3">Personalized</h3>
            <p class="text-gray-600">Recommendations based on your fitness level, preferences, and booking history</p>
          </div>
          
          <div class="bg-white p-8 rounded-lg shadow-md">
            <div class="text-4xl mb-4">⚡</div>
            <h3 class="text-xl font-bold mb-3">Consistent booking</h3>
            <p class="text-gray-600">The database enforces capacity, so the last spot cannot be double-booked</p>
          </div>
          
          <div class="bg-white p-8 rounded-lg shadow-md">
            <div class="text-4xl mb-4">📊</div>
            <h3 class="text-xl font-bold mb-3">Explainable</h3>
            <p class="text-gray-600">Nine fixed rules, each showing the points it contributed. No trained model, no black box</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
