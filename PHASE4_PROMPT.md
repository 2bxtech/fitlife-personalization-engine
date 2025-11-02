# Phase 4: Frontend & Production Readiness - Implementation Prompt

## 🎯 Objective

Build a production-ready Vue.js 3 SPA frontend and prepare the entire system for deployment to Azure Kubernetes Service (AKS).

## Prerequisites

**Phase 3 Status**: ✅ Complete
- Core recommendation engine operational
- Background workers running (EventConsumer, RecommendationGenerator, UserProfiler)
- API endpoints functional with JWT authentication
- 31/31 tests passing

**Current System State:**
- Backend API: .NET 8.0 running on `localhost:5269`
- Database: SQL Server in Docker
- Cache: Redis in Docker
- Events: Kafka + Zookeeper in Docker
- All infrastructure healthy

## 📦 Phase 4 Deliverables

### Part A: Frontend Vue.js SPA
1. **Project Setup**
   - Initialize Vite + Vue 3 + TypeScript
   - Install core dependencies (Pinia, Vue Router, Axios, Tailwind CSS)
   - Configure build and dev server

2. **Authentication Flow**
   - Login/Register views
   - JWT token management (localStorage)
   - Auth guard for protected routes
   - Axios interceptors for token injection

3. **Core Features**
   - User dashboard with personalized recommendations
   - Class catalog with filters (type, instructor, time)
   - Class detail view with booking
   - User profile management
   - Event tracking integration (View, Click, Book)

4. **State Management**
   - Pinia store for auth (`authStore`)
   - Pinia store for classes (`classStore`)
   - Pinia store for recommendations (`recommendationStore`)

5. **Components**
   - `ClassCard.vue` - Display class with rating, instructor
   - `ClassList.vue` - Grid/list of classes with pagination
   - `ClassFilter.vue` - Filter by type, time, instructor
   - `RecommendationFeed.vue` - Personalized recommendations
   - `Header.vue` / `Footer.vue` - Layout components

### Part B: API Enhancements
6. **Middleware**
   - Rate limiting (10 req/sec per user)
   - Request correlation IDs for tracing
   - CORS configuration for frontend

7. **Documentation**
   - Swagger/OpenAPI with JWT authorization
   - API versioning (v1)
   - Health check endpoints enhanced

8. **Observability**
   - Structured logging with Serilog (file + console)
   - Custom metrics (recommendation quality, cache hit rate)
   - Performance monitoring

### Part C: Production Readiness
9. **Containerization**
   - Dockerfile for API (multi-stage build)
   - Dockerfile for Vue.js (Nginx serving static build)
   - Docker Compose for local full-stack testing

10. **Kubernetes Manifests**
    - Namespace configuration
    - ConfigMaps (appsettings)
    - Secrets (JWT key, connection strings)
    - Deployments (API, Web, Workers)
    - Services (ClusterIP, LoadBalancer)
    - Ingress with SSL/TLS
    - HPA (Horizontal Pod Autoscaler)

11. **CI/CD Pipeline**
    - GitHub Actions workflow
    - Build + test + push to ACR (Azure Container Registry)
    - Deploy to AKS staging
    - Manual approval for production

12. **Documentation**
    - Deployment guide (`docs/DEPLOYMENT.md` update)
    - Runbook for common operations
    - Troubleshooting guide

---

## 📋 Detailed Implementation Steps

### PART A: Frontend Vue.js SPA

#### A1. Project Initialization

```bash
# From repository root
npm create vite@latest fitlife-web -- --template vue-ts
cd fitlife-web

# Install core dependencies
npm install pinia vue-router axios
npm install @vueuse/core  # Composables for Vue 3

# Install UI/styling
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

# Install dev tools
npm install -D @types/node
```

**File**: `fitlife-web/vite.config.ts`
```typescript
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5269',
        changeOrigin: true,
      },
    },
  },
})
```

**File**: `fitlife-web/tailwind.config.js`
```javascript
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        'fitlife-blue': '#0066CC',
        'fitlife-green': '#00CC66',
      },
    },
  },
  plugins: [],
}
```

#### A2. Project Structure

Create this folder structure in `fitlife-web/src/`:

