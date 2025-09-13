# AuthUserId Linking - Debugging Breakpoint Guide

## Key Breakpoint Locations for Testing AuthUserId Linking

### **1. In AuthFunctions.cs - VerifySmsCode method**
**File**: `c:\Users\JesseFitzGibbon\source\repos\sidespins\functions\auth\AuthFunctions.cs`
**Line**: Around 250-252

**Breakpoint at:**
```csharp
// IMPORTANT: Link authUserId to player during first successful authentication
if (!string.IsNullOrEmpty(result.Claims?.Sub) && !string.IsNullOrEmpty(result.PhoneNumber))
{
    await LinkAuthUserIdToPlayerAsync(result.Claims.Sub, result.PhoneNumber); // <- BREAKPOINT HERE
}
```

**What to inspect:**
- `result.Claims.Sub` (should contain Stytch user ID)
- `result.PhoneNumber` (should contain the verified phone number)

### **2. In AuthFunctions.cs - LinkAuthUserIdToPlayerAsync method entry**
**File**: `c:\Users\JesseFitzGibbon\source\repos\sidespins\functions\auth\AuthFunctions.cs`
**Line**: Around 540

**Breakpoint at:**
```csharp
private async Task LinkAuthUserIdToPlayerAsync(string authUserId, string phoneNumber)
{
    try
    {
        _logger.LogInformation("Attempting to link authUserId {AuthUserId} with phone {Phone}", authUserId, phoneNumber); // <- BREAKPOINT HERE
```

**What to inspect:**
- `authUserId` parameter (Stytch user ID)
- `phoneNumber` parameter (phone number from verification)

### **3. In AuthFunctions.cs - Player lookup results**
**File**: `c:\Users\JesseFitzGibbon\source\repos\sidespins\functions\auth\AuthFunctions.cs`
**Line**: Around 547

**Breakpoint at:**
```csharp
// Get players by phone number using the PlayerService
var players = await _playerService.GetPlayersByPhoneNumberAsync(phoneNumber);
_logger.LogInformation("Found {Count} players with phone number {Phone}", players.Count, phoneNumber); // <- BREAKPOINT HERE
```

**What to inspect:**
- `players` collection (should contain Brian's player record for +12408582605)
- `players.Count` (should be 1 for Brian)
- `players[0].AuthUserId` (should be null initially)

### **4. In AuthFunctions.cs - Before updating player**
**File**: `c:\Users\JesseFitzGibbon\source\repos\sidespins\functions\auth\AuthFunctions.cs`
**Line**: Around 580-585

**Breakpoint at:**
```csharp
// Safe to link - update the player with authUserId
player.AuthUserId = authUserId; // <- BREAKPOINT HERE
await _playerService.UpdatePlayerAsync(player);
```

**What to inspect:**
- `player.Id` (should be "p_brian")
- `player.PhoneNumber` (should match verification phone)
- `authUserId` being assigned
- `player.AuthUserId` before and after assignment

### **5. In CosmosPlayerService.cs - Database update**
**File**: `c:\Users\JesseFitzGibbon\source\repos\sidespins\functions\auth\CosmosPlayerService.cs`
**Line**: Around 100-105

**Breakpoint at:**
```csharp
public async Task<Player> UpdatePlayerAsync(Player player, CancellationToken cancellationToken = default)
{
    try
    {
        var container = _cosmosClient.GetContainer(_databaseName, _containerName);
        
        _logger.LogInformation("Updating player {PlayerId} with authUserId {AuthUserId}", player.Id, player.AuthUserId); // <- BREAKPOINT HERE
        
        var response = await container.UpsertItemAsync(player, new PartitionKey(player.Id), cancellationToken: cancellationToken);
```

**What to inspect:**
- `player.Id` (should be "p_brian")
- `player.AuthUserId` (should contain Stytch user ID)
- `response.StatusCode` after upsert (should be 200 or 201)

## **Testing Procedure**

### **Step 1: Set Breakpoints**
1. Open VS Code
2. Set breakpoints at all the locations above
3. Start debugging with F5

### **Step 2: Test with Brian's Account**
1. Use the signup flow with Brian's phone number: **+12408582605**
2. Use Brian's APA number: **21229808**
3. Complete SMS verification
4. Watch the breakpoints trigger in sequence

### **Step 3: Verify Results**
After successful linking, check the Cosmos DB document:

**Before linking:**
```json
{
  "id": "p_brian",
  "type": "player",
  "firstName": "Brian",
  "lastName": "D",
  "apaNumber": "21229808",
  "phoneNumber": "+12408582605",
  "createdAt": "2025-09-01T13:48:14.2611649Z",
  "authUserId": null  // <- Should be null initially
}
```

**After linking:**
```json
{
  "id": "p_brian",
  "type": "player",
  "firstName": "Brian",
  "lastName": "D",
  "apaNumber": "21229808",
  "phoneNumber": "+12408582605",
  "createdAt": "2025-09-01T13:48:14.2611649Z",
  "authUserId": "user-test-12345..." // <- Should contain Stytch user ID
}
```

### **Step 4: Test Membership Loading**
After linking, test the membership endpoint:
```
GET /api/me/memberships
Authorization: Bearer [JWT_TOKEN]
```

This should now work correctly and return Brian's memberships.

## **Expected Debugging Flow**

1. **VerifySmsCode** receives verification request
2. **AuthService.VerifySmsCodeAsync** validates code with Stytch
3. **AuthService.ValidateStytchSessionAsync** extracts phone number from Stytch user
4. **LinkAuthUserIdToPlayerAsync** gets called with authUserId and phoneNumber
5. **PlayerService.GetPlayersByPhoneNumberAsync** finds Brian's record
6. **PlayerService.UpdatePlayerAsync** saves authUserId to Cosmos DB
7. Future **MembershipFunctions.GetMyMemberships** calls use authUserId lookup

## **Common Issues to Watch For**

- **Empty phone number**: Check if `result.PhoneNumber` is populated
- **Multiple players**: Check if phone number returns more than one player
- **Database errors**: Watch for Cosmos DB connection or permission issues
- **Phone number format**: Ensure phone number format matches exactly

## **Log Messages to Monitor**

Look for these log messages in the Azure Functions output:
- `"Attempting to link authUserId {AuthUserId} with phone {Phone}"`
- `"Found {Count} players with phone number {Phone}"`
- `"Successfully linked authUserId {AuthUserId} to player {PlayerId}"`
- `"Updating player {PlayerId} with authUserId {AuthUserId}"`
