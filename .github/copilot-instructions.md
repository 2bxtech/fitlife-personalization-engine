# FitLife Personalization Engine — Copilot Instructions v2

**Project**: Gym class recommendation system with event-driven architecture  
**Status**: Phase 4 complete — Full-stack application with Kubernetes deployment ready  
**Stack**: .NET 8 API, Vue 3.5 + Pinia 3 + Tailwind CSS 4, Kafka events, Redis cache, SQL Server, Docker/K8s  
**Last updated**: February 7, 2026

---

## Architecture

```
Vue.js SPA (3000) → Vite proxy → .NET API (5269) → Services → Repositories → SQL Server
                                       ↓
                                  Kafka Events → Background Workers → Redis Cache / Database
```

### Layer Responsibilities

| Layer | Location | Purpose |
|-------|----------|---------|
| Controllers | `FitLife.Api/Controllers/` | HTTP handling, request validation, return DTOs only |
| DTOs (shared) | `FitLife.Core/DTOs/` | Request/response models — **NOT** in `FitLife.Api/DTOs/` |
| Services | `FitLife.Core/Services/` | Business logic, scoring algorithm |
| Interfaces | `FitLife.Core/Interfaces/` | Service/repository contracts |
| Models | `FitLife.Core/Models/` | Domain entities, EventTypes constants |
| Repositories | `FitLife.Infrastructure/Repositories/` | EF Core data access (no business logic) |
| External | `FitLife.Infrastructure/{Kafka,Cache,Auth}/` | Kafka producer, Redis, JWT |
| Background Workers | `FitLife.Api/BackgroundServices/` | Kafka consumer, rec generator, user profiler |
| Vue Services | `fitlife-web/src/services/` | Axios API calls, response unwrapping |
| Vue Stores | `fitlife-web/src/stores/` | Pinia state management |
| Vue Types | `fitlife-web/src/types/` | TypeScript interfaces matching backend DTOs |

### Critical Files

- `FitLife.Core/Services/ScoringEngine.cs` — 9-factor scoring implementation
- `FitLife.Api/Program.cs` — DI registration (lines 63–95), middleware order (lines 260–270)
- `FitLife.Api/BackgroundServices/` — `EventConsumerService`, `RecommendationGeneratorService`, `UserProfilerService`
- `fitlife-web/src/services/api.ts` — Axios instance, JWT interceptor, 401 handler
- `docs/RECOMMENDATIONS.md` — Full algorithm documentation

---

## Data Flow Patterns

### Recommendation Generation (batch every 10 min)
```
RecommendationGeneratorService.ExecuteAsync()
  → RecommendationService.GenerateRecommendationsAsync(userId)
  → Check Redis cache (key: rec:{userId}, 10min TTL)
  → On miss: ScoringEngine.CalculateScore() for each active/upcoming class
  → Persist to Recommendations table + cache in Redis
```

### Event Tracking (fire-and-forget)
```
POST /api/events → KafkaProducer.ProduceAsync("user-events")
  → EventConsumerService polls Kafka
  → Save to Interactions table
  → Invalidate user cache if needed
```

### Cache Invalidation Triggers
1. User books class → `await _cache.DeleteAsync($"rec:{userId}")`
2. User updates preferences → invalidate cache
3. User completes 5th class → recalculate segment + invalidate
4. Natural expiration after 10 minutes

---

## Backend Patterns (.NET 8)

### Service Layer
```csharp
// Constructor injection, guard clauses, structured logging
public class RecommendationService : IRecommendationService
{
    private readonly IUserRepository _userRepository;
    private readonly IScoringEngine _scoringEngine;
    private readonly ICacheService _cache;
    private readonly ILogger<RecommendationService> _logger;

    // Guard clause: if (userId == null) throw new ArgumentNullException()
    // Logging: _logger.LogInformation("Generated {Count} recs for {UserId}", count, userId)
}
```

### Repository Pattern (EF Core)
- Generic base: `Repository<T>` with `GetByIdAsync`, `AddAsync`, `UpdateAsync`
- Specific: `UserRepository : Repository<User>, IUserRepository`
- **No business logic** — only data access queries
- Always use `async` methods and `.AsNoTracking()` for read-only
- Use `.Include()` to avoid N+1 queries