```
src/
├── main.ts                    # Vue app entry point
├── App.vue                    # Root component
├── router/
│   └── index.ts               # Vue Router setup
├── stores/
│   ├── auth.ts                # Auth store (Pinia)
│   ├── classes.ts             # Classes store
│   └── recommendations.ts      # Recommendations store
├── services/
│   ├── api.ts                 # Axios instance + interceptors
│   ├── authService.ts         # Auth API calls
│   ├── classService.ts        # Class API calls
│   └── recommendationService.ts
├── types/
│   ├── User.ts                # TypeScript interfaces
│   ├── Class.ts
│   ├── Recommendation.ts
│   └── Event.ts
├── components/
│   ├── layout/
│   │   ├── Header.vue
│   │   └── Footer.vue
│   ├── classes/
│   │   ├── ClassCard.vue
│   │   ├── ClassList.vue
│   │   └── ClassFilter.vue
│   └── recommendations/
│       └── RecommendationFeed.vue
└── views/
    ├── HomeView.vue           # Landing page
    ├── LoginView.vue          # Login form
    ├── RegisterView.vue       # Registration form
    ├── DashboardView.vue      # User dashboard
    ├── ClassesView.vue        # Class catalog
    └── ProfileView.vue        # User profile
```

#### A3. TypeScript Types

**File**: `fitlife-web/src/types/User.ts`
```typescript
export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  fitnessLevel: 'Beginner' | 'Intermediate' | 'Advanced'
  goals?: string[]
  preferredClassTypes?: string[]
  favoriteInstructors?: string[]
  segment?: string
  createdAt: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  fitnessLevel: string
}

export interface AuthResponse {
  token: string
  user: User
}
```

**File**: `fitlife-web/src/types/Class.ts`
```typescript
export interface Class {
  id: string
  name: string
  type: string
  description?: string
  instructorId: string
  instructorName?: string
  startTime: string
  endTime: string
  duration: number
  capacity: number
  currentEnrollment: number
  location?: string
  averageRating: number
  difficultyLevel: 'Beginner' | 'Intermediate' | 'Advanced'
  isActive: boolean
}
```

**File**: `fitlife-web/src/types/Recommendation.ts`
```typescript
export interface Recommendation {
  userId: string
  itemId: string
  score: number
  rank: number
  reason?: string
  generatedAt: string
  classDetails?: Class
}
```

#### A4. API Service Layer

**File**: `fitlife-web/src/services/api.ts`
```typescript
import axios from 'axios'
import { useAuthStore } from '@/stores/auth'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor: Add JWT token
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor: Handle 401 (logout)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const authStore = useAuthStore()
      authStore.logout()
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default api
```

**File**: `fitlife-web/src/services/authService.ts`
```typescript
import api from './api'
import type { LoginRequest, RegisterRequest, AuthResponse } from '@/types/User'

export const authService = {
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>('/auth/login', credentials)
    return data
  },

  async register(userData: RegisterRequest): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>('/auth/register', userData)
    return data
  },

  async getCurrentUser() {
    const { data } = await api.get('/users/me')
    return data
  },
}
```

#### A5. Pinia Stores

**File**: `fitlife-web/src/stores/auth.ts`
```typescript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authService } from '@/services/authService'
import type { User, LoginRequest, RegisterRequest } from '@/types/User'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))
  const user = ref<User | null>(null)

  const isAuthenticated = computed(() => !!token.value)

  async function login(credentials: LoginRequest) {
    const response = await authService.login(credentials)
    token.value = response.token
    user.value = response.user
    localStorage.setItem('token', response.token)
  }

  async function register(userData: RegisterRequest) {
    const response = await authService.register(userData)
    token.value = response.token
    user.value = response.user
    localStorage.setItem('token', response.token)
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem('token')
  }

  return {
    token,
    user,
    isAuthenticated,
    login,
    register,
    logout,
  }
})
```

**File**: `fitlife-web/src/stores/recommendations.ts`
```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'
import type { Recommendation } from '@/types/Recommendation'

export const useRecommendationStore = defineStore('recommendations', () => {
  const recommendations = ref<Recommendation[]>([])
  const loading = ref(false)

  async function fetchRecommendations(userId: string, limit = 10) {
    loading.value = true
    try {
      const { data } = await api.get(`/recommendations/${userId}?limit=${limit}`)
      recommendations.value = data.data
    } finally {
      loading.value = false
    }
  }

  async function refreshRecommendations(userId: string) {
    loading.value = true
    try {
      const { data } = await api.post(`/recommendations/${userId}/refresh`)
      recommendations.value = data.data
    } finally {
      loading.value = false
    }
  }

  async function trackEvent(eventData: {
    userId: string
    itemId: string
    eventType: string
    metadata?: Record<string, any>
  }) {
    await api.post('/events', eventData)
  }

  return {
    recommendations,
    loading,
    fetchRecommendations,
    refreshRecommendations,
    trackEvent,
  }
})
```

