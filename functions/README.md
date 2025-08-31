# SideSpins API - Azure Functions

This Azure Functions app provides CRUD operations for the SideSpins pool league management system.

## Security

All endpoints require the `x-api-secret` header with a valid secret key.

## Configuration

Set these environment variables:

- `API_SHARED_SECRET`: Secret key for API authentication
- `COSMOS_URI`: Cosmos DB endpoint URI
- `COSMOS_KEY`: Cosmos DB access key
- `COSMOS_DB`: Database name (default: "sidespins")

## Database Structure

This API works with a multi-container Cosmos DB setup:
- **Players** container (partition key: `/id`)
- **TeamMemberships** container (partition key: `/teamId`)
- **TeamMatches** container (partition key: `/divisionId`)
- **Teams** container (partition key: `/divisionId`)
- **Divisions** container (partition key: `/id`)

## API Endpoints

### Players

**GET /api/players** - Get all players
```bash
curl -X GET "http://localhost:7071/api/players" \
  -H "x-api-secret: your-secret-key-here"
```

**POST /api/players** - Create a new player
```bash
curl -X POST "http://localhost:7071/api/players" \
  -H "Content-Type: application/json" \
  -H "x-api-secret: your-secret-key-here" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "apaNumber": "12345"
  }'
```

**PATCH /api/players/{id}** - Update a player
```bash
curl -X PATCH "http://localhost:7071/api/players/p_12345" \
  -H "Content-Type: application/json" \
  -H "x-api-secret: your-secret-key-here" \
  -d '{
    "firstName": "John",
    "lastName": "Smith",
    "apaNumber": "12345"
  }'
```

**DELETE /api/players/{id}** - Delete a player
```bash
curl -X DELETE "http://localhost:7071/api/players/p_12345" \
  -H "x-api-secret: your-secret-key-here"
```

### Team Memberships

**GET /api/memberships?teamId={teamId}** - Get memberships for a team
```bash
curl -X GET "http://localhost:7071/api/memberships?teamId=team_break_of_dawn_9b" \
  -H "x-api-secret: your-secret-key-here"
```

**POST /api/memberships** - Create a new membership
```bash
curl -X POST "http://localhost:7071/api/memberships" \
  -H "Content-Type: application/json" \
  -H "x-api-secret: your-secret-key-here" \
  -d '{
    "teamId": "team_break_of_dawn_9b",
    "divisionId": "div_nottingham_wed_9b_311",
    "playerId": "p_john_doe",
    "role": "player",
    "skillLevel_9b": 5
  }'
```

**DELETE /api/memberships/{membershipId}?teamId={teamId}** - Delete a membership
```bash
curl -X DELETE "http://localhost:7071/api/memberships/m_team_break_of_dawn_9b_p_john_doe?teamId=team_break_of_dawn_9b" \
  -H "x-api-secret: your-secret-key-here"
```

### Matches

**GET /api/matches?divisionId={divisionId}** - Get matches for a division
```bash
curl -X GET "http://localhost:7071/api/matches?divisionId=div_nottingham_wed_9b_311" \
  -H "x-api-secret: your-secret-key-here"
```

**PATCH /api/matches/{matchId}/lineup?divisionId={divisionId}** - Update match lineup
```bash
curl -X PATCH "http://localhost:7071/api/matches/tm_div_nottingham_wed_9b_311_20250806_001/lineup?divisionId=div_nottingham_wed_9b_311" \
  -H "Content-Type: application/json" \
  -H "x-api-secret: your-secret-key-here" \
  -d '{
    "ruleset": "APA_9B",
    "maxTeamSkillCap": 23,
    "home": [
      {
        "playerId": "p_ava",
        "skillLevel": 5,
        "intendedOrder": 1,
        "isAlternate": false,
        "notes": "Prefers to open"
      }
    ],
    "away": [],
    "totals": {
      "homePlannedSkillSum": 5,
      "awayPlannedSkillSum": 0,
      "homeWithinCap": true,
      "awayWithinCap": true
    },
    "locked": false,
    "lockedBy": null,
    "lockedAt": null,
    "history": []
  }'
```

## Running Locally

1. Make sure you have the Azure Functions Core Tools installed
2. Configure your local.settings.json with the required values
3. Run `func start` or use the VS Code debugger

## CORS Configuration

For production deployment, configure CORS on the Function App to:
- Allow your Jekyll domain(s) as origins
- Allow methods: GET, POST, PATCH, DELETE
- Allow headers: content-type, x-api-secret

## Error Responses

- `401 Unauthorized`: Missing or invalid x-api-secret header
- `400 Bad Request`: Invalid request data or missing required parameters
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

All successful responses return JSON data with appropriate HTTP status codes.
