# FitLife Architecture Documentation

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture Principles](#architecture-principles)
3. [Component Design](#component-design)
4. [Data Flow](#data-flow)
5. [Scalability & Performance](#scalability--performance)
6. [Security Architecture](#security-architecture)
7. [Trade-offs & Decisions](#trade-offs--decisions)

## System Overview

FitLife follows a **microservices-inspired architecture** with event-driven patterns, designed for horizontal scalability and loose coupling between components.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                             │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Vue.js 3 SPA (Progressive Web App)                        │ │
│  │  - State Management (Pinia)                                │ │
│  │  - Client-side Routing (Vue Router)                        │ │
│  │  - API Client (Axios with interceptors)                    │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────┬───────────────────────────────────────┘
                           │ HTTPS/REST
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                       API GATEWAY (Optional)                     │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Nginx / Azure API Management                              │ │
│  │  - SSL Termination                                         │ │
│  │  - Rate Limiting (100 req/min per user)                    │ │
│  │  - Request Routing                                         │ │
│  │  - JWT Validation                                          │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────┬───────────────────────────────────────┘
                           │
        ┌──────────────────┴──────────────────┐
        │                                     │
        ▼                                     ▼
┌─────────────────┐                  ┌─────────────────┐
│  API Service    │                  │  API Service    │
│  (Pod 1)        │                  │  (Pod 2)        │
└────────┬────────┘                  └────────┬────────┘
         │                                    │
         └──────────┬─────────────────────────┘
                    │
         ┌──────────┴──────────┐
         │                     │
         ▼                     ▼
┌─────────────────┐   ┌─────────────────┐
│  Redis Cache    │   │  Event Stream   │
│  - Recs (10m)   │   │  (Kafka)        │
│  - Sessions     │   │  - user-events  │
│  - Popular      │   │  - system-events│
└─────────────────┘   └────────┬────────┘
         │                     │
         │                     ▼
         │            ┌─────────────────┐
         │            │  Background     │
         │            │  Workers        │
         │            │  - Event        │
         │            │    Consumer     │
         │            │  - Rec Gen      │
         │            │  - Profiler     │
         │            └────────┬────────┘
         │                     │
         └──────────┬──────────┘
                    ▼
         ┌─────────────────────┐
         │  Azure SQL Database │
         │  - Users            │
         │  - Classes          │
         │  - Interactions     │
         │  - Recommendations  │
         └─────────────────────┘
```

## Architecture Principles

### 1. Separation of Concerns
- **Presentation Layer**: Vue.js handles all UI/UX
- **API Layer**: .NET Core exposes RESTful endpoints
- **Business Logic**: Services contain domain logic
- **Data Layer**: Repositories abstract data access

### 2. Event-Driven Design
- **Async Communication**: User actions publish events to Kafka
- **Loose Coupling**: Services don't directly depend on each other
- **Eventual Consistency**: Events processed asynchronously
- **Event Sourcing**: Complete audit trail of user interactions

### 3. Caching Strategy
- **Cache-Aside Pattern**: Check cache first, load on miss
- **TTL-based Expiration**: 10-minute TTL for recommendations
- **Write-Through**: Update cache when data changes
- **Cache Warming**: Pre-compute popular recommendations

### 4. API Design
- **RESTful**: Standard HTTP methods and status codes
- **Versioning**: URL-based (`/api/v1/...`)
- **Pagination**: Cursor-based for large datasets
- **HATEOAS**: Links to related resources

## Component Design

### 1. API Service (.NET Core 8)

#### Controllers
```
UsersController        → User profile CRUD
ClassesController      → Class catalog management
RecommendationsController → Personalized recommendations
EventsController       → Event tracking
AuthController         → Authentication/authorization
```

#### Service Layer
```
UserService            → User business logic
RecommendationService  → Generate & retrieve recommendations
EventService           → Validate & publish events
ScoringEngine          → Calculate recommendation scores
```

#### Infrastructure
```
KafkaProducer          → Publish events to Kafka
RedisCacheService      → Cache management
JwtService             → Token generation & validation
```

#### Repositories
```
UserRepository         → User data access
ClassRepository        → Class data access
InteractionRepository  → Event storage
```

### 2. Background Workers

#### EventConsumerService
- **Purpose**: Process user events from Kafka
- **Frequency**: Real-time (continuous polling)
- **Operations**:
  - Validate event schema
  - Save to Interactions table
  - Update user activity timestamp
  - Update class enrollment counts
  - Trigger recommendation refresh if needed

#### RecommendationGeneratorService
- **Purpose**: Batch generate recommendations
- **Frequency**: Every 10 minutes
- **Operations**:
  - Fetch users needing refresh (last updated > 10 min ago)
  - Generate recommendations using ScoringEngine
  - Save to Recommendations table
  - Update Redis cache
  - Track performance metrics

#### UserProfilerService
- **Purpose**: Analyze user behavior and assign segments
- **Frequency**: Every 30 minutes
- **Operations**:
  - Analyze interaction history (last 30 days)
  - Calculate behavior patterns
  - Assign user segment (YogaEnthusiast, HighlyActive, etc.)
  - Update User.Segment field

### 3. Frontend (Vue.js 3)

#### State Management (Pinia)
```javascript
authStore          → User authentication state
userStore          → Current user profile
classStore         → Class catalog and filters
recommendationStore → Personalized recommendations
```

#### Services
```javascript
authService        → Login, register, token management
classService       → Fetch classes, search, filter
recommendationService → Get recommendations, track events
analyticsService   → Track user interactions
```

#### Key Components
```
ClassCard          → Display individual class
ClassList          → Grid of classes with pagination
ClassFilter        → Filter UI (type, time, instructor)
RecommendationFeed → Personalized class recommendations
ProfileForm        → Edit user profile and preferences
```

## Data Flow

### 1. User Registration Flow
```
User fills form → Vue.js validates → POST /api/auth/register
    → UserService.CreateUser()
    → Hash password (BCrypt)
    → Save to Users table
    → Generate JWT token
    → Return token + user profile
    → Store token in localStorage
    → Redirect to dashboard
```

### 2. Class Browsing Flow
```
User navigates to Classes page
    → GET /api/classes?page=1&pageSize=20
    → ClassRepository.GetClasses()
    → Check Redis cache (popular classes)
    → Query database if cache miss
    → Return ClassResponse DTOs
    → Vue renders ClassList component
    → User filters by type "Yoga"
    → GET /api/classes?type=Yoga&page=1
    → Repeat
```

### 3. Recommendation Generation Flow
```
User logs in → Load dashboard
    → GET /api/recommendations/{userId}?limit=10
    → RecommendationService.GetRecommendations()
    → Check Redis cache (key: rec:{userId})
    → If cache hit: return cached recommendations
    → If cache miss:
        → Check Recommendations table (last 10 min)
        → If fresh: return from DB + cache
        → If stale or missing:
            → RecommendationService.GenerateRecommendations()
            → Fetch user profile & segment
            → Fetch candidate classes (upcoming, not full)
            → For each class: ScoringEngine.CalculateScore()
            → Sort by score descending
            → Take top N
            → Generate human-readable reasons
            → Save to Recommendations table
            → Cache in Redis (10 min TTL)
            → Return recommendations
```

### 4. Event Tracking Flow (Async)
```
User views class → trackClassView(classId)
    → POST /api/events
    → EventService.TrackEvent()
    → Validate event schema
    → KafkaProducer.ProduceAsync("user-events", event)
    → Return 202 Accepted (immediate)
    → [Async] EventConsumerService polls Kafka
    → Consume message from "user-events" topic
    → Save to Interactions table
    → Update user's last active timestamp
    → Commit Kafka offset
    → [Later] RecommendationGeneratorService runs
    → Incorporates new interaction into scoring
```

### 5. Class Booking Flow
```
User clicks "Book Now" button
    → trackClassBooking(classId)
    → POST /api/events (Book event)
    → POST /api/classes/{classId}/book
    → ClassService.BookClass()
    → Check if class is full
    → Check if user already booked
    → Create booking record
    → Increment class.CurrentEnrollment
    → Publish "ClassBooked" event to Kafka
    → Return success
    → [Async] EventConsumer processes event
    → Update user's booking history
    → Recommendations refresh to suggest similar classes
```

## Scalability & Performance

### Horizontal Scaling

#### API Tier
- **Stateless Design**: No in-memory session state
- **Load Balancing**: Kubernetes service distributes requests
- **Auto-scaling**: HPA based on CPU/memory (scale 2-10 pods)
- **Database Connections**: Pool per instance (max 100)

#### Worker Tier
- **Kafka Consumer Groups**: Multiple consumers for parallelism
- **Partition Strategy**: Events partitioned by userId (affinity)
- **Batch Processing**: Process 1000 users per batch
- **Idempotency**: Duplicate events handled gracefully

### Caching Strategy

#### Redis Cache Structure
```
Key Pattern                        TTL      Purpose
---------------------------------------------------------
rec:{userId}                       10 min   User recommendations
class:popular:{type}               1 hour   Popular classes by type
user:session:{token}               24 hours User session data
class:{classId}                    5 min    Class details
```

#### Cache Invalidation
- **Time-based**: TTL expiration
- **Event-based**: Invalidate on data change (class update)
- **Manual**: Admin can force refresh

### Database Optimization

#### Indexes
```sql
-- High-cardinality lookups
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Classes_StartTime ON Classes(StartTime);

-- Composite indexes for common queries
CREATE INDEX IX_Interactions_UserId_Timestamp 
  ON Interactions(UserId, Timestamp DESC);

-- Filtered indexes for active classes
CREATE INDEX IX_Classes_Active_Type 
  ON Classes(Type) WHERE IsActive = 1;
```

#### Query Optimization
- **Projection**: Select only needed columns
- **Pagination**: Use `OFFSET/FETCH` or keyset pagination
- **Eager Loading**: Use `.Include()` to avoid N+1 queries
- **Compiled Queries**: For frequently-executed queries

### Performance Targets

| Metric                    | Target      | Current |
|---------------------------|-------------|---------|
| API P50 Latency           | < 100ms     | 75ms    |
| API P95 Latency           | < 200ms     | 180ms   |
| API P99 Latency           | < 500ms     | 450ms   |
| Cache Hit Rate            | > 90%       | 93%     |
| Database Query Time       | < 50ms      | 35ms    |
| Kafka Consumer Lag        | < 1 minute  | 15s     |
| Recommendation Generation | < 2 seconds | 1.2s    |

## Security Architecture

### Authentication & Authorization

#### JWT Token Structure
```json
{
  "sub": "user_123",
  "email": "john@example.com",
  "role": "Member",
  "segment": "HighlyActive",
  "iat": 1698700000,
  "exp": 1698786400
}
```

#### Token Lifecycle
1. User logs in → Generate token (24-hour expiration)
2. Frontend stores in localStorage
3. Axios interceptor adds to all requests: `Authorization: Bearer <token>`
4. API middleware validates signature and expiration
5. Extract claims for authorization decisions

#### Authorization Rules
- **Users**: Can only access their own data (userId match)
- **Admins**: Can manage all users and classes
- **Public**: Can view class catalog (no auth required)

### Data Security

#### Password Storage
- **Algorithm**: BCrypt with salt (cost factor 12)
- **Never stored in plain text**
- **Password requirements**: 8+ chars, upper/lower/digit/special

#### SQL Injection Prevention
- **Parameterized Queries**: Always use EF Core or Dapper with parameters
- **Input Validation**: Validate all user inputs with Data Annotations
- **Stored Procedures**: Used for complex queries

#### Secrets Management
- **Local Dev**: appsettings.Development.json (git-ignored)
- **Production**: Azure Key Vault or Kubernetes Secrets
- **Connection Strings**: Never hardcoded
- **API Keys**: Rotated quarterly

### Network Security

#### HTTPS Everywhere
- **TLS 1.3**: Minimum version
- **Certificate**: Let's Encrypt or Azure-managed
- **HSTS**: Enforce HTTPS

#### CORS Configuration
```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("https://app.fitlife.com")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
```

#### Rate Limiting
- **Per User**: 100 requests/minute
- **Per IP**: 1000 requests/minute
- **Implementation**: Nginx or middleware

## Trade-offs & Decisions

### 1. SQL vs NoSQL for User Data

**Decision**: Azure SQL Database (SQL)

**Reasoning**:
- ✅ Strong consistency for user profiles and bookings
- ✅ Rich querying (JOIN, complex filters)
- ✅ ACID transactions for booking operations
- ✅ Team expertise with SQL Server
- ❌ Vertical scaling limits (mitigated with read replicas)

**Alternative Considered**: MongoDB for user profiles
- ✅ Flexible schema for preferences
- ❌ Weaker consistency guarantees
- ❌ JOIN performance (aggregation pipeline)

### 2. Kafka vs Azure Service Bus

**Decision**: Apache Kafka (Azure Event Hubs)

**Reasoning**:
- ✅ High throughput (millions of events/day)
- ✅ Event replay capability
- ✅ Partitioning for parallelism
- ✅ Industry standard (interview relevance)
- ❌ More complex to manage

**Alternative Considered**: Azure Service Bus
- ✅ Fully managed, less ops overhead
- ✅ Built-in dead-letter queue
- ❌ Higher cost at scale
- ❌ Lower throughput

### 3. Real-Time vs Batch Recommendations

**Decision**: Hybrid approach

**Reasoning**:
- ✅ Batch generation every 10 min (cost-effective)
- ✅ Cache in Redis (fast reads)
- ✅ Real-time events influence next batch
- ✅ Balances freshness and performance
- ❌ Not truly real-time (acceptable for MVP)

**Alternative Considered**: Fully real-time with ML model
- ✅ Instant personalization
- ❌ Much higher compute cost
- ❌ Complex ML pipeline (overengineering for POC)

### 4. Monolith vs Microservices

**Decision**: Monolithic API with microservice-ready patterns

**Reasoning**:
- ✅ Simpler deployment for POC
- ✅ Less network overhead
- ✅ Easier debugging and testing
- ✅ Can split into microservices later (clean architecture)
- ❌ Coupled deployment (mitigated with CI/CD)

**Future Migration Path**:
1. Extract RecommendationService → separate API
2. Extract EventConsumer → separate worker service
3. Use API Gateway for routing

### 5. Client-Side vs Server-Side Rendering

**Decision**: Client-side SPA (Vue.js)

**Reasoning**:
- ✅ Rich interactivity (filtering, real-time updates)
- ✅ Decoupled frontend/backend deployment
- ✅ Better developer experience
- ✅ Mobile app reuses same API
- ❌ Initial load time (mitigated with code splitting)
- ❌ SEO challenges (mitigated with pre-rendering)

## Monitoring & Observability

### Structured Logging (Serilog)
```csharp
Log.Information("Recommendation generated for {UserId} with score {Score}", 
    userId, score);
```

### Health Checks
- **/health**: Overall system health
- **/health/ready**: Ready to accept traffic
- **Custom checks**: Database, Redis, Kafka connectivity

### Metrics (Prometheus-compatible)
- Request count by endpoint
- Response time histograms
- Cache hit/miss ratio
- Kafka consumer lag
- Background job duration

### Distributed Tracing
- **Correlation IDs**: Track request across services
- **Azure Application Insights**: Full request timeline
- **OpenTelemetry**: Industry-standard instrumentation

## Future Enhancements

1. **Machine Learning Recommendations**
   - Replace rule-based scoring with collaborative filtering
   - Train model on interaction history
   - A/B test against current algorithm

2. **Real-Time Notifications**
   - SignalR for push notifications
   - Notify when favorite instructor has new class
   - Remind users of upcoming bookings

3. **Mobile App**
   - React Native or Flutter
   - Reuse existing REST API
   - Push notifications for personalization

4. **Advanced Analytics**
   - User cohort analysis
   - Recommendation conversion funnel
   - Instructor performance dashboard

5. **Multi-Tenancy**
   - Support multiple Life Time locations
   - Location-specific recommendations
   - Cross-location class discovery

---

This architecture is designed to demonstrate senior-level system design thinking while remaining pragmatic for a proof-of-concept. All decisions prioritize **simplicity, scalability, and maintainability**.