#### A6. Key Components

**File**: `fitlife-web/src/components/classes/ClassCard.vue`
```vue
<script setup lang="ts">
import { computed } from 'vue'
import type { Class } from '@/types/Class'
import { useRecommendationStore } from '@/stores/recommendations'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{
  classData: Class
}>()

const emit = defineEmits<{
  book: [classId: string]
}>()

const authStore = useAuthStore()
const recommendationStore = useRecommendationStore()

const availabilityPercent = computed(() => 
  ((props.classData.capacity - props.classData.currentEnrollment) / props.classData.capacity) * 100
)

const availabilityColor = computed(() => {
  if (availabilityPercent.value < 20) return 'text-red-600'
  if (availabilityPercent.value < 50) return 'text-yellow-600'
  return 'text-green-600'
})

async function handleView() {
  if (authStore.user) {
    await recommendationStore.trackEvent({
      userId: authStore.user.id,
      itemId: props.classData.id,
      eventType: 'View',
      metadata: { source: 'browse' }
    })
  }
}

async function handleBook() {
  emit('book', props.classData.id)
  if (authStore.user) {
    await recommendationStore.trackEvent({
      userId: authStore.user.id,
      itemId: props.classData.id,
      eventType: 'Book'
    })
  }
}

onMounted(() => {
  handleView()
})
</script>

<template>
  <div class="bg-white rounded-lg shadow-md p-6 hover:shadow-lg transition-shadow">
    <h3 class="text-xl font-bold mb-2">{{ classData.name }}</h3>
    <p class="text-gray-600 mb-2">{{ classData.type }} • {{ classData.instructorName }}</p>
    <p class="text-sm text-gray-500 mb-4">
      {{ new Date(classData.startTime).toLocaleString() }}
    </p>
    
    <div class="flex items-center gap-2 mb-4">
      <span class="text-yellow-500">★</span>
      <span>{{ classData.averageRating.toFixed(1) }}</span>
      <span class="text-gray-400">|</span>
      <span :class="availabilityColor">
        {{ classData.capacity - classData.currentEnrollment }} spots left
      </span>
    </div>

    <button 
      @click="handleBook"
      :disabled="classData.currentEnrollment >= classData.capacity"
      class="w-full bg-fitlife-blue text-white py-2 rounded-lg hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed"
    >
      {{ classData.currentEnrollment >= classData.capacity ? 'Full' : 'Book Class' }}
    </button>
  </div>
</template>
```

**File**: `fitlife-web/src/components/recommendations/RecommendationFeed.vue`
```vue
<script setup lang="ts">
import { onMounted } from 'vue'
import { useRecommendationStore } from '@/stores/recommendations'
import { useAuthStore } from '@/stores/auth'
import ClassCard from '@/components/classes/ClassCard.vue'

const recommendationStore = useRecommendationStore()
const authStore = useAuthStore()

onMounted(async () => {
  if (authStore.user) {
    await recommendationStore.fetchRecommendations(authStore.user.id)
  }
})

async function handleRefresh() {
  if (authStore.user) {
    await recommendationStore.refreshRecommendations(authStore.user.id)
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex justify-between items-center">
      <h2 class="text-2xl font-bold">Recommended for You</h2>
      <button 
        @click="handleRefresh"
        class="px-4 py-2 bg-fitlife-green text-white rounded-lg hover:bg-green-600"
      >
        Refresh
      </button>
    </div>

    <div v-if="recommendationStore.loading" class="text-center py-8">
      <p class="text-gray-500">Loading recommendations...</p>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <div v-for="rec in recommendationStore.recommendations" :key="rec.itemId">
        <ClassCard 
          v-if="rec.classDetails" 
          :class-data="rec.classDetails"
        />
        <p v-if="rec.reason" class="text-sm text-gray-600 mt-2">
          💡 {{ rec.reason }}
        </p>
      </div>
    </div>

    <div v-if="!recommendationStore.loading && recommendationStore.recommendations.length === 0" 
         class="text-center py-8">
      <p class="text-gray-500">No recommendations yet. Book a class to get personalized suggestions!</p>
    </div>
  </div>
</template>
```

#### A7. Views

