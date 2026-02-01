# Teams and Schedule Import Tools

These tools import division teams, players, and match schedules from the APA GraphQL API into the SideSpins Cosmos DB database.

## Features

- Fetches team roster data from APA's external GraphQL API
- Transforms data to SideSpins database format
- Deduplicates players by APA member number
- Assigns first roster player as team captain
- Supports both 8-ball and 9-ball divisions
- Dry-run mode with `--what-if` flag
- Idempotent upserts for safe re-runs

## Installation

```bash
cd Tools/TeamsIngest
pip install -r requirements.txt
```

## Usage

### Basic Import

```bash
python import_division.py \
  --division-id 418320 \
  --refresh-token "eyJhbGc..." \
  --division-name "Nottingham Wednesday 8-Ball" \
  --cosmos-uri "https://your-account.documents.azure.com:443/" \
  --cosmos-key "your-cosmos-key" \
  --cosmos-db "sidespins"
```

### Dry-Run (Preview Changes)

Add the `--what-if` flag to preview changes without committing to the database:

```bash
python import_division.py \
  --division-id 418320 \
  --refresh-token "eyJhbGc..." \
  --division-name "Nottingham Wednesday 8-Ball" \
  --cosmos-uri "https://your-account.documents.azure.com:443/" \
  --cosmos-key "your-cosmos-key" \
  --cosmos-db "sidespins" \
  --what-if
```

## Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `--division-id` | Yes | Division ID from APA system (e.g., 418320) |
| `--refresh-token` | Yes | API refresh token for authentication |
| `--division-name` | Yes | Human-readable name (e.g., "Nottingham Wednesday 8-Ball") |
| `--cosmos-uri` | Yes | Cosmos DB endpoint URI |
| `--cosmos-key` | Yes | Cosmos DB access key |
| `--cosmos-db` | Yes | Cosmos DB database name |
| `--what-if` | No | Preview changes without committing |

## How to Get API Tokens

### Refresh Token

1. Log into https://league.poolplayers.com in your browser
2. Open Developer Tools (F12) → Network tab
3. Navigate to any page that makes API calls
4. Look for requests to `gql.poolplayers.com/graphql`
5. Find the `GenerateAccessTokenMutation` request
6. Copy the `refreshToken` value from the request payload

The refresh token looks like: `eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIs...` (JWT format)

### Division ID

1. Navigate to a division page on https://league.poolplayers.com
2. The division ID is in the URL or can be found in API requests
3. Example: `https://league.poolplayers.com/division/418320` → ID is `418320`

## Data Transformations

The script performs the following transformations:

### ID Generation

- **Division**: `div_{division_id}` (e.g., `div_418320`)
- **Team**: `team_{slugified_name}_{team_number}` (e.g., `team_nottinghams_03301`)
- **Player**: `p_{apa_member_number}` (e.g., `p_21273226`)
- **Membership**: `m_{team_id}_{player_id}`

### Name Splitting

API `displayName` ("Michael Hayes") → Database:
- `firstName`: "Michael"
- `lastName`: "Hayes"

### Skill Levels

The script detects 8-ball vs 9-ball from the API's `division.type` field:
- `"EIGHT"` → populates `skillLevel_8b` in TeamMembership
- `"NINE"` → populates `skillLevel_9b` in TeamMembership

### Captain Assignment

The **first player** in each team's roster is assigned as captain (`captainPlayerId` in Team model).

## Player Deduplication

Players are identified by their APA member number:

- **New player**: Creates new Player record with ID `p_{apaNumber}`
- **Existing player**: Reuses existing Player record, skips creation
- **Name mismatch**: Logs warning if existing player's name differs from API data (does not update)

## Output

The script provides detailed output:

```
Fetching access token...
✓ Access token obtained
Fetching division 418320 rosters...
✓ Division data received: 8 teams

Connecting to Cosmos DB: sidespins...
✓ Connected to Cosmos DB

============================================================
DIVISION
============================================================
✓ Division upserted: div_418320

============================================================
TEAMS & PLAYERS
============================================================

--- Team: Nottingham's (03301) ---
✓ Team upserted: team_nottinghams_03301
  Players (8):
  ✓ Michael Hayes (APA#21273226, SL4) - Created [CAPTAIN]
  ○ John Smith (APA#12345678, SL5) - Exists
  ...

============================================================
IMPORT SUMMARY
============================================================
Divisions:   1 created/updated
Teams:       8 created/updated
Players:     15 created, 23 skipped (existing)
Memberships: 38 created/updated

✓ Import completed successfully
```

## Idempotent Operations

The script uses Cosmos DB's `upsert_item()` which safely handles:

- **New records**: Creates them
- **Existing records**: Updates them (based on `id` and partition key)
- **Re-runs**: Safe to execute multiple times without duplicates

## Error Handling

The script will fail gracefully if:

- API authentication fails
- Division ID not found
- Cosmos DB connection fails
- Network errors occur

