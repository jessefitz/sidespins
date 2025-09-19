<!--
Sync Impact Report:
- Version change: Initial → 1.0.0
- Principles added: Mobile-First UX, Three-Tier Architecture, RESTful API Design, Secure Authentication, Testing & Validation
- Templates requiring updates: ✅ Ready for validation phase
- No deferred TODOs
-->

# SideSpins Constitution

## Core Principles

### I. Mobile-First User Experience (NON-NEGOTIABLE)

The frontend interface MUST be designed for mobile users first, with desktop as a progressive enhancement. All user interactions must be simple, intuitive, and accessible to a wide spectrum of users browsing from mobile phones. Navigation patterns must be touch-friendly with clear visual hierarchy and minimal cognitive load.

**Rationale**: The majority of SideSpins users access the application from mobile devices. Complex interfaces create barriers to adoption and daily usage for pool league teams.

### II. Three-Tier Architecture Integrity

System MUST maintain clear separation between frontend (Jekyll static site), backend (Azure Functions API), and database (Cosmos DB). Each tier has distinct responsibilities and communication occurs only through defined interfaces (CORS-enabled REST API with authentication).

**Rationale**: This architecture ensures scalability, maintainability, and allows independent deployment of each tier while maintaining security boundaries.

### III. RESTful API Design Standards

Backend API MUST follow RESTful conventions with consistent response formats, proper HTTP status codes, and camelCase JSON serialization. All endpoints require authentication (JWT tokens) and support CORS for frontend integration.

**Rationale**: Standardized API design ensures predictable behavior, easier testing, and seamless frontend-backend integration while maintaining security requirements.

### IV. Secure Authentication by Default

All API endpoints MUST implement authentication using `[RequiresUserAuth]` attributes. JWT middleware validates user tokens, and sensitive operations require proper authorization levels. No functionality exposed without authentication.

**Rationale**: Pool league data is sensitive team information requiring protection. Security-by-default prevents accidental exposure of data or functionality.

### V. Testing & Validation Standards

All features MUST include automated testing strategies appropriate to their tier: API testing via PowerShell/bash scripts, frontend validation through browser testing, and database operations verified through seed data. Authentication flows require dedicated test scripts.

**Rationale**: APA pool league operations have zero tolerance for errors during live matches. Comprehensive testing prevents disruptions during critical team activities.

## Technology Constraints

All technology choices MUST align with the established stack:

- **Frontend**: Jekyll (Ruby) with responsive CSS and JavaScript for GitHub Pages deployment
- **Backend**: .NET 8 Azure Functions with Application Insights integration
- **Database**: Azure Cosmos DB with Python tooling for migrations
- **Authentication**: Stytch integration with JWT tokens
- **Deployment**: GitHub Actions for CI/CD, Azure for backend hosting

## Development Workflow

### Code Organization Standards

- **Azure Functions**: `{Domain}Functions.cs` pattern (e.g., `PlayersFunctions.cs`)
- **Data Models**: Singular nouns with `JsonProperty` attributes for camelCase API serialization
- **API Endpoints**: `/api/{plural-resource}` convention (e.g., `/api/players`)
- **Documentation**: Maintain API documentation alongside implementation

### Quality Gates

- All authentication changes MUST pass dedicated test script validation
- Frontend changes MUST be tested on mobile viewports before deployment
- API modifications MUST maintain backward compatibility or include migration strategy
- Database schema changes MUST include corresponding seed data updates

## Governance

This constitution supersedes all other development practices and guides all technical decisions. All pull requests and code reviews MUST verify compliance with these principles, particularly mobile-first UX and security requirements.

Amendments require:

1. Documentation of the change rationale
2. Impact assessment on existing templates and guidance
3. Migration plan for affected components
4. Version bump following semantic versioning

For runtime development guidance, refer to `.github/copilot-instructions.md` which provides implementation-specific direction while adhering to constitutional principles.

**Version**: 1.0.0 | **Ratified**: 2025-09-19 | **Last Amended**: 2025-09-19
