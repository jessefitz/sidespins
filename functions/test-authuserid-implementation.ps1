# Test AuthUserId Implementation
# Run this after deploying the updated functions

$BaseUrl = "http://localhost:7071/api"  # Update with your actual base URL

Write-Host "Testing AuthUserId Implementation" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

# Test 1: Check players without authUserId (admin endpoint)
Write-Host "`n1. Testing GET /admin/players/without-auth" -ForegroundColor Yellow
try {
    # Note: You'll need to add proper admin authentication headers
    $response = Invoke-RestMethod -Uri "$BaseUrl/admin/players/without-auth" -Method Get
    Write-Host "Players without authUserId: $($response.count)" -ForegroundColor Cyan
    foreach ($player in $response.players) {
        Write-Host "  - $($player.id): $($player.firstName) $($player.lastName) (APA: $($player.apaNumber))" -ForegroundColor White
    }
} catch {
    Write-Host "Error (expected if not admin): $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Test signup flow for new user (if you have test data)
Write-Host "`n2. Testing signup flow with authUserId linking" -ForegroundColor Yellow
$signupRequest = @{
    apaNumber = "TEST_APA_NUMBER"  # Replace with actual test APA number
    phoneNumber = "+1234567890"    # Replace with actual test phone
} | ConvertTo-Json

Write-Host "Signup request: $signupRequest" -ForegroundColor White
# Uncomment to test:
# try {
#     $response = Invoke-RestMethod -Uri "$BaseUrl/auth/signup/init" -Method Post -Body $signupRequest -ContentType "application/json"
#     Write-Host "Signup response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
# } catch {
#     Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
# }

# Test 3: Test profile endpoint with debug info
Write-Host "`n3. Testing GET /me/profile (requires authentication)" -ForegroundColor Yellow
# This requires a valid JWT token in Authorization header
Write-Host "Note: This requires authentication. Use your browser or Postman with valid JWT token." -ForegroundColor Yellow

# Test 4: Test manual linking (admin function)  
Write-Host "`n4. Testing manual player linking (admin only)" -ForegroundColor Yellow
$linkRequest = @{
    authUserId = "user-test-example-123"  # Replace with actual authUserId
} | ConvertTo-Json

Write-Host "Link request: $linkRequest" -ForegroundColor White
Write-Host "Endpoint: POST $BaseUrl/admin/players/PLAYER_ID/link-auth" -ForegroundColor White
Write-Host "Note: Replace PLAYER_ID with actual player ID and add admin authentication." -ForegroundColor Yellow

Write-Host "`n5. Testing membership endpoint" -ForegroundColor Yellow
Write-Host "Endpoint: GET $BaseUrl/me/memberships" -ForegroundColor White
Write-Host "Note: This requires user authentication. Should now work after authUserId linking." -ForegroundColor Yellow

Write-Host "`nTest completed. Check logs for linking attempts and results." -ForegroundColor Green