### EF Core Configuration
```csharp
builder.Services.AddDbContext<FitLifeDbContext>(options =>
    options.UseSqlServer(connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
```

### Background Workers (IHostedService)
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try { /* work */ await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { _logger.LogError(ex, "Worker failed"); }
    }
}
```

### Kafka Events
```csharp
// Validate against EventTypes static class (FitLife.Core.Models)
// Valid types: View | Click | Book | Complete | Cancel | Rate
await _kafka.ProduceAsync("user-events", new UserEvent {
    UserId = userId, ItemId = classId,
    EventType = EventTypes.View,  // Use constants, not raw strings
    Timestamp = DateTime.UtcNow,
    Metadata = new Dictionary<string, object> { ["source"] = "browse" }
});
```

### Kafka Producer Lifecycle
```csharp
// Registered as Singleton in Program.cs
builder.Services.AddSingleton<KafkaProducer>();

// Graceful shutdown flushes pending messages
lifetime.ApplicationStopping.Register(() => {
    kafkaProducer.Flush(TimeSpan.FromSeconds(30));
});
```

### DI Registration Order (Program.cs)
```
Repositories (Scoped) → Core Services (Scoped) → KafkaProducer (Singleton)
→ RedisCacheService (Singleton) → JwtService (Singleton) → Background Workers (Hosted)
→ JWT Auth → Rate Limiting → CORS → Health Checks
```

### Middleware Pipeline Order (CRITICAL)
```csharp
app.Use(/* Correlation ID */);   // First — request tracking
app.UseCors("AllowFrontend");
app.UseIpRateLimiting();          // Rate limiting before auth
app.UseAuthentication();           // MUST be before Authorization
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
```

---

## API Contract

### Response Wrapper

All endpoints (except Recommendations) wrap responses in `ApiResponse<T>`:
```typescript
{ success: boolean, data: T, message?: string, errors?: string[] }
```

Recommendations endpoints use anonymous objects:
```typescript
{ success: boolean, data: RecommendationDto[], count: number, message?: string }
```

### Endpoints

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| POST | `/api/auth/register` | No | Returns `{ token, user: UserDto }` |
| POST | `/api/auth/login` | No | Returns `{ token, user: UserDto }` |
| GET | `/api/classes?type=Yoga&limit=50` | No | Returns `ClassDto[]` |
| GET | `/api/classes/{id}` | No | Returns `ClassDto` |
| GET | `/api/recommendations/{userId}?limit=10` | JWT | Returns `RecommendationDto[]` (not in ApiResponse wrapper) |
| POST | `/api/recommendations/{userId}/refresh` | JWT | Force regenerate |
| POST | `/api/events` | JWT | Single event tracking |
| POST | `/api/events/batch` | JWT | Raw `EventDto[]` — max 100 |
| GET | `/api/users/{id}` | JWT | Returns `UserDto` |
| PUT | `/api/users/{id}/preferences` | JWT | Only `fitnessLevel`, `goals`, `preferredClassTypes` |
| GET | `/health` | No | Kubernetes probes |

### Key DTO Shapes

**ClassDto**: `id`, `name`, `description`, `type`, `instructorId`, `instructorName`, `level` (NOT `difficulty`), `startTime`, `durationMinutes` (NOT `endTime`), `capacity`, `currentEnrollment`, `availableSpots`, `averageRating`, `totalRatings`, `weeklyBookings`, `isActive`

**UserDto**: `id`, `email`, `firstName`, `lastName`, `fitnessLevel`, `goals[]`, `preferredClassTypes[]`, `segment`, `createdAt`, `updatedAt`

**RecommendationDto**: `rank`, `score`, `reason`, `class` (nested ClassDto — NOT `classDetails`), `generatedAt`

**EventDto**: `userId`, `itemId`, `itemType`, `eventType` (must be: View|Click|Book|Complete|Cancel|Rate), `metadata?`

---

## Recommendation Algorithm (9 Factors)

```
TotalScore = 
  FitnessLevelMatch × 10      +   // Beginner/Intermediate/Advanced alignment
  PreferredClassType × 15      +   // User's explicit preferences
  FavoriteInstructor × 20      +   // HIGHEST weight
  TimePreference × 8           +   // Historical booking patterns
  ClassRating × 2              +   // Average rating (4.8 → 9.6 pts)
  AvailabilityBonus            +   // -5 if <20% spots, +3 if >80%
  SegmentBoost (up to 12)      +   // YogaEnthusiast → +12 for Yoga
  RecencyBonus (up to 5)       +   // +5 if <1 day, +3 if <3 days
  PopularityBonus (up to 8)        // +8 if >50 bookings/week
