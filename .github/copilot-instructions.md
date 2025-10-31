# FitLife Personalization Engine - AI Agent Instructions

## ⚡ Quick Reference (Start Here)

**Most Important Files:**
1. `docs/RECOMMENDATIONS.md` - Algorithm logic (scoring formula, segmentation)
2. `docs/API.md` - All endpoints with request/response examples
3. `docs/DATABASE.md` - Schema with indexes and relationships
4. `docs/DEVELOPMENT.md` - Local setup and coding standards

**Key Commands:**
```bash
# Start infrastructure
docker-compose up -d

# Apply migrations (backend)
cd FitLife.Api
dotnet ef database update

# Run with seed data
dotnet run --seed

# Start frontend
cd fitlife-web
npm run dev

# Test recommendation engine
curl http://localhost:8080/api/recommendations/user_123?limit=5
```

**When Stuck, Check:**
- Scoring logic: `ScoringEngine.CalculateScore()` method with 9 factor weights
- Cache keys: Pattern is always `rec:{userId}` (not `recs:`)
- Event schema: See `docs/API.md` Event Tracking section (EventType must be View|Click|Book|Complete|Cancel|Rate)

---

## Project Context

This is a **documentation-first** gym class recommendation system for Life Time Fitness. All technical specifications exist in `docs/` but **implementation has not started yet**. Your role is to build the system according to these blueprints.

## 🎯 Implementation Order (Follow This Sequence)

### Phase 1: Backend Foundation (Start Here)
```bash
# 1. Create .NET solution structure
dotnet new sln -n FitLife
dotnet new webapi -n FitLife.Api -f net8.0
dotnet new classlib -n FitLife.Core -f net8.0
dotnet new classlib -n FitLife.Infrastructure -f net8.0
dotnet sln add FitLife.Api FitLife.Core FitLife.Infrastructure

# 2. Add package dependencies
cd FitLife.Api
dotnet add reference ../FitLife.Core ../FitLife.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Serilog.AspNetCore
dotnet add package BCrypt.Net-Next

cd ../FitLife.Infrastructure
dotnet add reference ../FitLife.Core
dotnet add package Confluent.Kafka
dotnet add package StackExchange.Redis
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

**Implementation Steps:**
1. ✅ Create entity models: `User`, `Class`, `Interaction`, `Recommendation` in `FitLife.Core/Models/`
2. ✅ Set up `FitLifeDbContext` in `FitLife.Infrastructure/Data/`
3. ✅ Create first migration: `dotnet ef migrations add InitialCreate`
4. ✅ Implement repository pattern with `IRepository<T>` base interface
5. ✅ Build CRUD controllers: `UsersController`, `ClassesController`
6. ✅ Add JWT authentication in `Program.cs`
7. ✅ Test endpoints with Postman/Swagger

### Phase 2: Infrastructure Services
8. ✅ Create `docker-compose.yml` with SQL Server, Redis, Kafka
9. ✅ Implement `KafkaProducer` in `FitLife.Infrastructure/Kafka/`
10. ✅ Implement `RedisCacheService` in `FitLife.Infrastructure/Cache/`
11. ✅ Test docker-compose connectivity

### Phase 3: Core Recommendation Logic
12. ✅ Implement `ScoringEngine` with 9-factor algorithm (see below)
13. ✅ Build `RecommendationService` with cache-aside pattern
14. ✅ Add `POST /api/events` endpoint for tracking
15. ✅ Create seed data script with sample users/classes
16. ✅ Test recommendation generation end-to-end

### Phase 4: Background Workers
17. ✅ Implement `EventConsumerService` (hosted service)
18. ✅ Implement `RecommendationGeneratorService` (batch job every 10 min)
19. ✅ Implement `UserProfilerService` (segmentation every 30 min)
20. ✅ Test Kafka event flow with real interactions

### Phase 5: Frontend
21. ✅ Initialize Vue.js project: `npm create vite@latest fitlife-web -- --template vue-ts`
22. ✅ Set up Pinia stores: `authStore`, `classStore`, `recommendationStore`
23. ✅ Build API client with Axios interceptors
24. ✅ Create core components: `ClassCard`, `ClassList`, `RecommendationFeed`
25. ✅ Implement authentication flow (login/register)
26. ✅ Connect to backend and test full user journey

### Phase 6: Production Ready
27. ✅ Add structured logging with Serilog
28. ✅ Implement health checks for K8s probes
29. ✅ Create Kubernetes manifests in `k8s/`
30. ✅ Set up GitHub Actions CI/CD pipeline

---

## 📁 File Creation Checklist

### Backend Structure
```
FitLife.Api/
├── [ ] Program.cs (entry point, DI configuration)
├── [ ] appsettings.json (connection strings, Kafka/Redis config)
├── [ ] appsettings.Development.json
├── Controllers/
│   ├── [ ] AuthController.cs (register, login)
│   ├── [ ] UsersController.cs (profile CRUD)
│   ├── [ ] ClassesController.cs (class catalog)
│   ├── [ ] RecommendationsController.cs (get/refresh recs)
│   └── [ ] EventsController.cs (track interactions)
├── DTOs/
│   ├── [ ] UserDto.cs
│   ├── [ ] ClassDto.cs
│   ├── [ ] RecommendationDto.cs
│   └── [ ] EventDto.cs
└── BackgroundServices/
    ├── [ ] EventConsumerService.cs
    ├── [ ] RecommendationGeneratorService.cs
    └── [ ] UserProfilerService.cs

