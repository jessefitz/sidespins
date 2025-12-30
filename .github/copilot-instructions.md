# SideSpins - AI Coding Agent Instructions

## Project Overview

SideSpins is a pool league management system for APA teams with a **three-tier architecture**:
- **Frontend**: Jekyll static site (`docs/`) deployed to GitHub Pages at `sidespins.com`
- **Backend**: .NET 8 Azure Functions (`functions/`) providing secure REST API
- **Database**: Azure Cosmos DB with Python tooling (`db/`)

## Architecture Patterns

### Authentication Flow
- **Stytch is the primary end-user authentication mechanism** - all user login/signup flows use Stytch
- **JWT middleware** (`functions/auth/AuthenticationMiddleware.cs`) validates all API requests using `IFunctionsWorkerMiddleware`
- Use `[RequireAuthentication("role")]` attribute on Function endpoints (e.g., "player", "manager", "admin")
- **Two-stage auth flow for users**: Stytch session JWT → App JWT generation (`/api/auth/generate-jwt`) → API access with app JWT in `Authorization: Bearer <token>` header
- All Function endpoints use `AuthorizationLevel.Anonymous` - middleware enforces security
- Test auth flows with `test-auth-middleware.ps1`, `test-authuserid-implementation.ps1`, and `test-signup-flow.ps1`

### Data Models & Cosmos DB
- **Multi-container strategy** with optimized partition keys:
  - Players (`/id` - self-partitioned for even distribution)
  - TeamMemberships (`/teamId` - queries by team)
  - TeamMatches (`/divisionId` - queries by division)
  - Teams (`/divisionId` - queries by division)
  - Divisions (`/id` - self-partitioned)
- Models in `functions/league/LeagueModels.cs` use **Newtonsoft.Json** with `[JsonProperty]` attributes for camelCase serialization
- **Global JSON settings** configured in `Program.cs` with `CamelCasePropertyNamesContractResolver`
- **2 MB item limit** per Cosmos DB document - avoid embedding large collections
- Seed data: `db/seed_sidespins.json` for development, import with `db/import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db`
- Container specs defined in Python import script (`CONTAINER_SPECS` dict)

### API Design Patterns
- **CRUD operations** organized by domain: `PlayersFunctions.cs`, `TeamsFunctions.cs`, `MembershipsFunctions.cs`, `MatchesFunctions.cs`
- **Consistent response format**: HTTP status codes (200, 201, 400, 401, 404, 500), JSON camelCase, error handling
- **CORS configuration**: Enabled for Jekyll site via `ENABLE_CODE_CORS` environment variable with `ALLOWED_ORIGINS` list
- **Dependency Injection**: `LeagueService` registered as singleton, `CosmosClient` configured in `Program.cs`
- **Function pattern**: All functions use `[Function("Name")]` attribute, `HttpRequest req`, and `FunctionContext context` parameters
- **Error handling**: Try-catch with appropriate HTTP status codes and error messages in response body

## Development Workflow

### Local Development Setup
```bash
# Backend (Azure Functions)
cd functions
dotnet build                     # Use VS Code task "build (functions)"
func start                       # Use VS Code task "func: 4" (background)
# API runs at http://localhost:7071

# Frontend (Jekyll)
cd docs
bundle install                   # First time only
bundle exec jekyll serve         # Serves at http://localhost:4000

# Database Setup
cd db
pip install -r requirements.txt  # First time only
# Set environment variables: COSMOS_URI, COSMOS_KEY, COSMOS_DB
python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db
```

### Testing Patterns
- **API testing**: Use `functions/test-api.ps1` (PowerShell) or `test-api.sh` (bash) with `$ApiSecret = "banana"` for local dev
- **Authentication testing**: `test-auth-middleware.ps1`, `test-authuserid-implementation.ps1`, `test-signup-flow.ps1`
- **Local environment**: Functions on `:7071`, Jekyll on `:4000`, test scripts include both success and unauthorized access cases
- **VS Code Tasks**: Use predefined tasks for build (`build (functions)`) and run (`func: 4` as background task)

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
- **User authentication**: Frontend uses Stytch for login/signup, then includes JWT in `Authorization: Bearer <token>` header for authenticated API calls
- **Legacy/testing**: `x-api-secret` header supported for test scripts and legacy flows (use JWT tokens for production user flows)
- **Lineup validation**: 23-point APA skill cap enforced in both frontend UI and backend

### External Dependencies
- **Stytch**: Authentication provider for user login/signup flows
- **Application Insights**: Telemetry and logging for Azure Functions
- **GitHub Pages**: Automated Jekyll deployment on push to `main`

## Common Tasks

### Adding New API Endpoints
1. Add model to `LeagueModels.cs` with proper `JsonProperty` attributes for camelCase
2. Create function in appropriate `{Domain}Functions.cs` file
3. Use `[Function("Name")]` and `[RequireAuthentication("role")]` attributes
4. Set `AuthorizationLevel.Anonymous` on `HttpTrigger` - middleware handles auth
5. Include `FunctionContext context` parameter for middleware access
6. Update `LeagueService.cs` for business logic
7. Test with PowerShell/bash scripts

### Frontend Development
- **API calls**: Use `fetch()` from Jekyll JavaScript to Azure Functions endpoints
- **User authentication**: Implement Stytch login/signup flows, then call `/api/auth/generate-jwt` to get app JWT
- **Authenticated requests**: Include JWT in `Authorization: Bearer <token>` header for user-specific operations
- **Responsive design**: Bootstrap-based styling in `_sass/` directory
- **Dynamic features**: Client-side JavaScript consumes Azure Functions API
- **Key pages**: `lineup-explorer-new.html` for interactive lineup planning with 23-point skill cap validation

### Database Changes
- Update models in `LeagueModels.cs` with `[JsonProperty]` attributes and corresponding containers
- Modify `db/seed_sidespins.json` for test data
- Use `db/import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db` for schema migrations
- Container specs in `CONTAINER_SPECS` dict define partition keys for each container