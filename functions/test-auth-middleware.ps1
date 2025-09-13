# Test script for new JWT authentication middleware
$BaseUrl = "http://localhost:7071/api"

Write-Host "Testing SideSpins JWT Authentication Middleware" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

# First, let's test endpoints without authentication
Write-Host "`n1. Testing unauthenticated endpoint (GetTeams)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/GetTeams?divisionId=test-division" -Method Get
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    Write-Host "Status: Success (200) - Public endpoint working" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Try authenticated endpoint without token (should fail)
Write-Host "`n2. Testing authenticated endpoint without token (should return 401)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/GetMemberships?teamId=test-team" -Method Get
    Write-Host "Unexpected success: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Expected 401 Unauthorized - Middleware working correctly!" -ForegroundColor Green
    } else {
        Write-Host "Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 3: Try authenticated endpoint with invalid token (should fail)
Write-Host "`n3. Testing authenticated endpoint with invalid token (should return 401)" -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer invalid-jwt-token"
    "Content-Type" = "application/json"
}
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/GetMemberships?teamId=test-team" -Method Get -Headers $headers
    Write-Host "Unexpected success: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Expected 401 Unauthorized - JWT validation working!" -ForegroundColor Green
    } else {
        Write-Host "Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n✅ Authentication middleware tests completed!" -ForegroundColor Green
Write-Host "📝 To test with valid JWTs, you'll need to:" -ForegroundColor Yellow
Write-Host "   1. Authenticate via your Stytch flow" -ForegroundColor Yellow
Write-Host "   2. Get an app JWT token" -ForegroundColor Yellow
Write-Host "   3. Use that token in the Authorization header" -ForegroundColor Yellow
