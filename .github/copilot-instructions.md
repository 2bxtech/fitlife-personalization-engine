# FitLife Personalization Engine - Copilot Instructions

**Project**: Gym class recommendation system with event-driven architecture (Life Time Fitness)  
**Stack**: .NET 8 API, Vue 3 + Pinia + Tailwind, Kafka events, Redis cache, SQL Server, Docker/K8s

---

## 🎯 Architecture Quick Reference

### Component Boundaries
```
Vue.js SPA → .NET API → Services → Repositories → Database
              ↓
         Kafka Events → Background Workers → Cache/Database
```

**Critical Files**:
- `docs/RECOMMENDATIONS.md` - 9-factor scoring algorithm (weights: instructor=20, type=15, level=10...)
- `FitLife.Core/Services/ScoringEngine.cs` - Recommendation scoring implementation
- `FitLife.Api/BackgroundServices/` - Event consumer, rec generator, user profiler (all IHostedService)

### Data Flow Patterns

**Recommendation Generation** (batch every 10min):
```
RecommendationGeneratorService.ExecuteAsync()
  → RecommendationService.GenerateRecommendationsAsync(userId)
  → Check Redis cache (rec:{userId}, 10min TTL)
  → On miss: ScoringEngine.CalculateScore() for each class
  → Persist to Recommendations table + cache in Redis
```

**Event Tracking** (fire-and-forget):
```
POST /api/events → KafkaProducer.ProduceAsync("user-events")
  → EventConsumerService polls Kafka
  → Save to Interactions table
  → Invalidate user cache if needed
```

**Cache Invalidation Triggers**:
1. User books class → `await _cache.DeleteAsync($"rec:{userId}")`
2. User updates preferences → invalidate cache
3. User completes 5th class → recalculate segment + invalidate
4. Natural expiration after 10 minutes

---

## 🔧 Critical Implementation Patterns

### Layer Responsibilities
| Layer | Location | Purpose |
|-------|----------|--------|
| Controllers | `FitLife.Api/Controllers/` | HTTP handling, request validation, DTOs only |
| Services | `FitLife.Core/Services/` | Business logic, scoring algorithm |
| Repositories | `FitLife.Infrastructure/Repositories/` | EF Core data access (no business logic) |
| Background Workers | `FitLife.Api/BackgroundServices/` | Kafka consumer, rec generator, profiler |
| External | `FitLife.Infrastructure/{Kafka,Cache,Auth}/` | Kafka producer, Redis, JWT |

### Backend (.NET 8)

**Service Layer Structure**:
```csharp
public class RecommendationService : IRecommendationService
{
    private readonly IUserRepository _userRepository;
    private readonly ScoringEngine _scoringEngine;
    private readonly ICacheService _cache;
    
    // Constructor injection for ALL dependencies
    // Guard clauses first: if (userId == null) throw new ArgumentNullException()
    // Structured logging: _logger.LogInformation("Generated {Count} recs", count)
}
```

**Repository Pattern** (EF Core):
- Generic base: `Repository<T>` with `GetByIdAsync`, `AddAsync`, `UpdateAsync`
- Specific implementations: `UserRepository : Repository<User>, IUserRepository`
- **No business logic** in repositories - only data access queries

**EF Core Best Practices**:
```csharp
// DbContext registration with retry logic (Program.cs)
builder.Services.AddDbContext<FitLifeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
);

// Always use async methods
var user = await _context.Users.FindAsync(userId);

// Avoid N+1 queries - use Include for related data
var classes = await _context.Classes
    .Include(c => c.Instructor)
    .Where(c => c.IsActive)
    .ToListAsync();
```

**Background Workers** (IHostedService):
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            // Work logic
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { _logger.LogError(ex, "Worker failed"); }
    }
}
```

**Kafka Event Schema** (user-events topic):
```csharp
// Validate against EventTypes static class (FitLife.Core.Models)
if (!EventTypes.IsValid(eventType)) throw new ArgumentException("Invalid event type");

await _kafka.ProduceAsync("user-events", new UserEvent {
    UserId = userId,
    ItemId = classId,
    EventType = EventTypes.View, // Use constants: EventTypes.{View|Click|Book|Complete|Cancel|Rate}
    Timestamp = DateTime.UtcNow,
    Metadata = new Dictionary<string, object> { ["source"] = "browse" }
});
```

**Kafka Producer Lifecycle**:
```csharp
// Register as singleton (Program.cs)
builder.Services.AddSingleton<KafkaProducer>();

