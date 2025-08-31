#!/bin/bash

# Test script for SideSpins API
BASE_URL="http://localhost:7071/api"
API_SECRET="banana"

echo "Testing SideSpins Azure Functions API"
echo "====================================="

# Test 1: Get Players (should return empty array initially)
echo -e "\n1. Testing GET /api/players"
curl -s -X GET "${BASE_URL}/players" \
  -H "x-api-secret: ${API_SECRET}" \
  -w "\nHTTP Status: %{http_code}\n"

# Test 2: Create a new player
echo -e "\n2. Testing POST /api/players"
curl -s -X POST "${BASE_URL}/players" \
  -H "Content-Type: application/json" \
  -H "x-api-secret: ${API_SECRET}" \
  -d '{
    "firstName": "Test",
    "lastName": "Player",
    "apaNumber": "12345"
  }' \
  -w "\nHTTP Status: %{http_code}\n"

# Test 3: Test unauthorized access (no header)
echo -e "\n3. Testing unauthorized access (no x-api-secret header)"
curl -s -X GET "${BASE_URL}/players" \
  -w "\nHTTP Status: %{http_code}\n"

# Test 4: Test unauthorized access (wrong secret)
echo -e "\n4. Testing unauthorized access (wrong secret)"
curl -s -X GET "${BASE_URL}/players" \
  -H "x-api-secret: wrong-secret" \
  -w "\nHTTP Status: %{http_code}\n"

echo -e "\nTest complete!"
