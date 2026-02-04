# FitLife Local Demo - Quick Start Script
# Run this in PowerShell: .\demo-start.ps1
# Use -Fresh flag to force clean slate: .\demo-start.ps1 -Fresh

param(
    [switch]$Fresh  # Force clean slate even if containers are healthy
)

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

# Check if containers are already running and healthy
Write-Host ""
Write-Host "Step 1b: Checking existing infrastructure..." -ForegroundColor Yellow

$skipContainerSetup = $false

if ($Fresh) {
    Write-Host "Fresh start requested - cleaning up..." -ForegroundColor Yellow
    docker-compose down -v 2>&1 | Out-Null
} else {
    $sqlHealthy = (docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{end}}' fitlife-sqlserver 2>$null) -eq "healthy"
    $redisHealthy = (docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{end}}' fitlife-redis 2>$null) -eq "healthy"
    $kafkaRunning = (docker inspect --format='{{.State.Status}}' fitlife-kafka 2>$null) -eq "running"
    $zookeeperRunning = (docker inspect --format='{{.State.Status}}' fitlife-zookeeper 2>$null) -eq "running"

    if ($sqlHealthy -and $redisHealthy -and $kafkaRunning -and $zookeeperRunning) {
        Write-Host "All services already running - skipping container setup" -ForegroundColor Green
        $skipContainerSetup = $true
    } else {
        Write-Host "Some services missing or unhealthy - starting fresh..." -ForegroundColor Yellow
        docker-compose down -v 2>&1 | Out-Null
    }
}

if (-not $skipContainerSetup) {
    Write-Host ""
    Write-Host "Step 2: Starting infrastructure (SQL, Redis, Kafka)..." -ForegroundColor Yellow
    Write-Host "  (This may take 30-60 seconds on first run)" -ForegroundColor Gray
    docker-compose up -d 2>&1 | ForEach-Object { Write-Host "." -NoNewline -ForegroundColor Cyan }
    Write-Host ""
    Write-Host "Containers started" -ForegroundColor Green
}

# Function to check container health
function Wait-ForHealthyContainer {
    param([string]$containerName, [int]$timeoutSeconds = 120)
    
    $elapsed = 0
    $interval = 5
    
    Write-Host "Waiting for $containerName to be healthy..." -ForegroundColor Yellow -NoNewline
    
    while ($elapsed -lt $timeoutSeconds) {
        # Check if container exists and is running
        $status = docker inspect --format='{{.State.Status}}' $containerName 2>$null
        
        if ($status -ne "running") {
            Write-Host "." -ForegroundColor Yellow -NoNewline
            Start-Sleep -Seconds $interval
            $elapsed += $interval
            continue
        }
        
        # Check health status (if healthcheck is defined)
        $health = docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{end}}' $containerName 2>$null
        
        if ($health -eq "healthy") {
            Write-Host " OK (healthy)" -ForegroundColor Green
            return $true
        }
        elseif ([string]::IsNullOrEmpty($health)) {
            # No health check defined, just check if running for a few seconds
            if ($elapsed -ge 10) {
                Write-Host " OK (running)" -ForegroundColor Green
                return $true
            }
        }
        
        Write-Host "." -ForegroundColor Yellow -NoNewline
        Start-Sleep -Seconds $interval
        $elapsed += $interval
    }
    
    Write-Host " TIMEOUT" -ForegroundColor Red
    return $false
}

if (-not $skipContainerSetup) {
    Write-Host ""
    Write-Host "Step 3: Waiting for services to be healthy (this may take 1-2 minutes)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5  # Give containers a moment to initialize

    # Wait for each service in dependency order
    $services = @(
        @{Name="fitlife-zookeeper"; Timeout=45},
        @{Name="fitlife-redis"; Timeout=30},
        @{Name="fitlife-sqlserver"; Timeout=120},
        @{Name="fitlife-kafka"; Timeout=60}
    )

    $allHealthy = $true
    foreach ($service in $services) {
        if (-not (Wait-ForHealthyContainer -containerName $service.Name -timeoutSeconds $service.Timeout)) {
            Write-Host "ERROR $($service.Name) failed to become healthy!" -ForegroundColor Red
            Write-Host "Check logs: docker logs $($service.Name)" -ForegroundColor Yellow
            $allHealthy = $false
            break
        }
    }

    if (-not $allHealthy) {
        Write-Host ""
        Write-Host "Troubleshooting:" -ForegroundColor Yellow
        Write-Host "1. Clean restart: docker-compose down -v && docker-compose up -d" -ForegroundColor Gray
        Write-Host "2. Check Docker resources: Ensure Docker has enough memory (4GB+)" -ForegroundColor Gray
        Write-Host "3. View logs: docker-compose logs" -ForegroundColor Gray
        exit 1
    }
}

Write-Host ""
Write-Host "Step 4: Applying database migrations..." -ForegroundColor Yellow
Push-Location FitLife.Api
try {
    dotnet ef database update 2>&1 | Out-Null
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
dotnet build --no-restore --verbosity quiet
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