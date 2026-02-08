# FitLife Demo Setup Guide

This guide walks you through setting up FitLife for both **local** and **hosted** demos.

## 📋 Prerequisites

### Local Demo
- ✅ Docker Desktop installed and **running**
- ✅ .NET 8.0 SDK
- ✅ Node.js 20+
- ✅ Git

### Hosted Demo (Azure)
- ✅ Azure CLI installed
- ✅ Azure subscription (free tier works)
- ✅ kubectl installed
- ✅ Docker Desktop

---

## 🖥️ Local Demo Setup

### Step 1: Start Docker Desktop
```powershell
# Verify Docker is running
docker --version
docker ps
```

**If you get an error**: Open Docker Desktop app and wait for it to start completely.

### Step 2: Clone and Navigate
```powershell
cd c:\Code\fitlife-personalization-engine
```

### Step 3: Start Infrastructure
```powershell
# Start SQL Server, Redis, Kafka, Zookeeper
docker-compose up -d

# Wait ~30 seconds for services to be ready
Start-Sleep -Seconds 30

# Verify all containers are running
docker ps
# You should see: fitlife-sqlserver, fitlife-redis, fitlife-kafka, fitlife-zookeeper
```

### Step 4: Setup Backend Database
```powershell
cd FitLife.Api

# Apply database migrations
dotnet ef database update

# Seed sample data (5 users, 10 classes, interactions)
dotnet run --seed

# Verify seeding worked
# You should see: "Database seeded successfully!"
```

### Step 5: Start Backend API
```powershell
# Still in FitLife.Api directory
dotnet run

# API will start at: http://localhost:5269
# Swagger UI: http://localhost:5269/swagger
```

**Keep this terminal open!**

### Step 6: Start Frontend (New Terminal)
```powershell
# Open NEW PowerShell terminal
cd c:\Code\fitlife-personalization-engine\fitlife-web

# Install dependencies (first time only)
npm install

# Start dev server
npm run dev

# Frontend will start at: http://localhost:3000
```

**Keep this terminal open too!**

### Step 7: Test the Demo

**Open browser**: http://localhost:3000

**Demo User Credentials** (all use password: `Demo123!`):
1. **Sarah Johnson** - Yoga Enthusiast
   - Email: `sarah.johnson@example.com`
   - Shows Yoga/Pilates recommendations

2. **Mike Chen** - Highly Active
   - Email: `mike.chen@example.com`
   - Shows HIIT/Strength/Spin recommendations

3. **Emily Rodriguez** - Beginner
   - Email: `emily.rodriguez@example.com`
   - Shows beginner-friendly classes

4. **David Kim** - Cardio Lover
   - Email: `david.kim@example.com`
   - Shows Spin/Running/Cardio recommendations

5. **Jessica Taylor** - Strength Trainer
   - Email: `jessica.taylor@example.com`
   - Shows Strength/HIIT recommendations

**Demo Flow**:
1. Login with one of the demo users
2. View personalized recommendations on dashboard
3. Browse class catalog with filters (type, level)
4. Book a class from the catalog
5. Refresh recommendations to see personalization adapt
6. Update preferences in profile page
7. Check different user profiles to see personalization differences

### Troubleshooting Local Demo

**Docker not starting?**
```powershell
# Check Docker Desktop is running
docker info

# Restart Docker Compose
docker-compose down
docker-compose up -d
```

**Database connection failed?**
```powershell
# Check SQL Server is running
docker logs fitlife-sqlserver

# Verify connection string in appsettings.json matches docker-compose.yml
# Default: Server=localhost,1433;Database=FitLifeDb;User Id=sa;Password=YourStrong@Passw0rd;
```

**Kafka errors?**
```powershell
# Check Kafka is running
docker logs fitlife-kafka

# Verify events are being produced
docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning
```

**Redis not connecting?**
```powershell
# Check Redis is running
docker exec -it fitlife-redis redis-cli ping
# Should return: PONG

# View cached recommendations
docker exec -it fitlife-redis redis-cli
> KEYS rec:*
> GET rec:user_001
```

