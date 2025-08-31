# Test script for SideSpins API (PowerShell version)
$BaseUrl = "http://localhost:7071/api"
$ApiSecret = "banana"

Write-Host "Testing SideSpins Azure Functions API" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

# Test 1: Get Players (should return empty array initially)
Write-Host "`n1. Testing GET /api/players" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/players" -Method Get -Headers @{"x-api-secret" = $ApiSecret}
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    Write-Host "Status: Success (200)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# Test 2: Create a new player
Write-Host "`n2. Testing POST /api/players" -ForegroundColor Yellow
$newPlayer = @{
    firstName = "Test"
    lastName = "Player"
    apaNumber = "12345"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/players" -Method Post -Body $newPlayer -ContentType "application/json" -Headers @{"x-api-secret" = $ApiSecret}
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    Write-Host "Status: Success (200)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# Test 3: Test unauthorized access (no header)
Write-Host "`n3. Testing unauthorized access (no x-api-secret header)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/players" -Method Get
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    Write-Host "Status: Unexpected Success" -ForegroundColor Red
} catch {
    Write-Host "Expected error: $($_.Exception.Message)" -ForegroundColor Green
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Green
}

# Test 4: Test unauthorized access (wrong secret)
Write-Host "`n4. Testing unauthorized access (wrong secret)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/players" -Method Get -Headers @{"x-api-secret" = "wrong-secret"}
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    Write-Host "Status: Unexpected Success" -ForegroundColor Red
} catch {
    Write-Host "Expected error: $($_.Exception.Message)" -ForegroundColor Green
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Green
}

Write-Host "`nTest complete!" -ForegroundColor Green
