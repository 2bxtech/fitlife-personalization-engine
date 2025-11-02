<template>
  <footer class="bg-gray-800 text-white mt-auto">
    <div class="container mx-auto px-6 py-8">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-8">
        <div>
          <h3 class="text-xl font-bold mb-4">FitLife</h3>
          <p class="text-gray-400">
            Personalized gym class recommendations powered by AI
          </p>
        </div>
        <div>
          <h4 class="font-semibold mb-4">Quick Links</h4>
          <ul class="space-y-2 text-gray-400">
            <li><router-link to="/classes" class="hover:text-white">Browse Classes</router-link></li>
            <li><router-link to="/dashboard" class="hover:text-white">My Dashboard</router-link></li>
          </ul>
        </div>
        <div>
          <h4 class="font-semibold mb-4">Support</h4>
          <ul class="space-y-2 text-gray-400">
            <li><a href="#" class="hover:text-white">Help Center</a></li>
            <li><a href="#" class="hover:text-white">Contact Us</a></li>
          </ul>
        </div>
      </div>
      <div class="border-t border-gray-700 mt-8 pt-6 text-center text-gray-400">
        <p>&copy; 2025 FitLife Personalization Engine. All rights reserved.</p>
      </div>
    </div>
  </footer>
</template>