**Frontend can't reach API?**
```powershell
# Verify CORS is configured correctly
# Check appsettings.json: "Cors:AllowedOrigins" includes "http://localhost:3000"

# Test API directly
Invoke-RestMethod -Uri "http://localhost:5269/health"
```

---

## ☁️ Hosted Demo Setup (Azure)

### Option A: Full Azure Deployment (~45 minutes)

**Estimated Monthly Cost**: $50-100 (smallest instances)

#### Step 1: Azure Login & Resource Group
```bash
# Login to Azure
az login

# Create resource group
az group create --name fitlife-rg --location eastus
```

#### Step 2: Create Azure Container Registry
```bash
# Create ACR (Basic tier for demo)
az acr create \
  --name fitlifeacr \
  --resource-group fitlife-rg \
  --sku Basic

# Enable admin access
az acr update --name fitlifeacr --admin-enabled true

# Get credentials
az acr credential show --name fitlifeacr --resource-group fitlife-rg
# Save username and password for GitHub secrets
```

#### Step 3: Create Azure SQL Database
```bash
# Create SQL Server
az sql server create \
  --name fitlife-sql-server \
  --resource-group fitlife-rg \
  --location eastus \
  --admin-user fitlifeadmin \
  --admin-password '<YourSecurePassword123!>'

# Allow Azure services to access
az sql server firewall-rule create \
  --resource-group fitlife-rg \
  --server fitlife-sql-server \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create database (S0 tier for demo)
az sql db create \
  --name FitLifeDb \
  --server fitlife-sql-server \
  --resource-group fitlife-rg \
  --service-objective S0
```

#### Step 4: Create AKS Cluster
```bash
# Create AKS cluster (smallest node size for demo)
az aks create \
  --name fitlife-aks \
  --resource-group fitlife-rg \
  --node-count 2 \
  --node-vm-size Standard_B2s \
  --attach-acr fitlifeacr \
  --enable-managed-identity \
  --generate-ssh-keys

# Get credentials
az aks get-credentials \
  --resource-group fitlife-rg \
  --name fitlife-aks
```

#### Step 5: Build and Push Docker Images
```powershell
# Login to ACR
az acr login --name fitlifeacr

# Build and push API image
docker build -t fitlifeacr.azurecr.io/fitlife-api:latest -f FitLife.Api/Dockerfile .
docker push fitlifeacr.azurecr.io/fitlife-api:latest

# Build and push Web image
docker build -t fitlifeacr.azurecr.io/fitlife-web:latest -f fitlife-web/Dockerfile ./fitlife-web
docker push fitlifeacr.azurecr.io/fitlife-web:latest
```

#### Step 6: Update Kubernetes Manifests
```powershell
# Update k8s/secrets.yaml with actual values
# Base64 encode connection strings:
$sqlConnString = "Server=fitlife-sql-server.database.windows.net,1433;Database=FitLifeDb;User Id=fitlifeadmin;Password=<YourPassword>;TrustServerCertificate=True;"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($sqlConnString)
$encoded = [Convert]::ToBase64String($bytes)
Write-Output $encoded

# Update k8s manifests:
# 1. Replace <ACR_REGISTRY> with fitlifeacr.azurecr.io in deployment files
# 2. Update secrets.yaml with encoded connection strings
```

#### Step 7: Deploy to Kubernetes
```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Apply secrets and config
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml

# Deploy applications
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml

# Apply HPA (auto-scaling)
kubectl apply -f k8s/hpa.yaml

# Apply ingress (for external access)
kubectl apply -f k8s/ingress.yaml

# Wait for deployments
kubectl rollout status deployment/fitlife-api -n fitlife --timeout=5m
kubectl rollout status deployment/fitlife-web -n fitlife --timeout=5m
```

#### Step 8: Apply Database Migrations
```bash
# Get API pod name
kubectl get pods -n fitlife

# Exec into API pod
kubectl exec -it <api-pod-name> -n fitlife -- /bin/bash

# Run migrations
dotnet ef database update
dotnet run --seed

# Exit pod
exit
```

#### Step 9: Get Public URL
```bash
# Get external IP
kubectl get service fitlife-web-service -n fitlife

# Or if using Ingress:
kubectl get ingress -n fitlife

# Access at: http://<EXTERNAL-IP>
```

