# API Contracts – Match Management (Integrated Enhancements)

Base URL: `/api`
Authentication: Transitional dual mode. Read endpoints: API secret OR JWT (via `AllowApiSecret`). Mutation endpoints: Captain JWT required; legacy secret/admin path allowed only when `ALLOW_SECRET_MUTATIONS` true (logged & deprecated). Future tightening will disable secret writes.
Content-Type: `application/json`

## Entity Summary

- TeamMatch: team-level match record and aggregate scores.
- PlayerMatch: individual player vs player result summary.
- Game: individual rack result within a PlayerMatch.

## Conventions

- ULID identifiers in path parameters (string).
- 404 if resource not found in its partition.
- 400 for validation errors (missing required fields, type mismatch).
- 409 reserved for future concurrency (not used MVP).
- Response bodies use camelCase.

---
 
## TeamMatch Endpoints

### Create / Enhance TeamMatch

POST `/api/team-matches`
Request:

```json
{
  "divisionId": "string",
  "homeTeamId": "string",
  "awayTeamId": "string",
  "matchDate": "2025-01-12T20:00:00Z"  // maps to persisted scheduledAt
}
```
Response 201:

```json
{
  "id": "01HF...",
 
  "divisionId": "DIV123",
  "teamId": "<homeTeamId>",
  "homeTeamId": "...",
  "awayTeamId": "...",
  "matchDate": "2025-01-12T20:00:00Z",
  "status": "completed",
  "playerMatchIds": [],
  "teamScoreHome": 0,
  "teamScoreAway": 0,
  "createdUtc": "2025-01-12T21:00:00Z",
  "updatedUtc": "2025-01-12T21:00:00Z"
}
```


### List Recent Team Matches (by Team within Division)

GET `/api/divisions/{divisionId}/teams/{teamId}/team-matches?limit=25&continuationToken=...`
Response 200:

```json
{
  "items": [ { "id": "01HF...", "matchDate": "2025-01-12T20:00:00Z", "homeTeamId": "...", "awayTeamId": "...", "teamScoreHome": 9, "teamScoreAway": 6 } ],
  "continuationToken": "string-or-null"
}
```

### Get TeamMatch Detail

GET `/api/team-matches/{teamMatchId}` (response includes `matchDate` alias; legacy stored field is `scheduledAt`)
Response 200: TeamMatch document (without expanded children).

### Delete TeamMatch (MVP Optional – implement if needed)

DELETE `/api/team-matches/{teamMatchId}` → 204
Note: Hard delete; no soft-delete semantics MVP.

---
 
## PlayerMatch Endpoints

### Add PlayerMatch to TeamMatch (Nested)

POST `/api/team-matches/{teamMatchId}/player-matches`
Request:

```json
{
  "divisionId": "string",   
  "homePlayerId": "string",
  "awayPlayerId": "string",
  "order": 1,
  "homePlayerSkill": 4,
  "awayPlayerSkill": 3
}
```
Response 201:

```json
{
  "id": "01HG...",
  "teamMatchId": "...",
  "order": 1,
  "homePlayerId": "...",
  "awayPlayerId": "...",
  "gamesWonHome": 0,
  "gamesWonAway": 0,
  "createdUtc": "...",
  "updatedUtc": "..."
}
```


### Get PlayerMatch

GET `/api/player-matches/{playerMatchId}` → 200

### Update PlayerMatch Scores (Direct Patch – optional; usually driven by adding Game records)

PATCH `/api/player-matches/{playerMatchId}`
Request (any subset):

```json
{ "gamesWonHome": 3, "gamesWonAway": 2 }
```

Response 200: updated PlayerMatch.

### Delete PlayerMatch

DELETE `/api/player-matches/{playerMatchId}` → 204
Cascade Consideration: MVP may allow only if no games exist (else 400).

---
 
## Game Endpoints

### Record Game (Point-Based)

POST `/api/player-matches/{playerMatchId}/games`
Request:

```json
{ "rackNumber": 3, "pointsHome": 2, "pointsAway": 1, "divisionId": "DIV123", "winner": "home" }
```
Response 201:

```json
{ "id": "01HH...", "playerMatchId": "...", "rackNumber": 3, "pointsHome": 2, "pointsAway": 1, "winner": "home", "createdUtc": "..." }
```

Notes:

- `winner` optional; no inference performed MVP (left null if absent).
- Legacy `gamesWonHome/gamesWonAway` updated only if winner supplied AND all points zero (fallback case).
- If any Game in a PlayerMatch has points > 0, team aggregation uses points (fallback to gamesWon otherwise – feature flag removable).


### List Games for PlayerMatch

GET `/api/player-matches/{playerMatchId}/games`
Response 200:

```json
{ "items": [ { "id": "01HH...", "rackNumber": 3, "pointsHome": 2, "pointsAway": 1, "winner": "home" } ] }
```



---
 
## Aggregated Retrieval (Future – Not MVP)

- GET `/api/team-matches/{teamMatchId}/full` → returns TeamMatch + embedded PlayerMatches + Games.

---
 
## Error Response Shape

All errors:

```json
{ "error": { "code": "string", "message": "human readable" } }
```

Codes (MVP): `validation_failed`, `not_found`, `conflict` (reserved), `internal_error`.

---
 
## Open Considerations (Documented, Not Blocking MVP)

- Bulk creation endpoint for player matches (skip).
- Idempotency keys for duplicate POST suppression (future).
- ETag concurrency headers (future).
- Potential route alternative: `POST /api/divisions/{divisionId}/team-matches` to derive partition from path (trade-off: duplication vs explicit body field). MVP keeps body field for flexibility with future migration tooling.

## Ready Checklist

- [x] CRUD endpoints enumerated
- [x] Request/response skeletons
- [x] Error shape standardized
- [x] Future expansion noted

Status: READY (integration version) for quickstart + plan.md consolidation.
