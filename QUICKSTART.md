# 🚀 Quick Start - FitLife Demo

## Local Demo (5 minutes)

### Prerequisites
- Docker Desktop **running**
- .NET 8.0 SDK
- Node.js 20+

### Option 1: Automated Setup (Recommended)
```powershell
# Run the setup script
.\demo-start.ps1

# Then start backend (terminal 1)
cd FitLife.Api
dotnet run

# Then start frontend (terminal 2)
cd fitlife-web
npm install
npm run dev
```

### Option 2: Manual Setup
```powershell
# 1. Start infrastructure
docker compose up -d sqlserver redis zookeeper kafka

# 2. Setup database
cd FitLife.Api
dotnet ef database update
dotnet run --seed

# 3. Start backend
dotnet run  # http://localhost:5269

# 4. Start frontend (new terminal)
cd ..\fitlife-web
npm install
npm run dev  # http://localhost:3000
```

### Option 3: Full Docker (all-in-one)
```powershell
# Runs everything in containers — no local SDK needed
docker-compose up -d --build

# Wait for healthy containers (~60s)
docker ps

# Demo mode seeds the synthetic catalog and personas at startup.
```
- **Frontend**: http://localhost:3000
- **API/Swagger**: http://localhost:5269/swagger

For local SDK runs, the API serves HTTP only. Start separate Consumer and
Scheduler terminals using [Worker Topology](public-docs/Worker-Topology.md) when
those features are needed. Full Docker configures both workers; do not start an
additional local scheduler against its database.

## Demo members

Open the app and pick a persona. One click, no password:

| Persona | Shows |
|---|---|
| Sarah | Yoga regular with morning history: morning yoga with her instructor ranks first |
| Mike | Advanced, evening HIIT and strength history: those rank first |
| Emily | New member with one completed class: ranking leans on preferences and level |

Starting a session resets that persona, undoing earlier visitors' bookings.
Seeded accounts also accept the local-only password `Demo123!` on the sign-in
page.

## Endpoints
- **Frontend**: http://localhost:3000
- **API**: http://localhost:5269
- **Swagger**: http://localhost:5269/swagger
- **Health**: http://localhost:5269/health

## Troubleshooting

**Docker error?**
```powershell
docker ps  # Check Docker Desktop is running
docker-compose down -v && docker-compose up -d  # Restart
```

**Port in use?**
```powershell
netstat -ano | findstr :5269  # Find process
taskkill /PID <PID> /F  # Kill it
```

**Need full setup?** → See `DEMO_SETUP.md`

---

## Hosted Demo

There is no verified public hosted environment yet. Deployment is planned as a
separate, cost-bounded step; nothing here claims a live deployment.

---

## Architecture at a Glance

```
Vue 3 SPA → .NET 8 API → EF Core → SQL Server
             ↓
         Kafka → Background Workers → Redis Cache
```

**9-Factor Recommendation Algorithm**:
- Fitness Level Match (10 pts)
- Preferred Class Type (15 pts)
- Favorite Instructor (20 pts) ⭐
- Time Preference (8 pts)
- Class Rating (2× rating)
- Availability (+3 to -5 pts)
- User Segment Boost (12 pts)
- Recency Bonus (5 pts)
- Popularity Bonus (8 pts)

**User Segments**: Beginner, HighlyActive, YogaEnthusiast, StrengthTrainer, CardioLover, WeekendWarrior, General

---

## Project Structure

```
FitLife.Api/              # Web API + Background Services
FitLife.Core/             # Business logic + Scoring Engine
FitLife.Infrastructure/   # Repositories, Kafka, Redis, EF Core
FitLife.Tests/            # Unit + Integration tests
fitlife-web/              # Vue 3 SPA
k8s/                      # Kubernetes manifests
docs/                     # Architecture documentation
```

---

**Full Documentation**: See README.md and docs/ folder

**Questions?** Check DEMO_SETUP.md troubleshooting section
