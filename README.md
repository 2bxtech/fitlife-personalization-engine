# FitLife Personalization Engine

An explainable, event-driven gym-class personalization case study built with
.NET 8, Vue 3, SQL Server, Redis, and Kafka.

![A demo member's top recommendation with its nine-factor score breakdown](public-docs/images/dashboard-breakdown.png)

FitLife combines member preferences, fitness level, instructor affinity,
schedule, class availability, ratings, recency, popularity, and behavior-derived
segments to produce ranked recommendations with human-readable reasons. The
current engine is intentionally deterministic: it demonstrates transparent
decision-system design without presenting a trained model or an LLM as a
requirement.

> **Project status:** active portfolio case study. The application and its local
> Docker environment are implemented and tested. A public hosted environment has
> not yet been verified. Kubernetes and disabled Azure deployment assets are
> configuration evidence, not a claim of a live AKS deployment.

## See it in action

```powershell
docker compose -f docker-compose.yml -f docker-compose.minimal.yml up -d --build api scheduler web
```

Open <http://localhost:3000>, then pick a demo member. One click starts a
session, with no sign-up. Open **Why #1?** on any card to see each rule's
points, book a class, then use **View as** to watch the same catalog rank
differently for another member. [Demo guide](DEMO_SETUP.md) has a five-minute
script and the evidence behind each claim.

| Mike's ranking (evening HIIT and strength) | Mobile |
|---|---|
| ![Mike's recommendations](public-docs/images/dashboard-mike.png) | ![Emily's recommendations on a phone](public-docs/images/mobile-dashboard.png) |

## What this project demonstrates

- Full-stack delivery with ASP.NET Core, EF Core, Vue, TypeScript, Pinia, and
  Tailwind CSS.
- Explainable personalization through a nine-factor deterministic scorer.
- Authorization boundaries for members and catalog operators.
- Durable, transactional booking with database-enforced uniqueness and capacity
  invariants.
- Safe booking retries, optimistic concurrency, idempotency keys, and
  exactly-once capacity restoration on cancellation.
- Redis cache-aside behavior with post-commit invalidation.
- Kafka-based interaction publishing and consumption.
- Health checks, production configuration validation, rate limiting, and
  correlation IDs.
- Reproducible backend and frontend validation in GitHub Actions.

## Product flow

1. A visitor picks a synthetic demo member (the session resets that member), or
   a member registers or signs in.
2. The API ranks upcoming classes with available capacity using profile and
   interaction data.
3. Each recommendation includes a one-line reason built only from rules that
   scored, and the full factor breakdown behind its score.
4. The member books or cancels a class.
5. Booking state, enrollment, and the corresponding interaction are committed
   atomically.
6. The UI updates the booked card from the booking response at once. The
   re-rank runs afterwards, and if it fails the booking still stands.

Booking state is scoped to the authenticated member. One member cannot inspect
or cancel another member's booking through the class API. Classes with no
remaining capacity are excluded from recommendations. A member's existing
booking remains visible in the class catalog so it can still be cancelled.

## Architecture

```mermaid
flowchart LR
    SPA[Vue 3 SPA] --> API[ASP.NET Core API]
    API --> CORE[Domain services]
    CORE --> SQL[(SQL Server)]
    CORE --> REDIS[(Redis)]
    API --> KAFKA[Kafka]
    KAFKA --> CONSUMER[Event consumer]
    CONSUMER --> SQL
    WORKERS[Scheduled personalization workers] --> CORE
```

The repository is organized as a modular monolith:

```text
FitLife.Api/              HTTP API and separate consumer/scheduler process roles
FitLife.Core/             Domain models, DTOs, scoring and recommendation logic
FitLife.Infrastructure/   EF Core, SQL Server, Redis, Kafka, repositories
FitLife.Tests/            Unit, integration, migration and SQL invariant tests
fitlife-web/              Vue 3 and TypeScript SPA
k8s/                      Configured Kubernetes manifests; not verified live
public-docs/              Public architecture and decision records
scripts/verify.ps1        Reproducible repository validation
```

### Important engineering decisions

| Concern | Implemented approach |
|---|---|
| Personalization | Deterministic weighted scoring with readable reasons |
| Booking uniqueness | SQL Server filtered unique index for active member/class bookings |
| Capacity concurrency | Transactional updates plus SQL Server rowversion |
| Retry safety | Stable duplicate result and optional `Idempotency-Key` |
| Cancellation | Transactional status change, capacity restoration, and `Cancel` interaction |
| Authorization | Caller ownership checks and a named operator policy for catalog mutations |
| Recommendation cache | Redis cache-aside with invalidation only after committed changes |
| Health | Dependency-free liveness and SQL/Redis-aware readiness |

See [Architecture](public-docs/Architecture.md) and
[Design Decisions](public-docs/FitLife-Decisions.md) for the longer rationale.

## Recommendation model

The scorer combines nine explicit factors:

| Factor | Weight or range | Signal |
|---|---:|---|
| Favorite instructor | 20 | Completed-class instructor affinity |
| Preferred class type | 15 | Member-selected preferences |
| Segment alignment | 12 | Behavior-derived member segment |
| Fitness-level match | 10 | Class/member difficulty alignment |
| Time preference | 8 | Start hours of classes the member booked |
| Popularity | Up to 8 | Recent class demand |
| Recency | Up to 5 | Time until class starts |
| Availability | -5 to +3 | Remaining-capacity pressure |
| Class rating | Rating x 2 | Aggregate member rating |

Each recommendation carries the full factor breakdown, and its one-line reason is
generated only from factors that scored. See
[Recommendation model](public-docs/Recommendations.md).

These weights are product rules, not learned parameters. Performance values
described elsewhere in the repository are targets unless accompanied by a
retained, reproducible measurement.

## Run locally

### Prerequisites

- Docker Desktop
- .NET 8 SDK
- Node.js 20 or later

### 1. Start local dependencies

```powershell
docker compose up -d sqlserver redis zookeeper kafka
```

This starts SQL Server on port `1433`, Redis on `6380`, and Kafka on `9092`.

Kafka is optional for the demo. Omit `zookeeper kafka` and start the API with
`dotnet run --project FitLife.Api -- --Events:Transport=Direct` to persist
events in-request instead; see
[Kafka-free minimal demo](public-docs/Worker-Topology.md#kafka-free-minimal-demo)
for the trade-offs.

### 2. Apply migrations and seed demo data

The API applies pending EF Core migrations at startup. When `Demo:Enabled` is
true (the default in the local launch profiles and Compose), it also seeds the
synthetic catalog and personas at startup. Otherwise, seed once:

```powershell
dotnet run --project FitLife.Api --seed
```

#### Demo personas

Three synthetic members (`sarah`, `mike`, `emily`) have fixed histories chosen so
the scheduled profiler assigns the segment each is seeded with.
`POST /api/demo/personas/{id}/session` resets that persona and returns a Member
token:

- the profile and interaction history are restored, and stored
  recommendations are deleted so the next read regenerates them;
- the persona's active bookings are cancelled;
- class enrollment is recomputed from active bookings.

Every visitor therefore starts from the same, reproducible recommendations.
**Verified in tests:** two personas get disjoint top-three results, the profiler
agrees with every seeded segment, and a new session undoes the previous visitor's
booking. The routes return 404 unless `Demo:Enabled` is true, and a session never
grants operator access. Visitors on the same persona share one member, so the
latest session resets it.

### 3. Start the API

```powershell
dotnet run --project FitLife.Api
```

- API: `http://localhost:5269`
- Swagger: `http://localhost:5269/swagger`
- Liveness: `http://localhost:5269/health/live`
- Readiness: `http://localhost:5269/health/ready`

The API runs no background workers. To enable event ingestion and scheduled
personalization, start the Consumer and Scheduler roles in separate terminals as
described in [Worker Topology](public-docs/Worker-Topology.md). On-demand
recommendations and transactional booking continue to run in the API.

### 4. Start the SPA

```powershell
cd fitlife-web
npm ci --legacy-peer-deps
npm run dev
```

Open `http://localhost:3000`.

For the guided setup and seeded persona list, see
[Quick Start](QUICKSTART.md) and [Demo Setup](DEMO_SETUP.md).

## Verify the repository

Run the same backend/frontend quality gate locally:

```powershell
.\scripts\verify.ps1
```

The script restores and builds the solution, runs backend tests, installs
frontend dependencies, runs lint and frontend tests, builds the production SPA,
and audits production npm dependencies.

SQL Server-specific booking tests run when
`FITLIFE_SQLSERVER_TEST_CONNECTION` points to a disposable SQL Server database
host. They create and remove isolated test databases:

```powershell
$env:FITLIFE_SQLSERVER_TEST_CONNECTION = "<SQL Server test connection>"
dotnet test FitLife.Tests --filter "FullyQualifiedName~BookingConcurrencyTests"
```

CI runs these SQL Server tests against a disposable container on every push and
pull request, and fails if any backend test is skipped.

### End-to-end demo journey

Playwright drives the demo in desktop and mobile browsers against a real API
(SQL Server, Redis, and the Kafka-free event transport). It covers:

- the persona journey: reasons, the factor breakdown, booking, switching
  personas, and the reset on re-entry;
- a booking whose follow-up re-rank fails;
- keyboard use, including the skip link and focus after navigation;
- the mobile menu;
- axe WCAG 2.1 AA scans (no serious or critical violations).

CI runs this suite as the `e2e` job. Locally, with the API running on `:5269`:

```powershell
cd fitlife-web
npm run build; npm run preview   # serves :4173 and proxies /api to :5269
npm run test:e2e
```

Test counts change with every PR; the CI run on `main` is the current record.
None of this is a production performance or availability claim.

## API surface

| Area | Routes |
|---|---|
| Authentication | `POST /api/auth/register`, `POST /api/auth/login` |
| Members | `GET /api/users/{id}`, `PUT /api/users/{id}/preferences`, `DELETE /api/users/{id}` |
| Classes | `GET /api/classes`, `GET /api/classes/{id}`, `GET /api/classes/popular` |
| Booking | `POST /api/classes/{id}/book`, `POST /api/classes/{id}/cancel` |
| Catalog management | `POST /api/classes`, `PUT /api/classes/{id}`, `DELETE /api/classes/{id}` |
| Recommendations | `GET /api/recommendations/{userId}`, `POST /api/recommendations/{userId}/refresh` |
| Events | `POST /api/events`, `POST /api/events/batch` |
| Operations | `GET /health/live`, `GET /health/ready`, `GET /health` |

Swagger provides the complete request and response schemas in Development.

## Security and data integrity

- Passwords are hashed with BCrypt.
- JWT authentication protects member operations.
- User, recommendation, and event routes enforce subject ownership.
- Catalog mutations require the `ManageCatalog` operator policy.
- Client-supplied registration data cannot grant the operator role.
- Production startup rejects placeholder secrets, local dependency endpoints,
  and unsafe demo-operator configuration.
- Booking foreign keys, status values, active uniqueness, capacity bounds, and
  idempotency-key uniqueness are enforced in SQL Server.
- Booking creation and cancellation invalidate recommendation cache entries only
  after the database transaction commits.

FitLife is a demonstration system and should be used only with seeded or
synthetic data. It is not intended to collect real health information.

## Delivery status

### Verified in the repository

- Backend build and tests.
- Frontend lint, tests, and production build.
- Production npm dependency audit.
- SQL Server booking concurrency and forced-failure rollback behavior.
- Authorization denial and allowance paths.
- Liveness/readiness behavior under dependency failure.

### Implemented, with further hardening planned

- Kafka producer and consumer with a versioned event envelope, broker-acknowledged
  publishing, and SQL-enforced event deduplication.
- Scheduled recommendation generation and user profiling.
- Kubernetes manifests and horizontal-scaling configuration.

The Kafka consumer applies three bounded processing attempts and publishes
metadata-only poison-event records to `user-events-dlq` before committing the
source offset. API, consumer, and scheduler run in separate process roles.
Compose and Kubernetes configure one scheduled owner; the scheduler must not be
scaled or duplicated against the same database. See [Worker Topology](public-docs/Worker-Topology.md)
for configuration, shutdown behavior, and ownership limits.

### Not currently claimed

- A live public FitLife deployment.
- A live AKS environment.
- Measured latency, throughput, cache-hit, availability, or consumer-lag
  results.
- Real users, adoption, retention improvement, or commercial use.

GitHub Actions currently runs validation. Image publishing and Azure deployment
jobs remain intentionally disabled until a deployment target is selected and
provisioned.

## Technology

- .NET 8, ASP.NET Core, EF Core 9, SQL Server
- Vue 3, TypeScript, Pinia, Vite, Tailwind CSS
- Redis, Kafka, Docker Compose
- xUnit, Vitest, FluentAssertions, Moq
- GitHub Actions
- Configured Kubernetes and Azure-oriented deployment assets

## Documentation

- [Quick Start](QUICKSTART.md)
- [Demo Setup](DEMO_SETUP.md)
- [Architecture](public-docs/Architecture.md)
- [Design Decisions](public-docs/FitLife-Decisions.md)
- [Recommendation Model](public-docs/Recommendations.md)

## License

[MIT](LICENSE)