**File**: `fitlife-web/src/views/LoginView.vue`
```vue
<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function handleLogin() {
  error.value = ''
  loading.value = true
  
  try {
    await authStore.login({ email: email.value, password: password.value })
    router.push('/dashboard')
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Login failed'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50">
    <div class="max-w-md w-full bg-white rounded-lg shadow-md p-8">
      <h2 class="text-3xl font-bold text-center mb-6">FitLife Login</h2>
      
      <form @submit.prevent="handleLogin" class="space-y-4">
        <div>
          <label class="block text-sm font-medium mb-2">Email</label>
          <input 
            v-model="email" 
            type="email" 
            required 
            class="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-fitlife-blue"
          />
        </div>

        <div>
          <label class="block text-sm font-medium mb-2">Password</label>
          <input 
            v-model="password" 
            type="password" 
            required 
            class="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-fitlife-blue"
          />
        </div>

        <div v-if="error" class="text-red-600 text-sm">{{ error }}</div>

        <button 
          type="submit" 
          :disabled="loading"
          class="w-full bg-fitlife-blue text-white py-2 rounded-lg hover:bg-blue-700 disabled:bg-gray-300"
        >
          {{ loading ? 'Logging in...' : 'Login' }}
        </button>
      </form>

      <p class="text-center mt-4 text-sm">
        Don't have an account? 
        <router-link to="/register" class="text-fitlife-blue hover:underline">
          Register
        </router-link>
      </p>
    </div>
  </div>
</template>
```

**File**: `fitlife-web/src/views/DashboardView.vue`
```vue
<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import RecommendationFeed from '@/components/recommendations/RecommendationFeed.vue'

const authStore = useAuthStore()
</script>

<template>
  <div class="container mx-auto px-4 py-8">
    <div class="mb-8">
      <h1 class="text-3xl font-bold">Welcome back, {{ authStore.user?.firstName }}!</h1>
      <p class="text-gray-600">Your personalized fitness dashboard</p>
    </div>

    <RecommendationFeed />
  </div>
</template>
```

---

### PART B: API Enhancements

#### B1. Rate Limiting Middleware

**File**: `FitLife.Api/Middleware/RateLimitingMiddleware.cs`
```csharp
using System.Collections.Concurrent;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _limiters = new();
    private const int RequestsPerSecond = 10;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString();
        
        if (!string.IsNullOrEmpty(userId))
        {
            var limiter = _limiters.GetOrAdd(userId, _ => new SemaphoreSlim(RequestsPerSecond, RequestsPerSecond));
            
            if (!await limiter.WaitAsync(TimeSpan.Zero))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded" });
                return;
            }

            _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => limiter.Release());
        }

        await _next(context);
    }
}
```

#### B2. Correlation ID Middleware

**File**: `FitLife.Api/Middleware/CorrelationIdMiddleware.cs`
```csharp
using Serilog.Context;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

#### B3. Enhanced Swagger Configuration

Update `Program.cs`:
```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FitLife Personalization API",
        Version = "v1",
        Description = "Gym class recommendation engine with real-time personalization"
    });

    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
```

---

### PART C: Production Readiness

#### C1. API Dockerfile

**File**: `FitLife.Api/Dockerfile`
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["FitLife.Api/FitLife.Api.csproj", "FitLife.Api/"]
COPY ["FitLife.Core/FitLife.Core.csproj", "FitLife.Core/"]
COPY ["FitLife.Infrastructure/FitLife.Infrastructure.csproj", "FitLife.Infrastructure/"]
RUN dotnet restore "FitLife.Api/FitLife.Api.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/FitLife.Api"
RUN dotnet build "FitLife.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "FitLife.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FitLife.Api.dll"]
```

#### C2. Frontend Dockerfile

**File**: `fitlife-web/Dockerfile`
```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html

# Custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

**File**: `fitlife-web/nginx.conf`
```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://fitlife-api:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml text/javascript;
}
```

#### C3. Kubernetes Manifests

**File**: `k8s/namespace.yaml`
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: fitlife
  labels:
    name: fitlife
```

**File**: `k8s/configmap.yaml`
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fitlife-config
  namespace: fitlife
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  BackgroundWorkers__EventConsumer__Enabled: "true"
  BackgroundWorkers__EventConsumer__BatchSize: "100"
  BackgroundWorkers__RecommendationGenerator__Enabled: "true"
  BackgroundWorkers__RecommendationGenerator__IntervalMinutes: "10"
  BackgroundWorkers__UserProfiler__Enabled: "true"
  BackgroundWorkers__UserProfiler__IntervalMinutes: "30"