// Graceful shutdown: Flush messages before app stops
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var kafkaProducer = app.Services.GetRequiredService<KafkaProducer>();
    kafkaProducer.Flush(TimeSpan.FromSeconds(30)); // Wait up to 30s for pending messages
});
```

### Frontend (Vue 3 + TypeScript)

**Component Structure** (Composition API):
```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'

const props = defineProps<{ classId: string }>()
const emit = defineEmits<{ book: [classId: string] }>()

// API calls via services, NOT directly in components
// Use Pinia stores for shared state
</script>
```

**Pinia Store Pattern**:
```typescript
// stores/recommendations.ts
export const useRecommendationStore = defineStore('recommendations', () => {
  const recommendations = ref<Recommendation[]>([])
  
  async function fetchRecommendations(userId: string) {
    recommendations.value = await recommendationService.getRecommendations(userId)
  }
  
  return { recommendations, fetchRecommendations }
})
```

**Axios Interceptor** (JWT auth):
```typescript
// services/api.ts
axios.interceptors.request.use(config => {
  const token = authStore.token
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
```

---

## 📊 Recommendation Algorithm (9 Factors)

**Scoring Formula** (from `docs/RECOMMENDATIONS.md`):
```
TotalScore = 
  FitnessLevelMatch × 10 +        // Beginner/Intermediate/Advanced alignment
  PreferredClassType × 15 +        // User's explicit preferences
  FavoriteInstructor × 20 +        // HIGHEST weight
  TimePreference × 8 +             // Historical booking patterns
  ClassRating × 2 +                // Average rating (4.8 → 9.6 pts)
  AvailabilityBonus +              // -5 if <20% spots, +3 if >80%
  SegmentBoost × 12 +              // YogaEnthusiast → +12 for Yoga
  RecencyBonus +                   // +5 if <1 day, +3 if <3 days
  PopularityBonus                  // +8 if >50 bookings/week
```

**User Segments** (recalculated every 30min):
- `Beginner` (<5 completed classes)
- `HighlyActive` (5+ classes/week)
- `YogaEnthusiast` (>60% yoga classes in last 30 days)
- `StrengthTrainer`, `CardioLover`, `WeekendWarrior`, `General`

---

## 🗄️ Database Schema

**Key Tables**:
```sql
Users: Id, Email (unique), PasswordHash, FitnessLevel, Goals (JSON), 
       PreferredClassTypes (JSON), Segment, CreatedAt

Classes: Id, Name, Type, InstructorId, StartTime, Capacity, 
         CurrentEnrollment, AverageRating, IsActive

Interactions: Id, UserId, ItemId, EventType, Timestamp, Metadata (JSON)

Recommendations: UserId (PK), ItemId (PK), Score, Rank, Reason, GeneratedAt
```

**Critical Indexes**:
```sql
IX_Users_Email (unique)
IX_Classes_StartTime_Type_Active (composite, WHERE IsActive=1)
IX_Interactions_UserId_Timestamp (DESC)
IX_Recommendations_UserId_Rank (covering: Score, Reason)
```

---

## � Project Structure Notes

**DTOs Location** (CRITICAL):
```
✅ FitLife.Core/DTOs/          # Shared DTOs - prevents circular dependencies
❌ FitLife.Api/DTOs/           # Don't create DTOs here - causes circular refs
✅ FitLife.Api/Controllers/    # Controllers reference Core DTOs
```

**Why?** FitLife.Api references FitLife.Core. If DTOs are in Api, Core can't use them without circular dependency.

**Phased Development History**:
- Phase 1: Backend foundation (models, repositories, auth, controllers)
- Phase 2: Infrastructure (Kafka, Redis, event tracking)
- Phase 3: Recommendation engine (scoring algorithm, background workers)
- Phase 4: Frontend (Vue SPA) + Production (Docker, K8s, CI/CD)

---

## �🚀 Developer Workflows

### Local Development (Windows)
```powershell
# Start infrastructure (SQL Server, Redis, Kafka)
docker-compose up -d

# Backend (PowerShell/CMD)
cd FitLife.Api
dotnet ef database update
dotnet run  # http://localhost:5269
# Seed data on first run with --seed flag
dotnet run --seed  # Seeds sample users, classes, instructors (exits after seeding)

# Frontend (separate terminal)
cd fitlife-web
npm install
npm run dev  # http://localhost:3000
```

**Connection String Note**: Default password is `YourStrong@Passw0rd` (see `docker-compose.yml` and `appsettings.json`)
**Database Seeding**: Use `dotnet run --seed` to populate initial data (users, classes, instructors, interactions). Application exits after seeding.

### Testing
```bash
dotnet test                                    # All tests
dotnet test --filter "Category=Integration"   # Integration only
cd fitlife-web && npm run test                # Frontend unit tests
```

**⚠️ CI/CD Note**: Ensure test projects target `net8.0` in .csproj. GitHub Actions runners support .NET 8 SDK but not .NET 10.
```xml
<!-- FitLife.Tests/FitLife.Tests.csproj -->
<TargetFramework>net8.0</TargetFramework>  <!-- NOT net10.0 -->
```

**Key Test Files**:
- `FitLife.Tests/Services/` - Scoring algorithm and service unit tests
- `FitLife.Tests/InfrastructureTests.cs` - Repository and infrastructure tests
- Run with: `dotnet test` or `dotnet test --filter FullyQualifiedName~ScoringEngine`

### Verify Infrastructure Health
```powershell
# Check all containers are running
docker ps

# Redis connectivity
docker exec -it fitlife-redis redis-cli
> KEYS rec:*
> GET rec:{userId}
> TTL rec:{userId}

# Kafka topics and messages
docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning

# SQL Server connectivity
docker exec -it fitlife-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q "SELECT DB_NAME()"
```

### Key API Endpoints
```bash
POST /api/auth/register  # Returns { token, user }
POST /api/auth/login     # Returns { token, user }
GET  /api/recommendations/{userId}?limit=10  # Cached recs
POST /api/recommendations/{userId}/refresh   # Force regenerate
POST /api/events         # Track interaction (async to Kafka)
POST /api/events/batch   # Track multiple events (batch processing)
GET  /api/classes?type=Yoga&startTime=2025-11-03  # Filtered search
GET  /health             # Health check for Kubernetes probes
```

**Testing with curl (Windows PowerShell)**:
```powershell
# Register user
$body = @{
    email = "test@example.com"
    password = "TestPass123!"
    firstName = "Test"
    lastName = "User"
    fitnessLevel = "Intermediate"
    preferredClassTypes = @("Yoga", "HIIT")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5269/api/auth/register" -Method POST -Body $body -ContentType "application/json"

# Get recommendations (requires token from register/login)
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://localhost:5269/api/recommendations/$userId" -Headers $headers
```

---

## ⚠️ Anti-Patterns (DON'T DO)

1. **DON'T** store recs ONLY in Redis → Always persist to Recommendations table
2. **DON'T** await Kafka publish in request handler → Fire-and-forget
3. **DON'T** compute recs on-demand → Pre-compute in batch jobs
4. **DON'T** use `UserId` as Kafka partition key → Use `Hash(UserId)`
5. **DON'T** query Interactions without time filter → Always include `Timestamp > @cutoff`
6. **DON'T** return EF entities from controllers → Use DTOs
7. **DON'T** accept any string for `EventType` → Validate against enum
8. **DON'T** put DTOs in FitLife.Api → Circular dependencies occur; use `FitLife.Core/DTOs/`
9. **DON'T** use `User.FindFirst("sub")` alone → JWT 'sub' becomes `ClaimTypes.NameIdentifier` (see Security Gotchas)

---

## 🔐 Security Patterns

**Password Hashing**:
```csharp
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
var isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);
```

**JWT Structure**:
```json
{ "sub": "user_123", "email": "user@gym.com", "role": "Member", 
  "segment": "YogaEnthusiast", "exp": 1730246400 }
```

**JWT Claims Extraction (CRITICAL GOTCHA)**:
```csharp
// ⚠️ WRONG: JWT 'sub' claim becomes ClaimTypes.NameIdentifier in ASP.NET Core
var userId = User.FindFirst("sub")?.Value; // Returns null!

// ✅ CORRECT: Use ClaimTypes.NameIdentifier or fallback pattern
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
             ?? User.FindFirst("sub")?.Value;

// This was the cause of the infamous 403 error in Phase 2!
```

**Middleware Order** (CRITICAL - Program.cs lines 260-270):
```csharp
// Correlation ID middleware (first - for request tracking)
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers.Append("X-Correlation-ID", correlationId);
    await next();
});

