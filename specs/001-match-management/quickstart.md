# Quickstart – Match Management (Integrated MVP)

This guide helps you exercise the enhanced match management API endpoints (extending existing `MatchesFunctions` / `LeagueService`) locally with the Azure Functions backend and Jekyll frontend.

Feature Directory: `specs/001-match-management` (renamed from `specs/001-captain-match-management`).

Note: Existing TeamMatch `lineupPlan` (skill cap planning, availability, alternates) is preserved unchanged; new PlayerMatch/Game persistence is additive.

## Prerequisites

- .NET 8 SDK installed
- Azure Functions Core Tools installed
- Python (for optional Cosmos seed script) + requirements in `db/requirements.txt`
- Cosmos DB local emulator OR Azure Cosmos DB connection string configured in `functions/local.settings.json`
- API secret present in `local.settings.json` (value: `ApiSecret`)

## 1. Seed / Prepare Data (Optional)

If starting fresh with player/team data:

```bash
cd db
python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db
```

## 2. Start Backend (Azure Functions)

```bash
cd functions
func start
```

Hosted at: <http://localhost:7071>

## 3. Verify Authentication Header

All calls require header:

```text
x-api-secret: <value from local.settings.json>
Content-Type: application/json
```

### 3.1 Prefer JWT (Forward-Looking)

While the API secret works for local development, production-bound flows will require a JWT issued by Stytch. When JWT auth is fully enabled for these endpoints, requests will resemble:

```bash
curl -X POST http://localhost:7071/api/team-matches \
  -H "Authorization: Bearer $JWT" \
  -H "Content-Type: application/json" \
  -d '{"divisionId":"DIV123","homeTeamId":"TEAM_A","awayTeamId":"TEAM_B","matchDate":"2025-01-12T20:00:00Z"}'
```

Transitional Phase Guidance:

Continue using `x-api-secret` only for local/manual testing until flags `ALLOW_SECRET_MATCH_READS` / `ALLOW_SECRET_MATCH_WRITES` are tightened.

Begin integrating login in the frontend and store the JWT (short-lived) rather than baking secrets into JS.

Mutation endpoints will be the first to require JWT only.

## 4. Create a Team Match (matchDate aliases persisted scheduledAt)

```bash
curl -X POST http://localhost:7071/api/team-matches \
  -H "x-api-secret: $API_SECRET" \
  -H "Content-Type: application/json" \
  -d '{"divisionId":"DIV123","homeTeamId":"TEAM_A","awayTeamId":"TEAM_B","matchDate":"2025-01-12T20:00:00Z"}'
```

Response contains `id` – store as TEAM_MATCH_ID.

## 5. Add Player Match

```bash
curl -X POST http://localhost:7071/api/team-matches/$TEAM_MATCH_ID/player-matches \
  -H "x-api-secret: $API_SECRET" \
  -H "Content-Type: application/json" \
  -d '{"divisionId":"DIV123","homePlayerId":"PLAYER_A1","awayPlayerId":"PLAYER_B1","order":1}'
```

Store returned `id` as PLAYER_MATCH_ID.

## 6. Record Game Results (Point-Based)

 
```bash
curl -X POST http://localhost:7071/api/player-matches/$PLAYER_MATCH_ID/games \
  -H "x-api-secret: $API_SECRET" \
  -H "Content-Type: application/json" \
  -d '{"rackNumber":1,"pointsHome":2,"pointsAway":1,"divisionId":"DIV123","winner":"home"}'
```

Repeat with incrementing `rackNumber`. You may omit `winner` if points do not imply a clear winner for a rack format. PlayerMatch `pointsHome/pointsAway` and TeamMatch aggregate scores will recompute after each insert.

## 7. List Recent Matches for a Team


```bash
curl -H "x-api-secret: $API_SECRET" http://localhost:7071/api/divisions/DIV123/teams/TEAM_A/team-matches?limit=10
```
Returns summary list with scores.

## 8. Retrieve Match Detail

 
```bash
curl -H "x-api-secret: $API_SECRET" http://localhost:7071/api/team-matches/$TEAM_MATCH_ID
```
(For full expansion you would fetch player matches and games separately in MVP.)

