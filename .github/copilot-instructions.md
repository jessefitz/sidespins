# SideSpins - AI Coding Agent Instructions

## Project Overview

SideSpins is a pool league management system for APA teams with a **three-tier architecture**:
- **Frontend**: Jekyll static site (`docs/`) deployed to GitHub Pages at `sidespins.com`
- **Backend**: .NET 8 Azure Functions (`functions/`) providing secure REST API
- **Database**: Azure Cosmos DB with Python tooling (`db/`)

## Architecture Patterns

### Authentication Flow
- **JWT middleware** (`functions/auth/AuthenticationMiddleware.cs`) validates all API requests
- Use `[RequiresApiSecret]` or `[RequiresUserAuth]` attributes on Function endpoints
- Two-stage auth: API secret for anonymous calls, JWT tokens for user-specific operations
- Test auth flows with `test-auth-middleware.ps1` and `test-authuserid-implementation.ps1`

### Data Models & Cosmos DB
- **Multi-container strategy** with partition keys: Players (`/id`), TeamMemberships (`/teamId`), etc.
- Models in `functions/league/LeagueModels.cs` use Newtonsoft.Json with camelCase serialization
- Seed data: `db/seed_sidespins.json` for development, import with `db/import_cosmos_sidespins.py`

### API Design Patterns
- **CRUD operations** organized by domain: `PlayersFunctions.cs`, `TeamsFunctions.cs`, etc.
- **Consistent response format**: HTTP status codes, JSON camelCase, error handling
- **CORS configuration**: Enabled for Jekyll site via `ENABLE_CODE_CORS` environment variable

## Development Workflow

### Local Development Setup
```bash
# Backend (Azure Functions)
cd functions
dotnet build                     # Use VS Code task "build (functions)"
func start                       # Use VS Code task "func: 4" (background)

# Frontend (Jekyll)
cd docs
bundle exec jekyll serve         # Serves at http://localhost:4000

# Database
cd db
python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db
```

### Testing Patterns
- **API testing**: Use `functions/test-api.ps1` (PowerShell) or `test-api.sh` (bash)
- **Authentication testing**: Dedicated scripts for auth flows
- **Local environment**: Functions run on `:7071`, Jekyll on `:4000`

## Key Files & Conventions

### Configuration
- `functions/local.settings.json`: Local Azure Functions config (API secrets, Cosmos connection)
- `docs/_config.yml`: Jekyll site configuration
- `functions/host.json`: Azure Functions runtime settings

### Critical Components
- `functions/Program.cs`: DI container, middleware registration, CORS setup
- `functions/auth/AuthenticationMiddleware.cs`: JWT validation and role-based auth
- `functions/league/LeagueService.cs`: Business logic layer for CRUD operations
- `docs/lineup-explorer-new.html`: Core interactive lineup planning feature

### Naming Conventions
- **Azure Functions**: `{Domain}Functions.cs` (e.g., `PlayersFunctions.cs`)
- **Models**: Singular nouns with `JsonProperty` attributes for camelCase
- **API endpoints**: `/api/{plural-resource}` (e.g., `/api/players`)

## Integration Points

### Frontend ↔ Backend
- **CORS-enabled fetch calls** from Jekyll JavaScript to Azure Functions
- **API secret header**: `x-api-secret` required for all API calls
- **Lineup validation**: 23-point APA skill cap enforced in both frontend UI and backend

### External Dependencies
- **Stytch**: Authentication provider for user login/signup flows
- **Application Insights**: Telemetry and logging for Azure Functions
- **GitHub Pages**: Automated Jekyll deployment on push to `main`

## Common Tasks

### Adding New API Endpoints
1. Add model to `LeagueModels.cs` with proper `JsonProperty` attributes
2. Create function in appropriate `{Domain}Functions.cs` file
3. Add authentication attribute (`[RequiresApiSecret]` or `[RequiresUserAuth]`)
4. Update `LeagueService.cs` for business logic
5. Test with PowerShell/bash scripts

### Frontend Development
- **API calls**: Use `fetch()` with `x-api-secret` header from Jekyll JavaScript
- **Responsive design**: Bootstrap-based styling in `_sass/` directory
- **Dynamic features**: Client-side JavaScript consumes Azure Functions API

### Database Changes
- Update models in `LeagueModels.cs` and corresponding containers
- Modify `db/seed_sidespins.json` for test data
- Use `db/import_cosmos_sidespins.py` for schema migrations