FitLife.Core/
├── Models/
│   ├── [ ] User.cs
│   ├── [ ] Class.cs
│   ├── [ ] Interaction.cs
│   └── [ ] Recommendation.cs
├── Interfaces/
│   ├── [ ] IUserService.cs
│   ├── [ ] IRecommendationService.cs
│   ├── [ ] IScoringEngine.cs
│   ├── [ ] IRepository.cs
│   └── [ ] IUserRepository.cs
└── Services/
    ├── [ ] UserService.cs
    ├── [ ] RecommendationService.cs
    └── [ ] ScoringEngine.cs

FitLife.Infrastructure/
├── Data/
│   ├── [ ] FitLifeDbContext.cs
│   ├── [ ] Repository.cs (generic base)
│   ├── [ ] UserRepository.cs
│   └── Migrations/ (generated by EF)
├── Kafka/
│   ├── [ ] KafkaProducer.cs
│   └── [ ] KafkaConsumer.cs
├── Cache/
│   └── [ ] RedisCacheService.cs
└── Auth/
    └── [ ] JwtService.cs
```

### Frontend Structure
```
fitlife-web/
├── [ ] package.json
├── [ ] vite.config.ts
├── [ ] tailwind.config.js
├── [ ] tsconfig.json
├── src/
│   ├── [ ] main.ts
│   ├── [ ] App.vue
│   ├── router/
│   │   └── [ ] index.ts (Vue Router setup)
│   ├── stores/
│   │   ├── [ ] auth.ts (Pinia store)
│   │   ├── [ ] classes.ts
│   │   └── [ ] recommendations.ts
│   ├── services/
│   │   ├── [ ] api.ts (Axios instance with interceptors)
│   │   ├── [ ] authService.ts
│   │   ├── [ ] classService.ts
│   │   └── [ ] recommendationService.ts
│   ├── types/
│   │   ├── [ ] User.ts
│   │   ├── [ ] Class.ts
│   │   └── [ ] Recommendation.ts
│   ├── components/
│   │   ├── layout/
│   │   │   ├── [ ] Header.vue
│   │   │   └── [ ] Footer.vue
│   │   ├── classes/
│   │   │   ├── [ ] ClassCard.vue
│   │   │   ├── [ ] ClassList.vue
│   │   │   └── [ ] ClassFilter.vue
│   │   └── recommendations/
│   │       └── [ ] RecommendationFeed.vue
│   └── views/
│       ├── [ ] HomeView.vue
│       ├── [ ] LoginView.vue
│       ├── [ ] RegisterView.vue
│       ├── [ ] DashboardView.vue
│       ├── [ ] ClassesView.vue
│       └── [ ] ProfileView.vue
```

### Infrastructure
```
root/
├── [ ] docker-compose.yml
├── [ ] .gitignore
├── [ ] .editorconfig
├── k8s/
│   ├── [ ] namespace.yaml
│   ├── [ ] configmap.yaml
│   ├── [ ] secrets.yaml
│   ├── [ ] api-deployment.yaml
│   ├── [ ] web-deployment.yaml
│   ├── [ ] ingress.yaml
│   └── [ ] hpa.yaml
└── .github/workflows/
    └── [ ] deploy.yml
