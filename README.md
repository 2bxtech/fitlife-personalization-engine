# FitLife Personalization Engine

Gym class recommendation system for Life Time Fitness using event-driven architecture and multi-factor scoring algorithms.

## Why I Built This

This project demonstrates patterns I use professionally in enterprise software development:

- **Event-driven architecture** with Kafka - similar to logistics event tracking and async workflows I've built at C.H. Robinson
- **Multi-factor scoring algorithms** - recommendation engines that balance multiple weighted criteria
- **Redis caching strategies** - cache-aside patterns with TTL and invalidation for high-read workloads
- **Background workers** - long-running services (IHostedService) for batch processing and event consumption
- **.NET 8 + Vue 3 full-stack** - complete end-to-end application with JWT auth, Pinia state management, and Tailwind CSS

The architecture prioritizes **observability**, **loose coupling**, and **horizontal scalability** - principles that matter in production systems.

## Overview

FitLife delivers personalized class recommendations based on user preferences, interaction history, and behavior patterns. The system processes user events asynchronously through Kafka, generates recommendations using a 9-factor scoring algorithm, and caches results in Redis for performance.

## Tech Stack

**Backend**
- .NET 8.0 (ASP.NET Core Web API)
- Entity Framework Core 9.0 (SQL Server)
- Kafka (event streaming)
- Redis (caching layer)

**Frontend**
- Vue.js 3.5 with TypeScript
- Vite 7.1 (build tool)
- Pinia 3.0 (state management)
- Tailwind CSS 4.1 (styling)
- Axios 1.13 (HTTP client)

**Infrastructure**
- Docker & Docker Compose
- Kubernetes (Azure AKS)
- GitHub Actions (CI/CD)
- Nginx (web server)

## Architecture

```
Vue.js SPA → .NET API → Services → Repositories → Database
              ↓
         Kafka Events → Background Workers → Cache/Database
```

