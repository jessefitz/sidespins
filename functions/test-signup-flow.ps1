# Test script for SideSpins Signup and SMS Verification Flow
$BaseUrl = "http://localhost:7071/api"

Write-Host "Testing SideSpins Signup and SMS Verification Flow" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Test 1: Test Signup Init (this should now include PhoneId in response)
Write-Host "`n1. Testing POST /api/auth/signup/init" -ForegroundColor Yellow
$signupRequest = @{
    apaNumber = "12345"
    phoneNumber = "+1234567890"  # Test phone number
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/auth/signup/init" -Method Post -Body $signupRequest -ContentType "application/json"
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    
    if ($response.phoneId) {
        Write-Host "✅ SUCCESS: PhoneId found in response: $($response.phoneId)" -ForegroundColor Green
        $phoneId = $response.phoneId
    } else {
        Write-Host "❌ FAILED: PhoneId missing from response" -ForegroundColor Red
    }
    
    if ($response.success) {
        Write-Host "✅ SUCCESS: Signup init successful" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Signup init failed: $($response.message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
        try {
            $errorContent = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorContent)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error Body: $errorBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read error response body" -ForegroundColor Red
        }
    }
}

# Test 2: Test Direct SMS Send (for comparison)
Write-Host "`n2. Testing POST /api/auth/sms/send (for comparison)" -ForegroundColor Yellow
$smsRequest = @{
    phoneNumber = "+1234567890"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/auth/sms/send" -Method Post -Body $smsRequest -ContentType "application/json"
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
    
    if ($response.phoneId) {
        Write-Host "✅ SUCCESS: PhoneId found in SMS send response: $($response.phoneId)" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: PhoneId missing from SMS send response" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n📋 Summary:" -ForegroundColor Yellow
Write-Host "- Both signup/init and sms/send endpoints should return a phoneId" -ForegroundColor Yellow
Write-Host "- The phoneId is required for SMS verification to work" -ForegroundColor Yellow
Write-Host "- Frontend should store this phoneId and use it for verification" -ForegroundColor Yellow

Write-Host "`n✅ Test completed!" -ForegroundColor Green
