# Match Management API Documentation

## Overview

The Match Management API provides comprehensive functionality for tracking individual player matches and games within team matches, enabling detailed scoring and statistics collection for APA pool leagues.

## Base URLs

- **Local Development**: `http://localhost:7071/api`
- **Production**: `https://sidespins.azurewebsites.net/api`

## Authentication

All endpoints require authentication. Administrative operations support dual authentication:
- **JWT Tokens**: For user-specific operations (`Authorization: Bearer {token}`)
- **API Secret**: For administrative tools (`x-api-secret: {secret}`)

## Core Data Models

### TeamMatch (Enhanced)
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "divisionId": "division-123",
  "homeTeamId": "team-456",
  "awayTeamId": "team-789",
  "week": 5,
  "scheduledAt": "2025-01-15T19:00:00Z",
  "status": "in_progress",
  "totals": {
    "homePoints": 45,
    "awayPoints": 38,
    "homeGamesWon": 9,
    "awayGamesWon": 7
  },
  "lineupPlan": { /* existing lineup structure */ }
}
```

### PlayerMatch
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "teamMatchId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "divisionId": "division-123",
  "order": 1,
  "homePlayerId": "player-123",
  "awayPlayerId": "player-456",
  "homePlayerSkill": 5,
  "awayPlayerSkill": 6,
  "status": "completed",
  "gamesWonHome": 3,
  "gamesWonAway": 2,
  "pointsHome": 15,
  "pointsAway": 12,
  "createdAt": "2025-01-15T19:30:00Z",
  "completedAt": "2025-01-15T20:45:00Z"
}
```

### Game
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "playerMatchId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "divisionId": "division-123",
  "rackNumber": 1,
  "winner": "home",
  "pointsHome": 3,
  "pointsAway": 0,
  "recordedAt": "2025-01-15T19:35:00Z"
}
```

## API Endpoints

### Team Match Detail

#### Get Team Match with Enhanced Details
```http
GET /api/team-matches/{teamMatchId}?divisionId={divisionId}
Authorization: Bearer {jwt_token}
```

**Parameters:**
- `teamMatchId` (path): Unique identifier for the team match
- `divisionId` (query): Division ID for partition key routing

**Response Example:**
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "divisionId": "division-123",
  "homeTeamId": "team-456",
  "awayTeamId": "team-789",
  "totals": {
    "homePoints": 45,
    "awayPoints": 38,
    "homeGamesWon": 9,
    "awayGamesWon": 7
  }
}
```

### Player Match Management

#### Create Player Match
```http
POST /api/team-matches/{teamMatchId}/player-matches
Authorization: Bearer {jwt_token} OR x-api-secret: {secret}
Content-Type: application/json
```

**Request Body:**
```json
{
  "divisionId": "division-123",
  "homePlayerId": "player-123",
  "awayPlayerId": "player-456",
  "order": 1,
  "homePlayerSkill": 5,
  "awayPlayerSkill": 6
}
```

**Response:** `201 Created` with created PlayerMatch object

#### Get Player Match
```http
GET /api/player-matches/{playerMatchId}?divisionId={divisionId}
Authorization: Bearer {jwt_token}
```

**Response:** PlayerMatch object with populated player names

#### Update Player Match
```http
PUT /api/player-matches/{playerMatchId}
Authorization: Bearer {jwt_token} OR x-api-secret: {secret}
Content-Type: application/json
```

**Request Body:**
```json
{
  "divisionId": "division-123",
  "gamesWonHome": 3,
  "gamesWonAway": 2,
  "pointsHome": 15,
  "pointsAway": 12,
  "status": "completed"
}
```

#### Get Player Matches by Team Match
```http
GET /api/team-matches/{teamMatchId}/player-matches?divisionId={divisionId}
Authorization: Bearer {jwt_token}
```

**Response:** Array of PlayerMatch objects ordered by `order` field

### Game Recording

#### Record Game Result
```http
POST /api/player-matches/{playerMatchId}/games
Authorization: Bearer {jwt_token} OR x-api-secret: {secret}
Content-Type: application/json
```

**Request Body:**
```json
{
  "divisionId": "division-123",
  "rackNumber": 1,
  "winner": "home",
  "pointsHome": 3,
  "pointsAway": 0
}
```

**Response:** `201 Created` with created Game object

#### Get Games by Player Match
```http
GET /api/player-matches/{playerMatchId}/games?divisionId={divisionId}
Authorization: Bearer {jwt_token}
```

**Response:** Array of Game objects ordered by `rackNumber`

## Error Responses

All endpoints return consistent error responses:

```json
{
  "error": "Error description",
  "details": "Additional context if available"
}
```

**HTTP Status Codes:**
- `200` - Success
- `201` - Created
- `400` - Bad Request (validation errors)
- `401` - Unauthorized
- `403` - Forbidden
- `404` - Not Found
- `500` - Internal Server Error

## Business Logic

### Score Aggregation
1. Games contribute points to PlayerMatch totals
2. PlayerMatch totals aggregate to TeamMatch totals
3. Updates happen automatically when games are recorded
4. Points-priority scoring: points take precedence over games won for match outcomes

### Status Management
- `scheduled` - Match created but not started
- `in_progress` - Games being recorded
- `completed` - All games finished
- `cancelled` - Match cancelled

### Validation Rules
- Players must be from correct teams (home/away)
- Rack numbers must be sequential
- Points must be non-negative
- Winner must be either "home" or "away"

## Rate Limiting

- **Authenticated Users**: 1000 requests per hour
- **API Secret**: 5000 requests per hour
- **Public Endpoints**: 100 requests per hour per IP

## Example Workflows

### Complete Match Recording
1. Create PlayerMatch for each player pairing
2. Record Games as racks are completed
3. Scores automatically aggregate to PlayerMatch and TeamMatch
4. Monitor via GetTeamMatchDetail endpoint

### Score Dispute Resolution
1. Use GET endpoints to review recorded data
2. Update PlayerMatch totals if needed
3. Re-record individual Games if necessary
4. All changes automatically propagate upward