app.UseCors("AllowFrontend");
app.UseIpRateLimiting();        // Rate limiting before auth
app.UseAuthentication();        // MUST be before Authorization
app.UseAuthorization();
app.MapHealthChecks("/health"); // Health checks for K8s probes
app.MapControllers();
```

---

## 📝 Git Workflow (AI Agent Guidelines)

### Commit Style (Technical, No Fluff)
```bash
feat(models): Add User, Class, Interaction, Recommendation entities
fix(cache): Correct Redis key pattern from recs: to rec:
chore(docker): Add health checks to docker-compose services
```

**AI Agent SHOULD automatically**:
- ✅ Stage and commit after logical units (e.g., complete controller + DTO)
- ✅ Use conventional commits format
- ✅ Create feature branches

**AI Agent should ASK before**:
- ❓ Pushing to remote (`git push`)
- ❓ Creating Pull Requests
- ❓ Merging branches

---

## 🎯 Decision Trees

**Should I cache this?**
```
Recommendation?        → YES (Redis, 10min TTL)
User profile?          → NO (always fetch fresh)
Class list?            → MAYBE (popular/upcoming, 5min TTL)
Interaction event?     → NO (write-through to Kafka)
```

**Which layer?**
```
Business logic (scoring)?       → FitLife.Core/Services
Data access (EF queries)?       → FitLife.Infrastructure/Repositories
External services (Kafka)?      → FitLife.Infrastructure/Kafka
HTTP handling?                  → FitLife.Api/Controllers
DTOs?                           → FitLife.Core/DTOs
```

**Async or sync?**
```
Database query?        → ALWAYS async
Kafka publish?         → Fire-and-forget (no await in API)
Redis read?            → async
CPU-bound scoring?     → Sync
```

---

## 🚢 Deployment

**Docker**:
```bash
docker build -t fitlife-api:latest ./FitLife.Api
docker build -t fitlife-web:latest ./fitlife-web
```

**Kubernetes** (k8s/):
- `api-deployment.yaml` - 3 replicas, HPA (scale 3-10 based on CPU >70%)
- `web-deployment.yaml` - 2 replicas, nginx
- `secrets.yaml` - SQL connection, JWT secret (base64 encoded)
- `ingress.yaml` - TLS termination, rate limiting

**GitHub Actions** (`.github/workflows/deploy.yml`):
- Test → Build images → Push to ACR → Deploy to AKS
- Currently disabled (`if: false`) pending Azure provisioning

---

## 📚 When Stuck

**Scoring logic unclear?** → `FitLife.Core/Services/ScoringEngine.cs` + `docs/RECOMMENDATIONS.md`  
**Cache key pattern?** → Always `rec:{userId}` (not `recs:`)  
**Event schema?** → `docs/API.md` Event Tracking section  
**Database indexes?** → `docs/DATABASE.md` + migration files  
**Background worker config?** → `appsettings.json` BackgroundWorkers section  
**Middleware registration order?** → `FitLife.Api/Program.cs` lines 260-270 (critical: Authentication before Authorization)  
**DI container registration?** → `FitLife.Api/Program.cs` lines 63-95  
**Redis connection failing?** → Check port 6380 (mapped from container's 6379 to avoid conflicts)  
**Kafka not receiving events?** → Check `KafkaProducer` uses topic "user-events", verify with consumer script  
**JWT 403 errors on authenticated endpoints?** → Check Claims extraction (use `ClaimTypes.NameIdentifier` not "sub")  
**Circular dependency on build?** → Ensure DTOs are in `FitLife.Core/DTOs/`, NOT `FitLife.Api/DTOs/`  
**CI/CD build failing?** → Verify .csproj targets net8.0 (not net10.0) - GitHub Actions runners don't support .NET 10 yet  
**Local Redis conflict?** → This project uses port 6380 externally (mapped from 6379) to avoid conflicts with existing Redis instances
