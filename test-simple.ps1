# Simple Phase 2 Test
Write-Host "=== FitLife Phase 2 Test ===" -ForegroundColor Cyan

# 1. Health
$health = Invoke-RestMethod -Uri "http://localhost:5269/health"
Write-Host "✓ Health check passed" -ForegroundColor Green

# 2. Register/Login
try {
    $register = Invoke-RestMethod -Uri "http://localhost:5269/api/auth/register" -Method Post -Body (@{
        email = "test@lifetime.com"
        password = "Test123!"
        firstName = "Test"
        lastName = "User"
        fitnessLevel = "Intermediate"
    } | ConvertTo-Json) -ContentType "application/json"
    $token = $register.data.token
    $userId = $register.data.user.id
    Write-Host "✓ User registered: $userId" -ForegroundColor Green
} catch {
    $login = Invoke-RestMethod -Uri "http://localhost:5269/api/auth/login" -Method Post -Body (@{
        email = "test@lifetime.com"
        password = "Test123!"
    } | ConvertTo-Json) -ContentType "application/json"
    $token = $login.data.token
    $userId = $login.data.user.id
    Write-Host "✓ User logged in: $userId" -ForegroundColor Green
}

# 3. Track View event
$headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
$event = Invoke-RestMethod -Uri "http://localhost:5269/api/events" -Method Post -Headers $headers -Body (@{
    userId = $userId
    itemId = "class_001"
    eventType = "View"
    metadata = @{ source = "test" }
} | ConvertTo-Json)
Write-Host "✓ View event tracked" -ForegroundColor Green

# 4. Track Book event
$event2 = Invoke-RestMethod -Uri "http://localhost:5269/api/events" -Method Post -Headers $headers -Body (@{
    userId = $userId
    itemId = "class_001"
    eventType = "Book"
    metadata = @{ bookingId = "booking_123" }
} | ConvertTo-Json)
Write-Host "✓ Book event tracked" -ForegroundColor Green

# 5. Batch events
$batch = Invoke-RestMethod -Uri "http://localhost:5269/api/events/batch" -Method Post -Headers $headers -Body (@(
    @{ userId = $userId; itemId = "class_002"; eventType = "View"; metadata = @{} }
    @{ userId = $userId; itemId = "class_003"; eventType = "Click"; metadata = @{} }
) | ConvertTo-Json)
Write-Host "✓ Batch events: $($batch.data.published) of $($batch.data.total)" -ForegroundColor Green

Write-Host ""
Write-Host "=== All Tests Passed ===" -ForegroundColor Green
Write-Host ""
Write-Host "Verify Kafka messages:" -ForegroundColor Yellow
Write-Host "docker exec -it fitlife-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic user-events --from-beginning" -ForegroundColor Gray