#### Step 10: Setup GitHub Actions (Optional)
```bash
# Create service principal for GitHub Actions
az ad sp create-for-rbac \
  --name "fitlife-github-actions" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/fitlife-rg \
  --sdk-auth

# Add to GitHub Secrets:
# - AZURE_CREDENTIALS (JSON output from above)
# - ACR_USERNAME (from step 2)
# - ACR_PASSWORD (from step 2)

# Enable workflow by removing `if: false` from .github/workflows/deploy.yml
```

---

### Option B: Quick Deploy with Railway/Render (~15 minutes)

**Estimated Monthly Cost**: $0-20 (free tier available)

#### Railway.app (Recommended for Speed)

1. **Sign up**: https://railway.app (GitHub login)

2. **Create New Project** → "Deploy from GitHub"

3. **Add Services**:
   - **PostgreSQL** (Railway provides managed Postgres - easier than SQL Server)
     - Auto-provisioned, connection string auto-injected
   
   - **Redis** (Railway Plugin)
     - One-click add from marketplace
   
   - **API Service**:
     - Root directory: `/FitLife.Api`
     - Build command: `dotnet publish -c Release -o out`
     - Start command: `dotnet out/FitLife.Api.dll --seed`
     - Port: `8080`
   
   - **Web Service**:
     - Root directory: `/fitlife-web`
     - Build command: `npm install && npm run build`
     - Start command: `npm run preview`
     - Port: `3000`

4. **Environment Variables** (API Service):
   ```
   ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
   Redis__ConnectionString=${{Redis.REDIS_URL}}
   Kafka__BootstrapServers=localhost:9092  # Not ideal, but works for demo
   Jwt__Secret=your-super-secret-jwt-key-min-32-chars
   ```

5. **Deploy**: Automatic on git push

6. **Public URL**: Railway provides `https://<your-app>.railway.app`

**Note**: Railway doesn't have managed Kafka. For full functionality, you can:
- Use Upstash Kafka (serverless, free tier): https://upstash.com
- Or disable background workers temporarily for demo

#### Render.com Alternative

Similar process to Railway:
1. Create Blueprint or individual services
2. Connect GitHub repo
3. Add PostgreSQL, Redis from Render marketplace
4. Deploy with environment variables
5. Get public URL: `https://<your-app>.onrender.com`

---

## 🎬 Demo Script

### 5-Minute Demo Flow

**Intro (30 seconds)**:
> "FitLife is an AI-powered gym class recommendation system that uses a 9-factor scoring algorithm to personalize recommendations based on user preferences, fitness level, historical behavior, and real-time availability."

**Login (30 seconds)**:
- Login as Sarah Johnson (Yoga Enthusiast)
- Show dashboard with personalized recommendations

**Explain Personalization (1 minute)**:
- Point out recommendations are mostly Yoga/Pilates classes
- Show scoring explanation on each recommendation card: "Because you love Yoga classes and you enjoy classes with Sarah Martinez"
- Highlight class info: rating, availability, instructor

**Show Behavior Tracking (1 minute)**:
- Book a HIIT class (outside usual preferences)
- Refresh recommendations
- Note: System adapts - now showing more HIIT classes mixed with Yoga

**Multi-User Demo (1.5 minutes)**:
- Logout, login as Mike Chen (Highly Active)
- Show completely different recommendations: HIIT, Spin, Strength
- Highlight: "Same system, different user, personalized experience"

**Technical Deep Dive (1 minute)** (optional for technical audience):
- Open Swagger UI: `http://localhost:5269/swagger`
- Show API endpoints
- Demonstrate real-time event tracking endpoint
- Show caching strategy (Redis inspection)

**Wrap-up (30 seconds)**:
> "FitLife demonstrates event-driven architecture, caching strategies, background job processing, and personalization algorithms - all production-ready patterns for real-world recommendation systems."

---

## 📊 Demo Talking Points

### Architecture Highlights
- **Event-Driven**: User actions → Kafka → Background workers → Database
- **Caching Layer**: Redis with 10-min TTL for fast recommendations
- **Batch Processing**: Recommendations generated every 10 minutes for all active users
- **Kubernetes-Ready**: HPA, health checks, graceful shutdown