Re-run the script after fixing issues - upsert operations ensure no duplicates.

## Examples

### Import Multiple Divisions

```bash
# Division 1
python import_division.py --division-id 418320 --refresh-token "..." --division-name "Nottingham Wed 8-Ball" --cosmos-uri "..." --cosmos-key "..." --cosmos-db "sidespins"

# Division 2
python import_division.py --division-id 418321 --refresh-token "..." --division-name "Nottingham Thu 9-Ball" --cosmos-uri "..." --cosmos-key "..." --cosmos-db "sidespins"
```

### Preview Before Import

```bash
# First, preview changes
python import_division.py --division-id 418320 --refresh-token "..." --division-name "Nottingham Wed 8-Ball" --cosmos-uri "..." --cosmos-key "..." --cosmos-db "sidespins" --what-if

# If everything looks good, run without --what-if
python import_division.py --division-id 418320 --refresh-token "..." --division-name "Nottingham Wed 8-Ball" --cosmos-uri "..." --cosmos-key "..." --cosmos-db "sidespins"
```

## Schedule Import Tool

### Usage

Import match schedules for a division that already has teams imported:

```bash
python import_schedule.py \
  --division-id 418320 \
  --refresh-token "eyJhbGc..." \
  --session-id "session_2025_fall" \
  --cosmos-uri "https://your-account.documents.azure.com:443/" \
  --cosmos-key "your-cosmos-key" \
  --cosmos-db "sidespins"
```

### Dry-Run (Preview)

```bash
python import_schedule.py \
  --division-id 418320 \
  --refresh-token "eyJhbGc..." \
  --session-id "session_2025_fall" \
  --cosmos-uri "https://your-account.documents.azure.com:443/" \
  --cosmos-key "your-cosmos-key" \
  --cosmos-db "sidespins" \
  --what-if
```

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `--division-id` | Yes | Division ID from APA system (e.g., 418320) |
| `--refresh-token` | Yes | API refresh token for authentication |
| `--session-id` | Yes | Session ID to link matches to (e.g., "session_2025_fall") |
| `--cosmos-uri` | Yes | Cosmos DB endpoint URI |
| `--cosmos-key` | Yes | Cosmos DB access key |
| `--cosmos-db` | Yes | Cosmos DB database name |
| `--what-if` | No | Preview changes without committing |

### Important Notes

- **Run AFTER importing teams** - Schedule import requires teams to already exist in the database
- **Create-only mode** - Existing matches are skipped to preserve user-entered lineups and scores
- **Team matching** - Maps API team numbers to database team IDs by extracting team number from team ID suffix
- **Bye matches** - Automatically skipped during import
- **Missing teams** - Matches with teams not in database are skipped with warnings

### Data Transformations

#### Match IDs

Generated as: `match_{session_id}_{week}_{home_team_id}_{away_team_id}`

Example: `match_session_2025_fall_5_team_we_dem_boyz_03306_team_pound_for_pound_03305`

#### Status Mapping

API → Database:
- `"COMPLETED"` → `"completed"`
- `"UNPLAYED"` → `"scheduled"`
- `"SCHEDULED"` → `"scheduled"`

#### Match Records

- **New matches**: Full TeamMatch document with empty `lineupPlan` and `playerMatches`
- **Existing matches**: Skipped entirely to preserve user data
- **Completed matches**: Include `totals.homePoints` and `totals.awayPoints` from API

### Workflow Example

```bash
# Step 1: Import teams and players
python import_division.py \
  --division-id 418320 \
  --refresh-token "..." \
  --division-name "Nottingham Wednesday 8-Ball" \
  --cosmos-uri "..." \
  --cosmos-key "..." \
  --cosmos-db "sidespins"

# Step 2: Import schedule
python import_schedule.py \
  --division-id 418320 \
  --refresh-token "..." \
  --session-id "session_2025_fall" \
  --cosmos-uri "..." \
  --cosmos-key "..." \
  --cosmos-db "sidespins"
```

## Troubleshooting

### "Failed to get access token"

- Your refresh token may have expired (they typically last for a long time but do expire)
- Get a new refresh token from the browser as described above

### "Name mismatch" warnings

This is normal and indicates:
- Player already exists in database with slightly different name
- Could be due to nickname, typo, or name change
- Player record is NOT updated (preserves existing data)
- Membership is still created with correct APA number link

### No teams imported

Check that:
- Division ID is correct
- Division has non-bye teams with rosters
- API returned valid data (check network connectivity)

### "Teams not found in DB" warnings

This occurs when the schedule import cannot find teams in the database:
- Make sure you ran `import_division.py` first to import teams
- Check that team numbers match between API and database
- Verify the division ID is correct

### Matches not created

If matches aren't being created:
- Check that matches don't already exist (script skips existing matches)
- Verify session ID is correct
- Use `--what-if` to preview what would be created
