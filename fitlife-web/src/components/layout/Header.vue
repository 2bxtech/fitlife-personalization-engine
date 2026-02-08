<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()
const menuOpen = ref(false)

function handleLogout() {
  authStore.logout()
  menuOpen.value = false
  router.push('/login')
}

function closeMenu() {
  menuOpen.value = false
}
</script>

<template>
  <header class="bg-white shadow-md relative">
    <nav class="container mx-auto px-6 py-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center space-x-8">
          <router-link to="/" class="text-2xl font-bold text-primary-600">
            FitLife
          </router-link>
          <!-- Desktop nav links -->
          <div v-if="authStore.isAuthenticated" class="hidden md:flex space-x-4">
            <router-link
              to="/dashboard"
              class="px-3 py-2 rounded-lg text-gray-700 hover:text-primary-600 transition-colors"
              active-class="bg-primary-50 text-primary-700 font-semibold"
            >
              Dashboard
            </router-link>
            <router-link
              to="/classes"
              class="px-3 py-2 rounded-lg text-gray-700 hover:text-primary-600 transition-colors"
              active-class="bg-primary-50 text-primary-700 font-semibold"
            >
              Classes
            </router-link>
          </div>
        </div>
        
        <!-- Desktop right side -->
        <div class="hidden md:flex items-center space-x-4">
          <template v-if="authStore.isAuthenticated">
            <router-link
              to="/profile"
              class="px-3 py-2 rounded-lg text-gray-700 hover:text-primary-600 transition-colors"
              active-class="bg-primary-50 text-primary-700 font-semibold"
            >
              {{ authStore.user?.firstName }}
            </router-link>
            <button class="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition-colors" @click="handleLogout">
              Logout
            </button>
          </template>
          <template v-else>
            <router-link
              to="/login"
              class="px-3 py-2 rounded-lg text-gray-700 hover:text-primary-600 transition-colors"
              active-class="bg-primary-50 text-primary-700 font-semibold"
            >
              Login
            </router-link>
            <router-link to="/register" class="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors">
              Sign Up
            </router-link>
          </template>
        </div>

        <!-- Mobile hamburger button -->
        <button
          class="md:hidden p-2 rounded-lg text-gray-700 hover:bg-gray-100 transition-colors"
          aria-label="Toggle menu"
          @click="menuOpen = !menuOpen"
        >
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path v-if="!menuOpen" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
            <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Mobile menu -->
      <div v-if="menuOpen" class="md:hidden mt-4 pb-4 border-t border-gray-200 pt-4 space-y-2">
        <template v-if="authStore.isAuthenticated">
          <router-link
            to="/dashboard"
            class="block px-3 py-2 rounded-lg text-gray-700 hover:bg-primary-50 hover:text-primary-600 transition-colors"
            active-class="bg-primary-50 text-primary-700 font-semibold"
            @click="closeMenu"
          >
            Dashboard
          </router-link>
          <router-link
            to="/classes"
            class="block px-3 py-2 rounded-lg text-gray-700 hover:bg-primary-50 hover:text-primary-600 transition-colors"
            active-class="bg-primary-50 text-primary-700 font-semibold"
            @click="closeMenu"
          >
            Classes
          </router-link>
          <router-link
            to="/profile"
            class="block px-3 py-2 rounded-lg text-gray-700 hover:bg-primary-50 hover:text-primary-600 transition-colors"
            active-class="bg-primary-50 text-primary-700 font-semibold"
            @click="closeMenu"
          >
            Profile
          </router-link>
          <button
            class="w-full text-left px-3 py-2 rounded-lg text-gray-700 hover:bg-gray-100 transition-colors"
            @click="handleLogout"
          >
            Logout
          </button>
        </template>
        <template v-else>
          <router-link
            to="/login"
            class="block px-3 py-2 rounded-lg text-gray-700 hover:bg-primary-50 hover:text-primary-600 transition-colors"
            active-class="bg-primary-50 text-primary-700 font-semibold"
            @click="closeMenu"
          >
            Login
          </router-link>
          <router-link
            to="/register"
            class="block px-3 py-2 rounded-lg bg-primary-600 text-white text-center hover:bg-primary-700 transition-colors"
            @click="closeMenu"
          >
            Sign Up
          </router-link>
        </template>
      </div>
    </nav>
  </header>
</template>
