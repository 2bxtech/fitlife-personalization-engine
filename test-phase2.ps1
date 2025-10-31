# Test script for Phase 2 Kafka event publishing
# Run this after starting the API with: dotnet run

Write-Host "=== FitLife Phase 2 Testing ===" -ForegroundColor Cyan
Write-Host ""

# 1. Test health endpoint
Write-Host "1. Testing health endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5269/health" -Method Get
    Write-Host "   ✓ Health check passed" -ForegroundColor Green
    $health | ConvertTo-Json
} catch {
    Write-Host "   ✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 2. Register a test user
Write-Host "2. Registering test user..." -ForegroundColor Yellow
$registerBody = @{
    email = "testuser@lifetime.com"
    password = "TestPass123!"
    firstName = "Test"
    lastName = "User"
    fitnessLevel = "Intermediate"
    goals = @("Weight Loss", "Strength")
    preferredClassTypes = @("Yoga", "HIIT")
} | ConvertTo-Json

try {
    $registerResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/auth/register" `
        -Method Post `
        -Body $registerBody `
        -ContentType "application/json"
    
    $token = $registerResponse.data.token
    $userId = $registerResponse.data.user.id
    
    Write-Host "   ✓ User registered: $userId" -ForegroundColor Green
    Write-Host "   Token: $($token.Substring(0, 20))..." -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Registration failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Attempting login instead..." -ForegroundColor Yellow
    
    # Try login if user exists
    $loginBody = @{
        email = "testuser@lifetime.com"
        password = "TestPass123!"
    } | ConvertTo-Json
    
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json"
    
    $token = $loginResponse.data.token
    $userId = $loginResponse.data.user.id
    Write-Host "   ✓ User logged in: $userId" -ForegroundColor Green
}

Write-Host ""

# 3. Track a View event
Write-Host "3. Tracking View event..." -ForegroundColor Yellow
$viewEvent = @{
    userId = $userId
    itemId = "class_001"
    itemType = "Class"
    eventType = "View"
    metadata = @{
        source = "browse"
        page = 1
    }
} | ConvertTo-Json

try {
    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }
    
    $viewResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events" `
        -Method Post `
        -Headers $headers `
        -Body $viewEvent
    
    Write-Host "   ✓ View event tracked" -ForegroundColor Green
    Write-Host "   Response: $($viewResponse.message)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Event tracking failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 4. Track a Book event
Write-Host "4. Tracking Book event..." -ForegroundColor Yellow
$bookEvent = @{
    userId = $userId
    itemId = "class_001"
    itemType = "Class"
    eventType = "Book"
    metadata = @{
        bookingId = "booking_12345"
        source = "detail_page"
    }
} | ConvertTo-Json

try {
    $bookResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events" `
        -Method Post `
        -Headers $headers `
        -Body $bookEvent
    
    Write-Host "   ✓ Book event tracked" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Event tracking failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 5. Track batch events
Write-Host "5. Tracking batch events..." -ForegroundColor Yellow
$batchEvents = @(
    @{
        userId = $userId
        itemId = "class_002"
        eventType = "View"
        metadata = @{ source = "recommendations" }
    },
    @{
        userId = $userId
        itemId = "class_003"
        eventType = "Click"
        metadata = @{ source = "search" }
    },
    @{
        userId = $userId
        itemId = "class_004"
        eventType = "View"
        metadata = @{ source = "popular" }
    }
) | ConvertTo-Json

try {
    $batchResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events/batch" `
        -Method Post `
        -Headers $headers `
        -Body $batchEvents
    
    Write-Host "   ✓ Batch events tracked: $($batchResponse.data.published) of $($batchResponse.data.total)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Batch tracking failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Testing Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To verify Kafka messages, run:" -ForegroundColor Yellow
Write-Host "docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning" -ForegroundColor Gray
