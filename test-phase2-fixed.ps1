# Phase 2 End-to-End Test - Fixed Version
# Addresses 403 authorization issues with fresh user per test

Write-Host "=== Phase 2 End-to-End Test ===" -ForegroundColor Cyan

# 1. Health Check
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5269/health"
    Write-Host "   ✓ Health check passed" -ForegroundColor Green
} catch {
    Write-Host "   ✗ API not responding. Start with: dotnet run" -ForegroundColor Red
    exit 1
}

# 2. Fresh Registration (avoids conflicts)
Write-Host "`n2. Creating fresh test user..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$testEmail = "test-$timestamp@lifetime.com"

$registerBody = @{
    email = $testEmail
    password = "Test123!"
    firstName = "Test"
    lastName = "User"
    fitnessLevel = "Intermediate"
} | ConvertTo-Json

try {
    $registerResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/auth/register" `
        -Method Post `
        -Body $registerBody `
        -ContentType "application/json"

    $token = $registerResponse.data.token
    $userId = $registerResponse.data.user.id

    Write-Host "   ✓ User created: $userId" -ForegroundColor Green
    Write-Host "   Email: $testEmail" -ForegroundColor Gray

    # Decode JWT to verify userId matches (debug step)
    $tokenParts = $token.Split('.')
    $payloadBase64 = $tokenParts[1]
    # Add padding if needed
    while ($payloadBase64.Length % 4 -ne 0) {
        $payloadBase64 += "="
    }
    $payloadBytes = [System.Convert]::FromBase64String($payloadBase64)
    $payload = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
    $payloadObj = $payload | ConvertFrom-Json
    
    Write-Host "   JWT sub claim: $($payloadObj.sub)" -ForegroundColor Gray
    
    if ($payloadObj.sub -ne $userId) {
        Write-Host "   ⚠ WARNING: JWT sub ($($payloadObj.sub)) != userId ($userId)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ Registration failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 3. Track Single View Event
Write-Host "`n3. Tracking View event..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$viewEvent = @{
    userId = $userId  # This MUST match the JWT sub claim
    itemId = "class_yoga_001"
    eventType = "View"
    metadata = @{
        source = "test-script"
        timestamp = (Get-Date -Format "o")
    }
} | ConvertTo-Json

try {
    $viewResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events" `
        -Method Post `
        -Headers $headers `
        -Body $viewEvent
    
    Write-Host "   ✓ View event tracked" -ForegroundColor Green
    Write-Host "   Response: $($viewResponse.message)" -ForegroundColor Gray
    Write-Host "   Event ID: $($viewResponse.data.eventId)" -ForegroundColor Gray
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "   ✗ FAILED with HTTP $statusCode" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($statusCode -eq 403) {
        Write-Host "   → 403 Forbidden: JWT userId doesn't match event userId" -ForegroundColor Yellow
    }
}

# 4. Track Book Event
Write-Host "`n4. Tracking Book event..." -ForegroundColor Yellow
$bookEvent = @{
    userId = $userId
    itemId = "class_hiit_001"
    eventType = "Book"
    metadata = @{
        bookingId = "booking_$timestamp"
        source = "test-script"
    }
} | ConvertTo-Json

try {
    $bookResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events" `
        -Method Post `
        -Headers $headers `
        -Body $bookEvent
    
    Write-Host "   ✓ Book event tracked" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Book event failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Track Batch Events
Write-Host "`n5. Tracking batch events..." -ForegroundColor Yellow
$batchEvents = @(
    @{
        userId = $userId
        itemId = "class_hiit_002"
        eventType = "View"
        metadata = @{ source = "test-batch" }
    },
    @{
        userId = $userId
        itemId = "class_yoga_002"
        eventType = "Click"
        metadata = @{ source = "test-batch" }
    },
    @{
        userId = $userId
        itemId = "class_yoga_002"
        eventType = "Complete"
        metadata = @{ duration = 45; calories = 350 }
    }
) | ConvertTo-Json

try {
    $batchResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events/batch" `
        -Method Post `
        -Headers $headers `
        -Body $batchEvents
    
    Write-Host "   ✓ Batch published: $($batchResponse.data.published) of $($batchResponse.data.total)" -ForegroundColor Green
    
    if ($batchResponse.data.errors) {
        Write-Host "   ⚠ Errors: $($batchResponse.data.errors -join ', ')" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ Batch failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 6. Test invalid event type
Write-Host "`n6. Testing validation (invalid event type)..." -ForegroundColor Yellow
$invalidEvent = @{
    userId = $userId
    itemId = "class_test"
    eventType = "InvalidType"
    metadata = @{}
} | ConvertTo-Json

try {
    $invalidResponse = Invoke-RestMethod -Uri "http://localhost:5269/api/events" -Method Post -Headers $headers -Body $invalidEvent
    Write-Host "   ✗ Validation should have failed!" -ForegroundColor Red
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-Host "   ✓ Validation working correctly (400 Bad Request)" -ForegroundColor Green
    }
    else {
        Write-Host "   ? Unexpected status: $statusCode" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Kafka Verification Commands:" -ForegroundColor Yellow
Write-Host "  # View all events:" -ForegroundColor Gray
Write-Host "  docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning --property print.key=true --property print.timestamp=true" -ForegroundColor Gray
Write-Host ""
Write-Host "  # Count events:" -ForegroundColor Gray
Write-Host "  docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning --max-messages 100 | wc -l" -ForegroundColor Gray
Write-Host ""
Write-Host "Redis Verification:" -ForegroundColor Yellow
Write-Host "  docker exec -it fitlife-redis redis-cli" -ForegroundColor Gray
Write-Host "  > KEYS *" -ForegroundColor Gray
Write-Host "  > GET rec:$userId" -ForegroundColor Gray
