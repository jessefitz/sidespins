# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SideSpins is a pool league management system for APA teams with a three-tier architecture:
- **Frontend**: Jekyll static site (`docs/`) deployed to GitHub Pages at sidespins.com
- **Backend**: .NET 8 Azure Functions (`functions/`) providing REST API
- **Database**: Azure Cosmos DB with Python tooling (`db/`)
- **Tools**: Video ingest (`Tools/PoolIngest/` - PowerShell) and team/schedule import (`Tools/TeamsIngest/` - Python)

## Build & Run Commands

```bash
# Backend (.NET Azure Functions)
cd functions
dotnet build              # Build
func start                # Run locally at http://localhost:7071

# Frontend (Jekyll)
cd docs
bundle install            # First time only
bundle exec jekyll serve  # Serves at http://localhost:4000

# Database setup
cd db
pip install -r requirements.txt
python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db

# Video processing (PowerShell)
cd Tools/PoolIngest
.\video-processing.ps1 -DriveLetter D
```

## Testing

API test scripts live in `functions/`:
- `test-api.ps1` / `test-api.sh` — general API testing (local API secret: `"banana"`)
- `test-auth-middleware.ps1` — JWT authentication testing
- `test-authuserid-implementation.ps1` — user ID claim testing
- `test-signup-flow.ps1` — Stytch signup flow testing

No unit test framework is configured; testing is done via these scripts against a running local instance.

## Architecture & Key Patterns

### Authentication (Two-Stage)
1. User authenticates via **Stytch** (SMS/email) → gets Stytch session JWT
2. Session JWT exchanged for app JWT via `/api/auth/generate-jwt`
3. App JWT sent as `Authorization: Bearer <token>` on all API calls
4. `AuthenticationMiddleware.cs` validates JWT on every request using `IFunctionsWorkerMiddleware`
5. Endpoints use `[RequireAuthentication("role")]` attribute with `AuthorizationLevel.Anonymous` — middleware handles all auth
6. Role hierarchy: player < captain/manager < admin

### Backend Structure (`functions/`)
- `Program.cs` — DI container, middleware, CORS, global JSON settings
- `auth/` — Authentication middleware, Stytch integration, JWT generation, player/membership services
- `league/` — Core business logic: `LeagueService.cs` (singleton), `LeagueModels.cs`, domain function files
- `observations/` — Video observations: CRUD, timestamped notes, Azure Blob Storage URLs
- `admin/` — Migration utilities, admin player management
- Naming: `{Domain}Functions.cs` (e.g., `PlayersFunctions.cs`), endpoints at `/api/{PascalCaseName}`

### Data Models & Cosmos DB
- Models in `functions/league/LeagueModels.cs` use **Newtonsoft.Json** `[JsonProperty]` for camelCase serialization
- Global JSON config in `Program.cs`: `CamelCasePropertyNamesContractResolver`, UTC timezone handling
- Multi-container with partition keys: Players(`/id`), TeamMemberships(`/teamId`), TeamMatches(`/divisionId`), Teams(`/divisionId`), Divisions(`/id`), Sessions(`/divisionId`), Observations(`/id`), Notes(`/observationId`)
- **Always create Cosmos containers via Azure Portal or Azure CLI** — VS Code extension may set incorrect partition keys
- 2 MB item limit per document

### Frontend Authentication (Critical)
- Use `/assets/auth.js` — do NOT use `/auth/auth-manager.js` (doesn't exist)
- `const authManager = new AuthManager();` — no parameters, no `initialize()` method
- Always use `authManager.baseUrl` for API endpoints, never create a separate baseUrl variable
- API endpoint names are PascalCase: `/GetMatches`, `/GetPlayers`, `/CreatePlayer`
- Fetch ALL players with `/GetPlayers` then combine with memberships — never fetch individual players in a loop
- Skill levels come from `skillLevel_9b` on TeamMembership, not the Player model

### Adding New API Endpoints
1. Add model to `LeagueModels.cs` with `[JsonProperty]` attributes
2. Create function in appropriate `{Domain}Functions.cs`
3. Use `[Function("Name")]` and `[RequireAuthentication("role")]` attributes
4. Set `AuthorizationLevel.Anonymous` on `HttpTrigger`
5. Include `FunctionContext context` parameter
6. Add business logic in `LeagueService.cs`

### Video Processing Pipeline
- `Tools/PoolIngest/video-processing.ps1` handles: FFmpeg conversion (with `faststart`), FFprobe metadata extraction, smart renaming (`YYYYMMDD_HHmmss_D{duration}_{seq}_{name}.mp4`), Azure Blob upload via azcopy
- Config in `Tools/PoolIngest/config.json`

## Environment Variables (Backend)
- `COSMOS_URI`, `COSMOS_KEY`, `COSMOS_DB` — database connection
- `STYTCH_PROJECT_ID`, `STYTCH_SECRET`, `STYTCH_API_URL` — auth provider
- `JWT_SIGNING_KEY` — app JWT signing
- `API_SHARED_SECRET` — legacy/test API auth
- `ENABLE_CODE_CORS`, `ALLOWED_ORIGINS` — CORS configuration
- `BLOB_STORAGE_ACCOUNT_NAME`, `BLOB_CONTAINER_NAME`, `BLOB_STORAGE_CONNECTION_STRING` — video storage