## 9. Frontend Integration (Read-Only MVP)

Add JS in the Jekyll site (e.g., `docs/app.html` or a new page) to call the list endpoint and render matches (uses new score fields):

 
```js
async function loadRecentMatches(divisionId, teamId) {
  const res = await fetch(`http://localhost:7071/api/divisions/${divisionId}/teams/${teamId}/team-matches?limit=5`, {
    headers: { 'x-api-secret': '<API_SECRET>' }
  });
  const data = await res.json();
  console.log('Matches', data.items);
}
```

### 9.1 Viewing in Browser (Minimal Outcomes Viewer)

If you create the optional minimal pages described in the spec/plan:

| Page | Suggested Path | Query Params | Purpose |
|------|----------------|--------------|---------|
| Past Matches | `/matches.html` | `divisionId`, `teamId` | Lists recent matches with scores & derived outcome |
| Match Detail | `/match.html` | `id`, `divisionId` | Shows PlayerMatches and loads Games on expand |

Implementation Hints:

- Inject the API secret in all fetch calls via a small helper (`fetchJson`).
- Derive outcome label: `home > away => Win`, `< => Loss`, `=== => Tie`.
- Lazy-load games only when a player match row is expanded to reduce initial calls.
- Provide an empty state message when no matches returned.

Expected JSON Field Mapping:

| UI Field | Source Field |
|----------|--------------|
| Match Date | `matchDate` |
| Opponent Team | `awayTeamId` (temporary until team lookup) |
| Team Score | `teamScoreHome` / `teamScoreAway` |
| Player Home Points | `pointsHome` (PlayerMatch) |
| Player Away Points | `pointsAway` (PlayerMatch) |
| Game Points | `pointsHome` / `pointsAway` (Game) |
| Game Winner | `winner` |

Accessibility Checklist (Minimum):

- Table headers use `<th scope="col">`.
- Expand buttons toggle `aria-expanded` + controlled region with `role="region"`.
- Loading spinners / text placed in a container with `aria-live="polite"`.

## 10. Cleanup

- Delete test matches if a DELETE endpoint is implemented (optional)

```bash
curl -X DELETE -H "x-api-secret: $API_SECRET" http://localhost:7071/api/team-matches/$TEAM_MATCH_ID
```

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| 401 Unauthorized | Missing or incorrect `x-api-secret` | Confirm value in `local.settings.json` |
| 404 on child insert | Wrong TEAM_MATCH_ID or partition mismatch | Ensure team ID consistency with parent teamId |
| Scores not updating | Batch logic not yet implemented | Implement transactional batch or manual recompute service |

## Feature Flags (Transitional & Rationalization Roadmap)

Legacy (current) flags:

| Flag | Purpose | Default (local) |
|------|---------|-----------------|
| ALLOW_SECRET_MUTATIONS | Permit API secret write paths (legacy/testing) | true |
| DISABLE_API_SECRET_MUTATIONS | Force-disable all secret writes | false |
| DISABLE_GAMESWON_FALLBACK | Prevent fallback to gamesWon* aggregates | false |

Planned refined flags (supersede legacy – see `constitutional-future-considerations.md`):

| Upcoming Flag | Purpose | Notes |
|---------------|---------|-------|
| ALLOW_SECRET_MATCH_READS | Allow secret-based read-only access | Will default to true locally, false in prod later |
| ALLOW_SECRET_MATCH_WRITES | Allow secret-based writes | Will default to false once JWT flows stabilize |

Deprecation Path:
 
 1. Introduce new flags alongside legacy (mapping legacy to new behavior with warnings).
 2. Update integration tests for matrix of read/write perms.
 3. Remove legacy flags once no production environment references them.

Configure in `functions/local.settings.json` under `Values`.

## Next Steps (Post-MVP / Future)

- Aggregate endpoint to return full match with nested player matches and games.
- Add captain role-based authorization (JWT claims) instead of API secret.
- Implement validation rules (skill cap, lineup order uniqueness).

Ready: This quickstart exercises all MVP endpoints (integrated path).