```

**File**: `k8s/secrets.yaml` (base64 encoded)
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: fitlife-secrets
  namespace: fitlife
type: Opaque
data:
  JWT_SECRET: <base64-encoded-secret>
  SQL_CONNECTION_STRING: <base64-encoded-connection-string>
  REDIS_CONNECTION_STRING: <base64-encoded-redis-connection>
  KAFKA_BOOTSTRAP_SERVERS: <base64-encoded-kafka-servers>
```

**File**: `k8s/api-deployment.yaml`
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fitlife-api
  namespace: fitlife
spec:
  replicas: 3
  selector:
    matchLabels:
      app: fitlife-api
  template:
    metadata:
      labels:
        app: fitlife-api
    spec:
      containers:
      - name: api
        image: <acr-name>.azurecr.io/fitlife-api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: fitlife-config
              key: ASPNETCORE_ENVIRONMENT
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: SQL_CONNECTION_STRING
        - name: Jwt__Secret
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: JWT_SECRET
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: REDIS_CONNECTION_STRING
        - name: Kafka__BootstrapServers
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: KAFKA_BOOTSTRAP_SERVERS
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 15
          periodSeconds: 5
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: fitlife-api
  namespace: fitlife
spec:
  selector:
    app: fitlife-api
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP
```

**File**: `k8s/hpa.yaml`
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: fitlife-api-hpa
  namespace: fitlife
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: fitlife-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

**File**: `k8s/ingress.yaml`
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: fitlife-ingress
  namespace: fitlife
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - fitlife.example.com
    secretName: fitlife-tls
  rules:
  - host: fitlife.example.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: fitlife-api
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: fitlife-web
            port:
              number: 80
```

#### C4. GitHub Actions CI/CD

**File**: `.github/workflows/deploy.yml`
```yaml
name: Build and Deploy to AKS

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  ACR_NAME: <your-acr-name>
  AKS_CLUSTER: fitlife-cluster
  AKS_RESOURCE_GROUP: fitlife-rg
  NAMESPACE: fitlife

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4

    - name: Login to Azure Container Registry
      uses: azure/docker-login@v1
      with:
        login-server: ${{ env.ACR_NAME }}.azurecr.io
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}

    - name: Build and push API image
      run: |
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/fitlife-api:${{ github.sha }} \
                     -t ${{ env.ACR_NAME }}.azurecr.io/fitlife-api:latest \
                     -f FitLife.Api/Dockerfile .
        docker push ${{ env.ACR_NAME }}.azurecr.io/fitlife-api:${{ github.sha }}
        docker push ${{ env.ACR_NAME }}.azurecr.io/fitlife-api:latest

    - name: Build and push Web image
      run: |
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/fitlife-web:${{ github.sha }} \
                     -t ${{ env.ACR_NAME }}.azurecr.io/fitlife-web:latest \
                     -f fitlife-web/Dockerfile fitlife-web/
        docker push ${{ env.ACR_NAME }}.azurecr.io/fitlife-web:${{ github.sha }}
        docker push ${{ env.ACR_NAME }}.azurecr.io/fitlife-web:latest

  deploy-staging:
    needs: build-and-push
    runs-on: ubuntu-latest
    environment: staging
    steps:
    - uses: actions/checkout@v4

    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}

    - name: Set AKS context
      uses: azure/aks-set-context@v3
      with:
        resource-group: ${{ env.AKS_RESOURCE_GROUP }}
        cluster-name: ${{ env.AKS_CLUSTER }}

    - name: Deploy to AKS
      run: |
        kubectl apply -f k8s/namespace.yaml
        kubectl apply -f k8s/configmap.yaml
        kubectl apply -f k8s/secrets.yaml
        kubectl apply -f k8s/api-deployment.yaml
        kubectl apply -f k8s/web-deployment.yaml
        kubectl apply -f k8s/hpa.yaml
        kubectl apply -f k8s/ingress.yaml
        kubectl rollout status deployment/fitlife-api -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-web -n ${{ env.NAMESPACE }}

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production
    steps:
    - uses: actions/checkout@v4

    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}

    - name: Set AKS context
      uses: azure/aks-set-context@v3
      with:
        resource-group: ${{ env.AKS_RESOURCE_GROUP }}-prod
        cluster-name: ${{ env.AKS_CLUSTER }}-prod

    - name: Deploy to Production AKS
      run: |
        kubectl apply -f k8s/
        kubectl set image deployment/fitlife-api api=${{ env.ACR_NAME }}.azurecr.io/fitlife-api:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl set image deployment/fitlife-web web=${{ env.ACR_NAME }}.azurecr.io/fitlife-web:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-api -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-web -n ${{ env.NAMESPACE }}
```