### Algorithm Highlights
- **9 Factors**: Fitness level, preferences, instructor, time, rating, availability, segment, recency, popularity
- **Weighted Scoring**: Instructor preference weighted highest (20 points)
- **User Segmentation**: Beginner, HighlyActive, YogaEnthusiast, etc.
- **Explainable AI**: Every recommendation includes human-readable reason

### Tech Stack
- **Backend**: .NET 8, EF Core, Kafka, Redis
- **Frontend**: Vue 3, TypeScript, Pinia, Tailwind CSS
- **Infrastructure**: Docker, Kubernetes, Azure (AKS, ACR, SQL)
- **CI/CD**: GitHub Actions (automated testing, build, deploy)

---

## 🛠️ Demo Customization

### Add Your Own Data

**Add a new user**:
```sql
INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, FitnessLevel, Goals, PreferredClassTypes, Segment, CreatedAt)
VALUES ('user_006', 'your.email@example.com', '<bcrypt-hash>', 'Your', 'Name', 'Intermediate', '["Goal1","Goal2"]', '["Yoga"]', 'General', GETUTCDATE());
```

**Add a new class**:
```sql
INSERT INTO Classes (Id, Name, Type, InstructorId, InstructorName, Level, Description, StartTime, Duration, Capacity, CurrentEnrollment, AverageRating, WeeklyBookings, IsActive, CreatedAt)
VALUES ('class_011', 'New Class', 'Yoga', 'inst_sarah', 'Sarah Martinez', 'All Levels', 'Description', DATEADD(day, 1, GETUTCDATE()), 60, 30, 0, 4.5, 0, 1, GETUTCDATE());
```

### Modify Scoring Weights
Edit `FitLife.Core/Services/ScoringEngine.cs` to adjust factor weights for your use case.

---

## 🚨 Common Issues & Solutions

### Docker Containers Won't Start
```powershell
# Check Docker Desktop is running
docker info

# Remove old containers and volumes
docker-compose down -v
docker-compose up -d
```

### Port Already in Use
```powershell
# Find process using port
netstat -ano | findstr :5269

# Kill process (replace PID)
taskkill /PID <PID> /F
```

### Migration Failed
```powershell
# Drop and recreate database
docker-compose down -v
docker-compose up -d
Start-Sleep -Seconds 30
dotnet ef database update
dotnet run --seed
```

### Recommendations Not Showing
```powershell
# Check background workers are running
# View API logs for "RecommendationGeneratorService started"

# Manually trigger recommendation generation
Invoke-RestMethod -Uri "http://localhost:5269/api/recommendations/user_001/refresh" -Headers @{Authorization="Bearer <token>"}
```

---

## ✅ Pre-Demo Checklist

**Before presenting**:
- [ ] Docker Desktop is running
- [ ] All 4 containers are healthy: `docker ps`
- [ ] API is running: http://localhost:5269/health returns 200
- [ ] Frontend is running: http://localhost:3000 loads
- [ ] Can login with demo user
- [ ] Recommendations are showing
- [ ] Browser dev tools closed (for clean demo)
- [ ] Swagger UI tested (if showing technical audience)
- [ ] Demo script reviewed

**Have Ready**:
- [ ] Demo user credentials written down
- [ ] Architecture diagram (docs/ARCHITECTURE.md)
- [ ] Scoring algorithm explanation (docs/RECOMMENDATIONS.md)
- [ ] Terminal windows positioned for quick switching
- [ ] Browser tabs: Frontend, Swagger UI, GitHub repo

---

## 🎓 Further Learning

**Dive Deeper**:
- `docs/ARCHITECTURE.md` - System design details
- `docs/RECOMMENDATIONS.md` - Scoring algorithm deep-dive
- `docs/API.md` - Complete API reference
- `docs/DATABASE.md` - Schema and indexes
- `docs/DEPLOYMENT.md` - Production deployment guide

**Extend the Project**:
- Add real-time notifications (SignalR)
- Integrate ML model (collaborative filtering)
- Add social features (friends, sharing)
- Implement A/B testing framework
- Add analytics dashboard

---

**Questions?** Check troubleshooting sections or review the codebase documentation.

**Ready to demo!** 🚀