**Core Components**:
- **Frontend**: Vue 3 + TypeScript + Pinia (http://localhost:3000)
- **Backend**: .NET 8 Web API with JWT auth (http://localhost:5269)
- **Event Bus**: Kafka for async user interaction tracking
- **Cache**: Redis for recommendation caching (10min TTL)
- **Database**: SQL Server with EF Core migrations
- **Workers**: 3 background services (IHostedService):
  - `EventConsumerService` - Polls Kafka, saves interactions
  - `RecommendationGeneratorService` - Batch generates recs every 10min
  - `UserProfilerService` - Updates user segments every 30min

---

## Prerequisites

**Required**:
- Docker Desktop
- .NET 8.0 SDK
- Node.js 20+

**Optional** (for Kubernetes deployment):
- kubectl
- Azure CLI
- Azure subscription

## Local Development

### 1. Clone Repository
```bash
git clone <repository-url>
cd fitlife-personalization-engine
```

### 2. Start Infrastructure
```bash
docker-compose up -d
```

This starts:
- SQL Server (port 1433)
- Redis (port 6380)
- Kafka + Zookeeper (port 9092)

### 3. Run Backend
```bash
cd FitLife.Api
dotnet ef database update  # Apply migrations
dotnet run --seed          # Start API with seed data
```

API available at: `http://localhost:5269`  
Swagger UI: `http://localhost:5269/swagger`

### 4. Run Frontend
```bash
cd fitlife-web
npm install
npm run dev
```

Frontend available at: `http://localhost:3000`

## Testing

### Unit Tests
```bash
dotnet test                                    # All tests
dotnet test --filter "Category=Integration"   # Integration only
```

### Frontend Tests
```bash
cd fitlife-web
npm run test
```

### Manual API Testing

**Register User**:
```bash
curl -X POST http://localhost:5269/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "TestPass123!",
    "firstName": "Test",
    "lastName": "User",
    "fitnessLevel": "Intermediate",
    "preferredClassTypes": ["Yoga", "HIIT"]
  }'
```

**Get Recommendations**:
```bash
curl http://localhost:5269/api/recommendations/{userId}?limit=10 \
  -H "Authorization: Bearer {token}"
```

**Track Event**:
```bash
curl -X POST http://localhost:5269/api/events \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "{userId}",
    "itemId": "class_001",
    "eventType": "View",
    "metadata": { "source": "browse" }
  }'
```

### Verify Infrastructure

**Redis**:
```bash
docker exec -it fitlife-redis redis-cli
> KEYS rec:*
> GET rec:{userId}
> TTL rec:{userId}
```

**Kafka**:
```bash
docker exec -it fitlife-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-events \
  --from-beginning
```

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login and receive JWT token

### Users
- `GET /api/users/{id}` - Get user profile (requires auth)
- `PUT /api/users/{id}` - Update user profile (requires auth)

### Classes
- `GET /api/classes` - List classes with filters (type, difficulty, date)
- `GET /api/classes/{id}` - Get class details
- `POST /api/classes/{id}/book` - Book a class (requires auth)

### Recommendations
- `GET /api/recommendations/{userId}` - Get personalized recommendations (requires auth)
- `POST /api/recommendations/{userId}/refresh` - Force regenerate recommendations (requires auth)

### Events
- `POST /api/events` - Track single event (requires auth)
- `POST /api/events/batch` - Track multiple events (requires auth)

### Health
- `GET /health` - Overall health check
- `GET /health/ready` - Readiness probe (Kubernetes)

**Full API documentation**: See Swagger UI at `/swagger` when running locally

## Recommendation Algorithm

**9-Factor Scoring Formula**:
```
TotalScore = 
  FitnessLevelMatch × 10 +        // Beginner/Intermediate/Advanced alignment
  PreferredClassType × 15 +        // User's explicit preferences
  FavoriteInstructor × 20 +        // Highest weight
  TimePreference × 8 +             // Historical booking hour patterns
  ClassRating × 2 +                // Average rating (4.8 → 9.6 points)
  AvailabilityBonus +              // -5 if <20% spots, +3 if >80% spots
  SegmentBoost × 12 +              // YogaEnthusiast gets +12 for Yoga classes
  RecencyBonus +                   // +5 if <1 day away, +3 if <3 days
  PopularityBonus                  // +8 if >50 bookings/week
```

| Factor | Weight | Description |
|--------|--------|-------------|
| **Favorite Instructor** | 20 | HIGHEST weight - matches user's instructor preferences |
| **Preferred Class Type** | 15 | User's explicitly selected class types (Yoga, Spin, etc.) |
| **Segment Boost** | 12 | User segment alignment (YogaEnthusiast → +12 for Yoga classes) |
| **Fitness Level** | 10 | Beginner/Intermediate/Advanced alignment |
| **Time Preference** | 8 | Historical booking patterns (weekday evenings, weekend mornings) |
| **Recency Bonus** | Up to 5 | +5 if class starts <1 day, +3 if <3 days |
| **Popularity Bonus** | Up to 8 | +8 if >50 bookings/week |
| **Class Rating** | 2 | Average rating (4.8 → 9.6 points) |
| **Availability Bonus** | -5 to +3 | -5 if <20% spots, +3 if >80% spots available |

**User Segments** (recalculated every 30 minutes):
- Beginner (<5 completed classes)
- HighlyActive (5+ classes/week)
- YogaEnthusiast (>60% yoga classes in last 30 days)
- StrengthTrainer (>60% strength classes)
- CardioLover (>60% cardio classes)
- WeekendWarrior (>60% bookings Sat-Sun)
- General (default)

**Caching Strategy**:
- Redis key: `rec:{userId}`
- TTL: 10 minutes
- Also persisted to `Recommendations` table
- Cache invalidated on: Book event, profile update, segment change

---

## Project Structure

```
FitLife.Api/               # Web API + Background Services
  Controllers/             # AuthController, RecommendationsController, etc.
  BackgroundServices/      # EventConsumer, RecGenerator, UserProfiler
  DTOs/                    # Request/response models
  
FitLife.Core/              # Business logic (scoring engine, services)
  Services/                # RecommendationService, ScoringEngine
  Models/                  # User, Class, Interaction, Recommendation
  Interfaces/              # IRecommendationService, etc.
  
FitLife.Infrastructure/    # External dependencies
  Data/                    # EF Core DbContext, migrations
  Repositories/            # UserRepository, ClassRepository
  Kafka/                   # KafkaProducer, event schemas
  Cache/                   # RedisCacheService
  Auth/                    # JwtTokenService
  
fitlife-web/               # Vue 3 SPA
  src/
    components/            # ClassCard, RecommendationList
    views/                 # HomePage, ClassSearchPage, ProfilePage
    stores/                # Pinia stores (recommendations, auth)
    services/              # Axios API client

FitLife.Tests/             # Unit + integration tests
  Services/                # ScoringEngineTests, RecommendationServiceTests
```

## Docker Deployment

### Build Images
```bash
docker build -t fitlife-api:latest -f FitLife.Api/Dockerfile .
docker build -t fitlife-web:latest -f fitlife-web/Dockerfile ./fitlife-web
```

### Run with Docker Compose
```bash
docker-compose up -d
```

Access:
- Frontend: http://localhost:3000
- API: http://localhost:5269
- Swagger: http://localhost:5269/swagger

## Kubernetes Deployment

### Prerequisites
```bash
# Create Azure resources
az group create --name fitlife-rg --location eastus
az acr create --name fitlifeacr --resource-group fitlife-rg --sku Basic
az aks create --name fitlife-aks --resource-group fitlife-rg --node-count 3 --attach-acr fitlifeacr

# Get credentials
az aks get-credentials --resource-group fitlife-rg --name fitlife-aks
```

### Build and Push Images
```bash
az acr login --name fitlifeacr

docker build -t fitlifeacr.azurecr.io/fitlife-api:latest -f FitLife.Api/Dockerfile .
docker push fitlifeacr.azurecr.io/fitlife-api:latest

docker build -t fitlifeacr.azurecr.io/fitlife-web:latest -f fitlife-web/Dockerfile ./fitlife-web
docker push fitlifeacr.azurecr.io/fitlife-web:latest
```

### Deploy to Kubernetes
```bash
# Update manifests with your ACR registry
sed -i 's/<ACR_REGISTRY>/fitlifeacr.azurecr.io/g' k8s/*.yaml

# Apply manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml
kubectl apply -f k8s/ingress.yaml
kubectl apply -f k8s/hpa.yaml

# Check status
kubectl get pods -n fitlife
kubectl get services -n fitlife
```

### Monitor Deployment
```bash
kubectl rollout status deployment/fitlife-api -n fitlife
kubectl logs -f deployment/fitlife-api -n fitlife
kubectl get hpa -n fitlife
```

## CI/CD Pipeline

### GitHub Secrets Required
Add these to repository settings:
- `ACR_USERNAME` - Azure Container Registry username
- `ACR_PASSWORD` - Azure Container Registry password
- `AZURE_CREDENTIALS` - Service principal JSON for AKS access

### Workflow Triggers
- Push to `main` → Deploy to production
- Push to `development` → Deploy to staging
- Pull request → Run tests only

### Pipeline Stages
1. Test - Run .NET unit tests
2. Build - Build Docker images with caching
3. Push - Push images to Azure Container Registry
4. Deploy - Deploy to AKS namespace
5. Smoke Test - Verify health endpoints

## Configuration

### Backend (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=FitLife;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "FitLife",
    "Audience": "FitLifeUsers",
    "ExpirationHours": 24
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Redis": {
    "ConnectionString": "localhost:6380"
  },
  "BackgroundWorkers": {
    "EventConsumer": { "Enabled": true },
    "RecommendationGenerator": { "Enabled": true, "IntervalMinutes": 10 },
    "UserProfiler": { "Enabled": true, "IntervalMinutes": 30 }
  }
}
```

### Frontend (`.env`)
```env
VITE_API_URL=http://localhost:5269
```

## Security

**Authentication**:
- JWT tokens with 24-hour expiration
- BCrypt password hashing (work factor 12)
- Token stored in localStorage (frontend)
- Authorization header: `Bearer {token}`

**Authorization**:
- Users can only access their own data (userId validation)
- Protected endpoints require valid JWT token

**Network Security**:
- CORS configured with allowed origins
- Rate limiting: 10 req/sec, 100 req/min per IP
- TLS termination at Kubernetes Ingress
- Kubernetes Secrets for sensitive data

## Performance

**Targets**:
- API P50 latency: <100ms
- API P95 latency: <200ms
- Cache hit rate: >90%
- Kafka consumer lag: <1 minute
- Recommendation generation: <2 seconds

**Scaling**:
- API: Auto-scales 3-10 pods based on 70% CPU utilization
- Web: Auto-scales 2-5 pods based on 70% CPU utilization
- Background workers: Fixed 1 instance per service

## Troubleshooting

**Frontend not connecting to API**:
→ Verify CORS configuration in `appsettings.json`
→ Check `VITE_API_URL` in `.env` file
→ Inspect browser console for errors

**Database connection failed**:
→ Confirm SQL Server is running: `docker ps`
→ Verify connection string in `appsettings.json`
→ Run migrations: `dotnet ef database update`

**Kafka events not processing**:
→ Check Kafka is running: `docker logs fitlife-kafka`
→ Verify `EventConsumerService` is enabled in configuration
→ Check API logs for Kafka connection errors

**Recommendations not showing**:
→ Ensure user has interaction history (track events first)
→ Check `RecommendationGeneratorService` logs
→ Verify Redis cache is working: `redis-cli KEYS rec:*`
→ Force refresh: `POST /api/recommendations/{userId}/refresh`

## Documentation

- **Architecture**: `docs/ARCHITECTURE.md` - System design and component interactions
- **API**: `docs/API.md` - Complete API reference with examples
- **Database**: `docs/DATABASE.md` - Schema, indexes, and relationships
- **Recommendations**: `docs/RECOMMENDATIONS.md` - Scoring algorithm details
- **Development**: `docs/DEVELOPMENT.md` - Coding standards and patterns

## Git Workflow

**Branch Strategy**:
```
main (protected)
  └── development (integration)
       └── feature/phase-name
```

**Commit Format**:
```bash
<type>(<scope>): <description>

# Types: feat, fix, chore, docs, refactor, test
# Examples:
git commit -m "feat(scoring): Add instructor preference weight to algorithm"
git commit -m "fix(cache): Correct Redis key pattern from recs: to rec:"
git commit -m "chore(docker): Add health checks to compose services"
```

**Pull Request Process**:
1. Create feature branch from `development`
2. Implement changes with logical commits
3. Push to remote and create PR to `development`
4. After review and tests pass, merge to `development`
5. Periodically merge `development` → `main` for releases

## Contributing

**Development Standards**:
- Use async/await for all I/O operations
- Follow repository pattern for data access
- No business logic in controllers or repositories
- DTOs for all API responses (never return entities)
- Comprehensive error handling with structured logging
- Unit tests for business logic (services, scoring engine)

**Code Style**:
- Backend: Follow .NET coding conventions
- Frontend: ESLint + Prettier configuration
- Naming: PascalCase (C#), camelCase (TypeScript)
- Async methods: Suffix with `Async`

## License

MIT