```

---

## Architecture Overview (Read First)

- **Pattern**: Microservices-inspired with event-driven design
- **Backend**: .NET Core 8 API + background workers (events, recommendations, profiling)
- **Frontend**: Vue.js 3 SPA with TypeScript, Pinia state management, Tailwind CSS
- **Data Flow**: API → Kafka events → Workers → Redis cache → Database
- **Key Files**: `docs/ARCHITECTURE.md` (system design), `docs/RECOMMENDATIONS.md` (scoring algorithm)

### Component Boundaries
```
API Layer (.NET)     → REST endpoints, JWT auth, validation
Service Layer        → Business logic, scoring engine, recommendation generation
Repository Layer     → EF Core data access, async patterns
Background Workers   → Kafka consumers (EventConsumer, RecommendationGenerator, UserProfiler)
Frontend (Vue)       → Pinia stores, API services, reusable components
```

## Critical Patterns & Conventions

### Backend (.NET Core)

**Naming Standards** (from `docs/DEVELOPMENT.md`):
- Classes/Interfaces: `UserService`, `IUserRepository`
- Async methods: Always suffix with `Async` → `GetUserByIdAsync`
- Private fields: `_context`, `_logger` (underscore prefix)
- Use explicit types unless obvious: `User user = await ...`

**Service Layer Structure**:
```csharp
public class RecommendationService : IRecommendationService
{
    private readonly IUserRepository _userRepository;
    private readonly ScoringEngine _scoringEngine;
    private readonly IRedisCacheService _cache;
    
    // Constructor injection for ALL dependencies
    // Guard clauses early in methods
    // XML docs on public methods
}
```

**Repository Pattern** (EF Core):
- Generic base: `IRepository<T>` with `GetByIdAsync`, `AddAsync`, `UpdateAsync`
- Specific repos inherit: `UserRepository : Repository<User>, IUserRepository`
- No business logic in repositories - only data access

**Kafka Event Publishing** (from `docs/ARCHITECTURE.md`):
```csharp
// Events must be: classId, eventType, userId, timestamp, metadata (JSON)
await _kafkaProducer.ProduceAsync("user-events", new UserEvent {
    UserId = userId,
    ItemId = classId,
    EventType = "View", // View|Click|Book|Complete|Cancel|Rate
    Timestamp = DateTime.UtcNow,
    Metadata = JsonSerializer.Serialize(new { source = "browse" })
});
```

### Frontend (Vue.js 3)

**Component Structure** (Composition API only):
```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'

const props = defineProps<{ classId: string }>()
const emit = defineEmits<{ book: [classId: string] }>()