```

### User Segments (recalculated every 30 min by UserProfilerService)

| Segment | Criteria |
|---------|----------|
| Beginner | <5 completed classes |
| HighlyActive | 5+ classes/week |
| YogaEnthusiast | >60% yoga classes in last 30 days |
| StrengthTrainer | >60% HIIT/Strength classes |
| CardioLover | >60% Spin/Running classes |
| WeekendWarrior | >80% weekend bookings |
| General | Default |

---

## Database Schema

```sql
Users:           Id, Email (unique), PasswordHash, FitnessLevel, Goals (JSON),
                 PreferredClassTypes (JSON), Segment, CreatedAt
Classes:         Id, Name, Type, InstructorId, StartTime, Capacity,
                 CurrentEnrollment, AverageRating, IsActive
Interactions:    Id, UserId, ItemId, EventType, Timestamp, Metadata (JSON)
Recommendations: UserId (PK), ItemId (PK), Score, Rank, Reason, GeneratedAt
```

**Indexes**: `IX_Users_Email` (unique), `IX_Classes_StartTime_Type_Active` (composite, WHERE IsActive=1), `IX_Interactions_UserId_Timestamp` (DESC), `IX_Recommendations_UserId_Rank` (covering: Score, Reason)

---

## Frontend Patterns (Vue 3 + TypeScript)

### The Double-Unwrap Pattern (CRITICAL)
```typescript
// Backend wraps in ApiResponse<T>, Axios adds its own .data
const response = await api.get<ApiResponse<Class[]>>('/classes')
const classes = response.data.data  // ← actual payload at response.data.data
```

### Component Structure (Composition API only)
```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'

const props = defineProps<{ classId: string }>()
const emit = defineEmits<{ book: [classId: string] }>()
// API calls via services, NOT directly in components
// Shared state via Pinia stores
</script>
```

### Pinia Store Pattern
```typescript
export const useRecommendationStore = defineStore('recommendations', () => {
  const recommendations = ref<Recommendation[]>([])
  async function fetchRecommendations(userId: string) {
    recommendations.value = await recommendationService.getRecommendations(userId)
  }
  return { recommendations, fetchRecommendations }
})
```

### Auth Flow
- JWT token stored in `localStorage` under key `token`
- Axios interceptor in `api.ts` reads token from `localStorage` (NOT from store — avoids circular dependency)
- 401 responses → clear `localStorage`, dynamic-import router, redirect to `/login`
- Token format: `{ sub: userId, email, role: "Member", segment, exp }`

### Routing & Guards
```
Public:    /  /login  /register  (redirect to /dashboard if authenticated)
Protected: /dashboard  /classes  /profile  (redirect to /login if not authenticated)
```

### Vite Dev Server
- Port 3000, proxies `/api` → `http://localhost:5269`
- `VITE_API_URL` env var → defaults to `/api` (proxy handles it)
- `@` alias resolves to `src/`

### Tailwind CSS v4
- Uses `@tailwindcss/postcss` plugin (NOT `tailwindcss`)
- `tailwind.config.js` uses v3-style format (backward compat)
- Custom `primary-*` color palette defined in config
- If custom colors don't render → add as CSS custom properties in `style.css`

### Toast Notifications
```typescript
import { useToast } from '@/composables/useToast'
const toast = useToast()
toast.success('Class booked!')  // Also: .error(), .info(), .warning()
```
Auto-dismiss after 5s, positioned top-right, slide-in animation.

