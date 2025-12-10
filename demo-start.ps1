# FitLife Local Demo - Quick Start Script
# Run this in PowerShell: .\demo-start.ps1

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "FitLife Demo Setup - Local" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check Docker is running
Write-Host "Step 1: Checking Docker..." -ForegroundColor Yellow
try {
    docker ps | Out-Null
    Write-Host "OK Docker is running" -ForegroundColor Green
} catch {
    Write-Host "ERROR Docker is not running!" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and run this script again." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Starting infrastructure (SQL, Redis, Kafka)..." -ForegroundColor Yellow
docker-compose up -d

Write-Host "Waiting 30 seconds for services to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Verify containers
Write-Host ""
Write-Host "Step 3: Verifying containers..." -ForegroundColor Yellow
$containers = docker ps --format "{{.Names}}"
$required = @("fitlife-sqlserver", "fitlife-redis", "fitlife-kafka", "fitlife-zookeeper")

foreach ($container in $required) {
    if ($containers -contains $container) {
        Write-Host "OK $container is running" -ForegroundColor Green
    } else {
        Write-Host "ERROR $container is NOT running!" -ForegroundColor Red
        Write-Host "Try: docker-compose down; docker-compose up -d" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "Step 4: Applying database migrations..." -ForegroundColor Yellow
Push-Location FitLife.Api
try {
    dotnet ef database update
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK Migrations applied successfully" -ForegroundColor Green
    } else {
        Write-Host "ERROR Migration failed!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
} catch {
    Write-Host "ERROR Migration failed: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host ""
Write-Host "Step 5: Seeding sample data..." -ForegroundColor Yellow
dotnet build --no-restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-build --seed --no-launch-profile
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK Data seeded successfully" -ForegroundColor Green
} else {
    Write-Host "ERROR Seeding failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "OK Setup Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Start Backend API:" -ForegroundColor White
Write-Host "   cd FitLife.Api; dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Start Frontend (in new terminal):" -ForegroundColor White
Write-Host "   cd fitlife-web; npm install; npm run dev" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Open browser:" -ForegroundColor White
Write-Host "   http://localhost:3000" -ForegroundColor Gray
Write-Host ""
Write-Host "Demo Users (password: Demo123!):" -ForegroundColor Cyan
Write-Host "  - sarah.johnson@example.com (Yoga Enthusiast)" -ForegroundColor White
Write-Host "  - mike.chen@example.com (Highly Active)" -ForegroundColor White
Write-Host "  - emily.rodriguez@example.com (Beginner)" -ForegroundColor White
Write-Host "  - david.kim@example.com (Cardio Lover)" -ForegroundColor White
Write-Host "  - jessica.taylor@example.com (Strength Trainer)" -ForegroundColor White
Write-Host ""
Write-Host "Swagger UI: http://localhost:5269/swagger" -ForegroundColor Gray
Write-Host ""