// Use composables for shared logic: useAuth(), useClasses()
// API calls via services, not directly in components
</script>
```

**Pinia Store Pattern**:
- `authStore` → JWT token, current user, login/logout
- `classStore` → Class catalog, filters, pagination state
- `recommendationStore` → Personalized recs, tracking events
- Actions are async, use services layer: `async fetchRecommendations() { ... }`

**API Client Setup** (Axios with interceptors):
```typescript
// services/api.ts - Add JWT token to all requests
axios.interceptors.request.use(config => {
  const token = authStore.token
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
```

## Recommendation Algorithm (Critical Logic)

**Location**: Implement in `FitLife.Api/Services/ScoringEngine.cs`

**Multi-Factor Scoring Formula** (from `docs/RECOMMENDATIONS.md`):
```
TotalScore = 
  FitnessLevelMatch × 10 +        // Beginner/Intermediate/Advanced alignment
  PreferredClassType × 15 +        // User's explicit preferences (JSON array)
  FavoriteInstructor × 20 +        // Highest weight - instructor quality matters most
  TimePreference × 8 +             // Historical booking hour patterns
  ClassRating × 2 +                // Average rating (4.8 → 9.6 points)
  AvailabilityBonus +              // -5 if <20% spots, +3 if >80% spots
  SegmentBoost × 12 +              // YogaEnthusiast gets +12 for Yoga classes
  RecencyBonus +                   // +5 if <1 day away, +3 if <3 days
  PopularityBonus                  // +8 if >50 bookings/week, +4 if >20
```

**User Segmentation** (recalculate every 30 min):
- `Beginner` (<5 completed classes)
- `HighlyActive` (5+ classes/week)
- `YogaEnthusiast` (>60% yoga classes in last 30 days)
- `StrengthTrainer`, `CardioLover`, `WeekendWarrior`, `General`

**Caching Strategy**:
- Redis key: `rec:{userId}`, TTL: 10 minutes
- Also persist to `Recommendations` table with `GeneratedAt` timestamp
- Cache-aside pattern: check Redis → compute if miss → store in both

**Cache Invalidation (Critical):**

When to invalidate user's recommendation cache:

1. **User books a class** (POST /api/events with eventType=Book)
   ```csharp
   await _cache.DeleteAsync($"rec:{userId}");
   await _recQueue.EnqueueAsync(userId); // Trigger fast regen
   ```

2. **User updates preferences** (PUT /api/users/{id}/preferences)
   ```csharp
   await _cache.DeleteAsync($"rec:{userId}");
   // Wait for next batch run (10 min) - not urgent
   ```

3. **User completes 5th class** (segment may change from Beginner)
   ```csharp
   if (completedCount == 5) {
       await _userProfiler.RecalculateSegment(userId);
       await _cache.DeleteAsync($"rec:{userId}");
   }
   ```

4. **Natural expiration** after 10 minutes (even if not invalidated)

**Fallback Chain:**
```
Redis cache miss → Check database (last 10 min) → GenerateRecommendations() → Popular classes fallback
```

## Database Schema (EF Core)

**Key Tables** (from `docs/DATABASE.md`):

1. **Users**: `Id`, `Email` (unique), `PasswordHash`, `FitnessLevel`, `Goals` (JSON), `PreferredClassTypes` (JSON), `Segment`, `CreatedAt`
2. **Classes**: `Id`, `Name`, `Type`, `InstructorId`, `StartTime`, `Capacity`, `CurrentEnrollment`, `AverageRating`, `IsActive`
3. **Interactions**: `Id`, `UserId`, `ItemId`, `ItemType`, `EventType`, `Timestamp`, `Metadata` (JSON) - event store
4. **Recommendations**: Composite PK `(UserId, ItemId)`, `Score`, `Rank`, `Reason`, `GeneratedAt`

**Critical Indexes** (add in migrations):
```sql
IX_Users_Email (unique)
IX_Classes_StartTime_Type_Active (composite, filtered WHERE IsActive=1)
IX_Interactions_UserId_Timestamp (DESC for recent events)
IX_Recommendations_UserId_Rank (covering index with Score, Reason)
```

**Relationships**:
- No FK constraints on `Interactions.ItemId` (flexible event store)
- `Recommendations.UserId` → `Users.Id` (cascade delete)

## Developer Workflows

### Initial Setup (No Code Exists Yet)
```bash
# Backend
dotnet new webapi -n FitLife.Api -f net8.0
cd FitLife.Api
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Confluent.Kafka
dotnet add package StackExchange.Redis
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# Frontend
npm create vite@latest fitlife-web -- --template vue-ts
cd fitlife-web
npm install pinia vue-router axios @vueuse/core
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

### Running Locally (After Implementation)
```bash
# Start infrastructure (from root)
docker-compose up -d  # SQL Server, Redis, Kafka

# Backend
cd FitLife.Api
dotnet ef database update          # Apply migrations
dotnet run --seed                  # Seed sample data
# API: http://localhost:8080, Swagger: /swagger

# Frontend (separate terminal)
cd fitlife-web
npm run dev                        # http://localhost:3000
```

### Testing Commands
```bash
# Backend
dotnet test                                    # All tests
dotnet test --filter "Category=Integration"   # Integration only

# Frontend
npm run test                       # Vitest unit tests
npm run test:e2e                   # Playwright E2E
```

## API Contract Essentials

**Authentication** (JWT, see `docs/API.md`):
- `POST /api/auth/register` → Returns `{ token, user }`
- `POST /api/auth/login` → Returns `{ token, user }`
- All protected endpoints require: `Authorization: Bearer <token>`

**Key Endpoints**:
- `GET /api/recommendations/{userId}?limit=10` → Returns precomputed recs from cache/DB
- `POST /api/recommendations/{userId}/refresh` → Force regenerate (invalidate cache)
- `POST /api/events` → Body: `{ userId, itemId, eventType, metadata }` → Publish to Kafka
- `GET /api/classes?page=1&pageSize=20&type=Yoga&startTime=2025-10-31` → Filtered search

**Response Format** (consistent across all endpoints):
```json
{
  "success": true,
  "data": { ... },
  "message": "Optional human-readable message",
  "errors": []  // Only present on validation failures
}
```

## Integration Points

### Kafka Topics (Apache Kafka / Azure Event Hubs)
- **Producer**: API service publishes to `user-events` on every interaction
- **Consumer**: `EventConsumerService` (background worker) processes events, stores in `Interactions` table
- **Partition Key**: `UserId` for event ordering per user

### Redis Cache Structure
```
rec:{userId}           → List<RecommendationDto>  (TTL: 10 min)
session:{sessionId}    → User session data        (TTL: 30 min)
popular:classes        → Sorted set by bookings   (TTL: 5 min)
```

### External Dependencies
- **SQL Server**: Connection string in `appsettings.json` → `ConnectionStrings:DefaultConnection`
- **Redis**: `Redis:ConnectionString` (format: `localhost:6379,password=...`)
- **Kafka**: `Kafka:BootstrapServers` (format: `localhost:9092`)

---

## Background Worker Implementation

### EventConsumerService Configuration

```csharp
// Program.cs - Register as hosted service
services.AddHostedService<EventConsumerService>();

// Kafka Consumer Config
var config = new ConsumerConfig {
    GroupId = "fitlife-event-consumers",
    BootstrapServers = kafkaBootstrap,
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false, // Manual commit after processing
    MaxPollIntervalMs = 300000, // 5 minutes
    SessionTimeoutMs = 45000
};

// Partition Strategy:
// - 10 partitions in "user-events" topic
// - Partition key: UserId (ensures user's events processed in order)
// - Scale to 3-5 consumer instances for parallel processing
```

### Worker Startup Configuration

```json
// appsettings.json
{
  "BackgroundWorkers": {
    "EventConsumer": { 
      "Enabled": true, 
      "BatchSize": 100,
      "PollIntervalMs": 1000
    },
    "RecommendationGenerator": { 
      "Enabled": true, 
      "IntervalMinutes": 10,
      "BatchSize": 1000,
      "ProcessActiveUsersOnly": true
    },
    "UserProfiler": { 
      "Enabled": true, 
      "IntervalMinutes": 30,
      "LookbackDays": 30
    }
  }
}
```

### Graceful Shutdown Pattern

```csharp
public class EventConsumerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(1));
                if (result != null)
                {
                    await ProcessEvent(result.Message.Value);
                    _consumer.Commit(result); // Manual commit
                }
            }
            catch (OperationCanceledException)
            {
                break; // Shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker error");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        // Cleanup: Close consumer, commit offsets
        _consumer.Close();
        _logger.LogInformation("EventConsumer shut down gracefully");
    }
}
```

---

## Authentication & Security

### Password Hashing (BCrypt)

```csharp
// Register endpoint
var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
user.PasswordHash = passwordHash;

// Login endpoint
var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
if (!isValid) return Unauthorized("Invalid credentials");
```

### JWT Token Structure

```json
{
  "sub": "user_67890",
  "email": "member@lifetime.com",
  "role": "Member",
  "segment": "YogaEnthusiast",
  "iat": 1730246400,
  "exp": 1730332800
}
```

### Token Expiration Strategy

- **Access Token**: 24 hours (stored in localStorage)
- **No refresh token in v1** (user must login again after 24h)
- **Future v2**: Add refresh token for 7-day sessions

### Middleware Order (CRITICAL)

```csharp
// Program.cs - ORDER MATTERS
app.UseAuthentication();  // MUST come before Authorization
app.UseAuthorization();   // Depends on Authentication context
app.MapControllers();
```

---

## Structured Logging with Serilog

### Configuration

```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { 
        "Name": "File", 
        "Args": { 
          "path": "logs/fitlife-.txt", 
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Logging Patterns

```csharp
// Structured logging with context
_logger.LogInformation(
    "Generated {Count} recommendations for user {UserId} in {Duration}ms",
    recommendations.Count, userId, stopwatch.ElapsedMilliseconds
);

// Correlation IDs for request tracking
using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("UserId", userId))
{
    // All logs in this scope include CorrelationId and UserId
    _logger.LogInformation("Processing user request");
}

// Error logging with exception details
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
    throw;
}
```

---

## Global Error Handling

### Exception Middleware

```csharp
// Program.cs
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        
        logger.LogError(exception, "Unhandled exception occurred");
        
        context.Response.StatusCode = exception switch
        {
            UserNotFoundException => StatusCodes.Status404NotFound,
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };
        
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = exception?.Message ?? "An error occurred",
            traceId = Activity.Current?.Id ?? context.TraceIdentifier
        });
    });
});
```

### Controller Error Pattern

```csharp
[HttpGet("{userId}")]
public async Task<IActionResult> GetRecommendations(string userId, int limit = 10)
{
    try
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("UserId is required");
            
        var recs = await _recService.GetRecommendationsAsync(userId, limit);
        return Ok(new { success = true, data = recs });
    }
    catch (UserNotFoundException ex)
    {
        _logger.LogWarning("User not found: {UserId}", userId);
        return NotFound(new { success = false, error = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get recommendations for {UserId}", userId);
        return StatusCode(500, new { success = false, error = "Failed to retrieve recommendations" });
    }
}
```

---

## Health Checks for Kubernetes

### Implementation

```csharp
// Program.cs
services.AddHealthChecks()
    .AddSqlServer(
        connectionString, 
        name: "database",
        tags: new[] { "ready" })
    .AddRedis(
        redisConnection, 
        name: "cache",
        tags: new[] { "ready" })
    .AddKafka(
        new ProducerConfig { BootstrapServers = kafkaBootstrap }, 
        name: "kafka",
        tags: new[] { "ready" });

// Liveness probe (is app running?)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Readiness probe (is app ready to serve traffic?)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Kubernetes Probe Configuration

```yaml
# k8s/api-deployment.yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 15
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3
```

---

## FitLife-Specific Anti-Patterns (DON'T DO THESE)

1. **DON'T** store recommendations ONLY in Redis
   - ❌ Bad: Cache-only storage (data loss on Redis restart)
   - ✅ Good: Persist to database + cache in Redis

2. **DON'T** block API requests waiting for Kafka publish
   - ❌ Bad: `await _kafka.ProduceAsync()` in request handler
   - ✅ Good: Fire-and-forget with background retry queue

3. **DON'T** compute recommendations on-demand in API endpoint
   - ❌ Bad: Calculate score for 100 classes on every GET request
   - ✅ Good: Pre-compute in batch jobs, serve from cache/database

4. **DON'T** use `UserId` as Kafka partition key
   - ❌ Bad: Hot partitions for active users
   - ✅ Good: Hash(UserId) for even distribution across partitions

5. **DON'T** query `Interactions` table without time filter
   - ❌ Bad: `SELECT * FROM Interactions WHERE UserId = @userId`
   - ✅ Good: `... WHERE UserId = @userId AND Timestamp > @cutoff` (use index)

6. **DON'T** return EF entities directly from controllers
   - ❌ Bad: `return Ok(user)` (exposes PasswordHash, over-fetches)
   - ✅ Good: `return Ok(UserDto.FromEntity(user))` (explicit mapping)

7. **DON'T** use `EventType` as string without validation
   - ❌ Bad: Accept any string (typos like "Boook")
   - ✅ Good: Enum or const validation (View|Click|Book|Complete|Cancel|Rate)

---

## Decision Trees for Common Scenarios

### "Should I cache this?"

```
Is it a recommendation?     → YES → Cache in Redis (10 min TTL)
Is it user profile?         → NO  → Always fetch fresh from DB
Is it a class list?         → MAYBE → Cache popular/upcoming (5 min TTL)
Is it an interaction event? → NO  → Write-through to Kafka only
Is it analytics/metrics?    → YES → Cache aggregates (30 min TTL)
```

### "Which layer should this code live in?"

```
Business logic (scoring, validation)?      → FitLife.Core/Services
Data access (EF queries)?                  → FitLife.Infrastructure/Repositories
External services (Kafka, Redis)?         → FitLife.Infrastructure/{Kafka,Cache}
HTTP handling (validation, routing)?      → FitLife.Api/Controllers
Domain models (User, Class)?              → FitLife.Core/Models
DTOs for API responses?                   → FitLife.Api/DTOs
```

### "Should this be sync or async?"

```
Database query?             → ALWAYS async (await _context.Users.FindAsync)
Kafka publish?              → Fire-and-forget (no await in API)
Redis cache read?           → async (await _cache.GetAsync)
CPU-bound algorithm?        → Sync (ScoringEngine.CalculateScore)
Background job?             → async (await Task.Delay, cancellation token)
```

### "How do I handle this error?"

```
User not found?                → 404 NotFound with message
Validation failure?            → 400 BadRequest with field errors
Authentication failure?        → 401 Unauthorized (no details)
Authorization failure?         → 403 Forbidden
External service timeout?      → 503 ServiceUnavailable + retry later
Unexpected exception?          → 500 InternalServerError + log details
```

---

## Common Pitfalls to Avoid

## Common Pitfalls to Avoid (Legacy - See Anti-Patterns Above)

1. **Don't** put business logic in controllers - controllers only validate and delegate
2. **Don't** return `DbSet<T>` or EF entities directly - use DTOs to avoid over-fetching
3. **Don't** forget `async/await` on DB operations - all EF Core should be async
4. **Don't** store sensitive data in Kafka events - events are logged and audited
5. **Don't** regenerate recommendations synchronously on every request - use cache or return stale data
6. **Don't** use string concatenation for SQL - always use parameterized queries or EF LINQ

## Deployment (Docker/Kubernetes)

**Dockerfile Location** (multi-stage build):
- `FitLife.Api/Dockerfile` → .NET runtime image with health checks
- `fitlife-web/Dockerfile` → Nginx serving Vue build output

**Kubernetes Manifests** (`k8s/` directory):
- `api-deployment.yaml` → 3 replicas, HPA config (scale 2-10 based on CPU >70%)
- `web-deployment.yaml` → 2 replicas, LoadBalancer service
- `secrets.yaml` → SQL connection string, JWT secret, Redis password (base64 encoded)

**CI/CD** (GitHub Actions `.github/workflows/deploy.yml`):
- Trigger: Push to `main` → Build images → Push to ACR → Deploy to AKS staging
- Manual approval required for production deployment

## Questions to Ask User When Clarification Needed

1. "Should I implement the `EventConsumerService` background worker first, or focus on API endpoints?"
2. "For user segmentation, should `UserProfilerService` run on a schedule or triggered by events?"
3. "Do you want pagination for recommendations (e.g., infinite scroll) or just top 10?"
4. "Should class bookings be tracked in a separate `Bookings` table or just in `Interactions`?"
5. "For frontend state management, should recommendations refresh automatically or only on user action?"

---

**Start Here**: Read `docs/ARCHITECTURE.md` and `life time fitness - gym personal.txt` for full context. Begin with Phase 1 (Foundation) from the original spec: basic CRUD API + Vue frontend + Docker setup.