### Frontend File Map
```
fitlife-web/src/
├── main.ts                          # App bootstrap
├── App.vue                          # Root layout (Header + RouterView + Footer + Toasts)
├── style.css                        # Tailwind directives
├── router/index.ts                  # Routes + auth guards
├── services/
│   ├── api.ts                       # Axios instance, JWT interceptor, 401 handler
│   ├── authService.ts               # login(), register()
│   ├── classService.ts              # getClasses(), getClassById(), bookClass()
│   └── recommendationService.ts     # getRecommendations(), trackEvent()
├── stores/
│   ├── auth.ts                      # Token + user state, localStorage
│   ├── classes.ts                   # Class list, filters, pagination
│   └── recommendations.ts           # Recommendations list, refresh
├── types/
│   ├── Class.ts                     # Class, ClassFilter interfaces
│   ├── User.ts                      # User, LoginRequest, RegisterRequest
│   ├── Recommendation.ts            # Recommendation (has `class` property)
│   └── Event.ts                     # UserEvent, BatchEventsRequest
├── views/
│   ├── HomeView.vue                 # Landing page
│   ├── LoginView.vue                # Login form
│   ├── RegisterView.vue             # Multi-step registration
│   ├── DashboardView.vue            # Stats + RecommendationFeed
│   ├── ClassesView.vue              # Class browsing + filters
│   └── ProfileView.vue              # Profile editing (preferences only server-side)
├── components/
│   ├── layout/{Header,Footer}.vue
│   ├── classes/{ClassCard,ClassFilter,ClassList}.vue
│   ├── common/ToastNotification.vue
│   └── recommendations/RecommendationFeed.vue
└── composables/useToast.ts          # Toast notification composable
```

---

## Known Limitations (By Design)

1. **No booking endpoint** — `POST /api/classes/{id}/book` not implemented; frontend shows error toast
2. **Profile names** — `PUT /api/users/{id}/preferences` only saves `fitnessLevel`, `goals`, `preferredClassTypes`; firstName/lastName persist to localStorage only
3. **No pagination metadata** — Backend doesn't return `total` count; frontend estimates from `data.length`
4. **Batch events payload** — Backend expects raw `EventDto[]`; previous bug sent `{ events: [...] }` wrapper

---

## Anti-Patterns (DON'T DO)

1. **DON'T** store recs ONLY in Redis → Always persist to Recommendations table
2. **DON'T** await Kafka publish in request handler → Fire-and-forget
3. **DON'T** compute recs on-demand → Pre-compute in batch jobs
4. **DON'T** use `UserId` as Kafka partition key → Use `Hash(UserId)`
5. **DON'T** query Interactions without time filter → Always `Timestamp > @cutoff`
6. **DON'T** return EF entities from controllers → Use DTOs
7. **DON'T** accept any string for `EventType` → Validate against `EventTypes.ValidTypes`
8. **DON'T** put DTOs in `FitLife.Api/DTOs/` → Causes circular deps; use `FitLife.Core/DTOs/`
9. **DON'T** use `User.FindFirst("sub")` → JWT `sub` maps to `ClaimTypes.NameIdentifier` in ASP.NET Core
10. **DON'T** import `useAuthStore()` in `api.ts` → Circular dep; read token from `localStorage` directly

---

## Security

### Password Hashing
```csharp
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
var isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);
```

### JWT Claims Extraction (CRITICAL GOTCHA)
```csharp
// WRONG — returns null:
var userId = User.FindFirst("sub")?.Value;

// CORRECT — use NameIdentifier with fallback:
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
             ?? User.FindFirst("sub")?.Value;
```

### Rate Limiting
- 10 requests/second, 100 requests/minute per IP
- `AspNetCoreRateLimit` package, configured in Program.cs

---

## Decision Trees

### Should I cache this?
```
Recommendation?     → YES (Redis, rec:{userId}, 10min TTL)
User profile?       → NO (always fetch fresh)
Class list?         → MAYBE (popular/upcoming, 5min TTL)
Interaction event?  → NO (write-through to Kafka)
```

### Which layer?
```
Business logic (scoring)?    → FitLife.Core/Services
Data access (EF queries)?    → FitLife.Infrastructure/Repositories
External (Kafka, Redis)?     → FitLife.Infrastructure/{Kafka,Cache}
HTTP handling?               → FitLife.Api/Controllers
DTOs?                        → FitLife.Core/DTOs
```

