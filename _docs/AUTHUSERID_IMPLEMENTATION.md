# AuthUserId Implementation Documentation

## Overview

This document describes the implementation for linking authentication user IDs (`authUserId`) to player documents in Cosmos DB. This solves the issue where users couldn't access their memberships because the system couldn't map authentication tokens to player records.

## Changes Made

### 1. Player Model Updated

Added `authUserId` property to the `Player` model in `LeagueModels.cs`:

```csharp
[JsonProperty("authUserId")]
public string? AuthUserId { get; set; }
```

### 2. Player Service Interface & Implementation

Created `IPlayerService` and `CosmosPlayerService` to handle player lookups:

- `GetPlayerByAuthUserIdAsync(string authUserId)` - Find player by auth user ID
- `GetPlayerByIdAsync(string playerId)` - Find player by player ID

### 3. Updated Membership Functions

Modified `MembershipFunctions.cs` to use the new flow:

1. Get `authUserId` from JWT token
2. Look up player using `IPlayerService.GetPlayerByAuthUserIdAsync()`
3. Use player ID to query memberships

### 4. Automatic Linking During Registration

Enhanced `AuthFunctions.cs` to automatically link `authUserId` during SMS verification:

- Added `LinkAuthUserIdToPlayerAsync()` method
- Calls this method after successful SMS verification
- Uses safe linking logic (only links if exactly one candidate player)

### 5. Admin Management Functions

Created `PlayerManagementFunctions.cs` with admin endpoints:

- `POST /admin/players/{playerId}/link-auth` - Manually link player to auth user
- `DELETE /admin/players/{playerId}/link-auth` - Unlink player from auth user  
- `GET /admin/players/without-auth` - List players without auth user links

### 6. Debugging Support

Enhanced `GetMyProfile` endpoint with debug information to help troubleshoot linking issues.

## Implementation Flow

### Registration Flow
1. User calls `/auth/signup/init` with APA number and phone
2. System validates APA number and updates player's phone number
3. SMS verification code is sent
4. User calls `/auth/sms/verify` with verification code
5. **NEW**: System automatically links `authUserId` from Stytch to player document
6. User can now access `/me/memberships` successfully

### Membership Access Flow
1. User accesses `/me/memberships` with JWT token
2. System extracts `authUserId` from JWT token
3. **NEW**: System looks up player using `authUserId`
4. System queries memberships using player ID
5. Returns user's team memberships

## Database Migration

### Updating Existing Player Documents

You need to add `authUserId` to existing player documents. Options:

#### Option 1: Manual Update (Recommended for testing)
Update documents directly in Azure Data Explorer:

```json
{
  "id": "p_brian",
  "type": "player",
  "firstName": "Brian",
  "lastName": "D",
  "apaNumber": "21229808",
  "phoneNumber": "+12408582605",
  "authUserId": "user-test-16c9c3b3-...",
  "createdAt": "2025-09-01T13:48:14.2611649Z"
}
```

#### Option 2: Use Migration Script
Run the PowerShell script: `scripts/migrate-players-auth-userid.ps1`

#### Option 3: Use Admin Endpoints
1. Get list of players without auth: `GET /admin/players/without-auth`
2. Link players manually: `POST /admin/players/{playerId}/link-auth`

## Testing Instructions

### 1. Test Automatic Linking (New Users)
1. Create a new player document without `authUserId`
2. Have them go through the signup flow
3. Verify `authUserId` is set after SMS verification
4. Verify `/me/memberships` works

### 2. Test Manual Linking (Existing Users)
1. Find the user's `authUserId` from their JWT token or logs
2. Use admin endpoint to link: 
   ```bash
   POST /admin/players/p_brian/link-auth
   Content-Type: application/json
   Authorization: Bearer <admin-jwt>
   
   {
     "authUserId": "user-test-16c9c3b3-..."
   }
   ```
3. Verify `/me/memberships` works for that user

### 3. Test Debug Information
1. Call `/me/profile` to see debug information
2. Check logs for linking attempts and results

## Troubleshooting

### Issue: Memberships not loading
1. Check `/me/profile` endpoint for debug info
2. Verify `authUserId` is set in player document
3. Check logs for player lookup attempts

### Issue: Automatic linking not working
1. Check if multiple players exist without `authUserId`
2. Review logs for linking attempts
3. Use manual linking as fallback

### Issue: Wrong player linked
1. Use admin endpoint to unlink: `DELETE /admin/players/{playerId}/link-auth`
2. Link to correct player: `POST /admin/players/{correctPlayerId}/link-auth`

## API Endpoints Added/Modified

### Authentication Endpoints (Modified)
- `POST /auth/sms/verify` - Now includes automatic player linking

### Membership Endpoints (Modified)  
- `GET /me/memberships` - Now uses authUserId lookup
- `GET /me/profile` - Enhanced with debug information

### Admin Endpoints (New)
- `POST /admin/players/{playerId}/link-auth` - Link player to auth user
- `DELETE /admin/players/{playerId}/link-auth` - Unlink player from auth user
- `GET /admin/players/without-auth` - List unlinked players

## Security Considerations

- Admin endpoints require admin authentication
- `authUserId` uniqueness is enforced (one auth user = one player)
- Automatic linking only works when exactly one candidate exists
- Failed linking attempts are logged for manual review

## Next Steps

1. Deploy the updated code
2. Test with one user first (manual linking)
3. Update remaining player documents
4. Monitor logs for linking issues
5. Consider adding phone number validation to improve automatic linking