---

## 🧪 Testing Phase 4

### Local Full-Stack Testing

```bash
# 1. Build Docker images
docker build -t fitlife-api:local -f FitLife.Api/Dockerfile .
docker build -t fitlife-web:local -f fitlife-web/Dockerfile fitlife-web/

# 2. Update docker-compose.yml to include frontend and backend
docker-compose up -d

# 3. Access application
# Frontend: http://localhost:3000
# API: http://localhost:5269
# Swagger: http://localhost:5269/swagger
```

### Kubernetes Local Testing (Minikube/Docker Desktop)

```bash
# 1. Start local Kubernetes
minikube start
# OR enable Kubernetes in Docker Desktop

# 2. Apply manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl create secret generic fitlife-secrets \
  --from-literal=JWT_SECRET=your-dev-secret \
  --from-literal=SQL_CONNECTION_STRING=your-connection-string \
  -n fitlife

kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml

# 3. Port forward to access locally
kubectl port-forward -n fitlife svc/fitlife-api 8080:80
kubectl port-forward -n fitlife svc/fitlife-web 3000:80

# 4. Check pod status
kubectl get pods -n fitlife
kubectl logs -f deployment/fitlife-api -n fitlife
```

---

## 📋 Phase 4 Completion Checklist

### Frontend
- [ ] Vue.js project initialized with TypeScript
- [ ] Tailwind CSS configured
- [ ] Pinia stores (auth, classes, recommendations)
- [ ] Vue Router with auth guards
- [ ] API service with Axios interceptors
- [ ] Login/Register views functional
- [ ] Dashboard with recommendation feed
- [ ] Class catalog with filters
- [ ] Event tracking on View/Click/Book
- [ ] Responsive design (mobile + desktop)

### API Enhancements
- [ ] Rate limiting middleware (10 req/sec)
- [ ] Correlation ID middleware
- [ ] CORS configured for frontend origin
- [ ] Swagger with JWT authentication
- [ ] API versioning (/api/v1)
- [ ] Enhanced health checks

### Production Readiness
- [ ] API Dockerfile (multi-stage build)
- [ ] Frontend Dockerfile (Nginx)
- [ ] Kubernetes namespace created
- [ ] ConfigMap for app settings
- [ ] Secrets for sensitive data
- [ ] API Deployment with 3 replicas
- [ ] Web Deployment
- [ ] HPA configured (2-10 pods)
- [ ] Ingress with SSL/TLS
- [ ] GitHub Actions CI/CD pipeline
- [ ] Staging environment deployment tested
- [ ] Production deployment manual approval gate

### Documentation
- [ ] DEPLOYMENT.md updated with K8s instructions
- [ ] Runbook for common operations
- [ ] Troubleshooting guide
- [ ] API documentation (Swagger)

---

## 🚀 Success Criteria

**Phase 4 is complete when:**
1. ✅ Frontend SPA is fully functional (login, browse, recommendations, booking)
2. ✅ API has rate limiting, correlation IDs, and enhanced monitoring
3. ✅ Docker images build successfully for API + Web
4. ✅ Kubernetes manifests deploy without errors
5. ✅ CI/CD pipeline builds, pushes, and deploys to staging
6. ✅ Health checks pass in K8s (liveness + readiness)
7. ✅ Full user journey works: Register → View Classes → Get Recs → Book → Track Events
8. ✅ All background workers operational in K8s
9. ✅ Documentation complete for deployment and operations

---

## 📝 Next Steps After Phase 4

With Phase 4 complete, the FitLife Personalization Engine will be:
- ✅ Fully functional frontend + backend
- ✅ Production-ready on Kubernetes
- ✅ Auto-scaling with HPA
- ✅ CI/CD automated deployments
- ✅ Monitored with health checks and structured logging

**Future Enhancements (Post-Phase 4):**
- Advanced analytics dashboard (recommendation quality metrics)
- A/B testing framework for algorithm tuning
- Mobile app (React Native)
- Email notifications for class reminders
- Social features (friend recommendations, group bookings)
- Multi-gym support (franchise expansion)

---

**Let's build this! Start with Part A (Frontend) and work through systematically. Follow the same TDD approach from Phase 3 where applicable.**