### Async or sync?
```
Database query?     → ALWAYS async
Kafka publish?      → Fire-and-forget (no await in API controller)
Redis read?         → async
CPU-bound scoring?  → Sync
```

---

## Infrastructure & Ports

| Service | Container Port | Host Port | Credentials |
|---------|---------------|-----------|-------------|
| SQL Server | 1433 | 1433 | sa / `YourStrong@Passw0rd` |
| Redis | 6379 | **6380** | None (6380 to avoid local conflicts) |
| Kafka | 9092 | 9092 | None |
| Zookeeper | 2181 | 2181 | None |
| .NET API | 5269 | 5269 | N/A |
| Vue Dev | 3000 | 3000 | N/A |

---

## Local Development (Windows)

```powershell
# 1. Infrastructure
docker-compose up -d

# 2. Backend
cd FitLife.Api
dotnet ef database update
dotnet run --seed          # Seeds data then exits
dotnet run                 # http://localhost:5269, Swagger at /swagger

# 3. Frontend (separate terminal)
cd fitlife-web
npm install
npm run dev                # http://localhost:3000
```

### Testing
```bash
dotnet test                                    # All .NET tests
dotnet test --filter "Category=Integration"   # Integration only
dotnet test --filter "FullyQualifiedName~ScoringEngine"  # Scoring tests
cd fitlife-web && npm run test                # Frontend tests (vitest not yet installed)
```

**CI/CD Note**: Test projects MUST target `net8.0` — GitHub Actions runners don't support .NET 10.

---

## Git Workflow

### Commit Style (Conventional Commits)
```
feat(scoring): Add popularity bonus to recommendation algorithm
fix(cache): Correct Redis key pattern from recs: to rec:
chore(docker): Add health checks to docker-compose services
```

### AI Agent Rules
- **DO automatically**: Stage + commit after logical units, use conventional commits, create feature branches
- **ASK before**: `git push`, creating PRs, merging branches

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Scoring logic unclear | `FitLife.Core/Services/ScoringEngine.cs` + `docs/RECOMMENDATIONS.md` |
| Cache key format | Always `rec:{userId}` (not `recs:`) |
| JWT 403 errors | Use `ClaimTypes.NameIdentifier` not `"sub"` for claims extraction |
| Circular dependency on build | DTOs must be in `FitLife.Core/DTOs/`, not `FitLife.Api/DTOs/` |
| Redis connection failing | Port 6380 externally (mapped from container's 6379) |
| Kafka not receiving events | Topic is `user-events`, verify with console consumer |
| Middleware order issues | Authentication MUST come before Authorization in Program.cs |
| CI/CD build failing | Verify .csproj targets `net8.0` (not `net10.0`) |
| Frontend blank page | Is `npm run dev` running? Check Tailwind v4 PostCSS config |
| Classes page empty | Is backend running? Test `GET http://localhost:5269/api/classes` directly |
| CORS errors | Only happens outside Vite proxy — don't set `VITE_API_URL` to full backend URL |
| Batch events fail | Send raw `EventDto[]`, not `{ events: [...] }` |
| Profile edit doesn't persist names | By design — only preferences save to server |
| `rec.class` is undefined | Seed data first — backend returns null without classes |
| Error messages show undefined | Use `error.response?.data?.message` (not `.error`) — except RecommendationsController uses `.error` |

---

## Deployment

### Docker
```bash
docker build -t fitlife-api:latest -f FitLife.Api/Dockerfile .
docker build -t fitlife-web:latest -f fitlife-web/Dockerfile ./fitlife-web
```

### Kubernetes (k8s/)
- `api-deployment.yaml` — 3 replicas, HPA (scale 3–10 on CPU >70%)
- `web-deployment.yaml` — 2 replicas, nginx
- `secrets.yaml` — SQL connection, JWT secret (base64)
- `ingress.yaml` — TLS termination, rate limiting
- `configmap.yaml` — Environment configuration
- `hpa.yaml` — Horizontal Pod Autoscaler

### GitHub Actions (`.github/workflows/deploy.yml`)
- Test → Build images → Push to ACR → Deploy to AKS
- Currently disabled (`if: false`) pending Azure provisioning
