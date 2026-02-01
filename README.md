# SideSpins

**SideSpins** is a comprehensive pool league management system built for APA (American Poolplayers Association) teams. The project provides team management, lineup planning, and match tracking capabilities through a modern web application with RESTful API backend.

## Project Overview

SideSpins serves as the digital hub for "Break of Dawn" 9-ball team, providing tools for:

- **Session/Season Management**: Organize matches into seasons with start/end dates and active status
- **Match Scheduling & Tracking**: View upcoming matches and track results with session assignments
- **Lineup Planning**: Interactive lineup explorer with skill cap validation (23-point APA limit)
- **Match Outcome Recording**: Captains can record actual game results (Win/Loss/Forfeit) for players after matches
- **Player Management**: Roster management with skill levels and availability tracking
- **Team Administration**: Membership management and team statistics

## Architecture

This is a full-stack application with three main components:

### 🌐 Frontend (`docs/`)

**Technology**: Jekyll static site generator with Ruby  
**Deployment**: GitHub Pages with automated CI/CD

- **Public website** hosted at `sidespins.com`
- **Key Features**:
  - Interactive Lineup Explorer for experimenting with player combinations
  - Schedule display with match details and lineup planning links
  - Player availability system
  - Mobile-responsive design
- **Dynamic Features**: JavaScript applications that consume the Azure Functions API

### ⚡ Backend (`functions/`)

**Technology**: .NET 8 Azure Functions (HTTP triggered)  
**Deployment**: Azure Functions with Application Insights integration

- **Secure REST API** with JWT authentication middleware and Stytch integration
- **Authentication**: Two-stage flow (Stytch session JWT → App JWT → API access)
- **Authorization**: Role-based access control with hierarchy (player < captain/manager < admin)
  - **Admin**: Full system access including session management (create/edit/delete)
  - **Captain/Manager**: Team-scoped permissions for roster and match management
  - **Player**: Read access and personal availability updates
- **CRUD Operations** for:
  - Players management
  - Team memberships
  - Match data and lineup planning
  - Session/season management with automatic match assignment
- **Data Models**: Division, Team, Player, TeamMembership, TeamMatch, and Session entities
- **CORS Configuration**: Enables cross-origin requests from the Jekyll site
- **Error Handling**: Comprehensive HTTP status code responses

### 🗄️ Database (`db/`)

**Technology**: Azure Cosmos DB (SQL API) with Python tooling

- **Multi-container database** with partition strategies:
  - **Players** (partition: `/id`)
  - **TeamMemberships** (partition: `/teamId`)
  - **TeamMatches** (partition: `/divisionId`)
  - **Teams** (partition: `/divisionId`)
  - **Divisions** (partition: `/id`)
  - **Sessions** (partition: `/divisionId`)
- **Seed Data**: JSON-based initial data structure for development and testing
- **Import Script**: Python utility for database setup and data migration

## Development Setup

### Prerequisites

- **Jekyll Site**: Ruby 3.1+, Bundler
- **Azure Functions**: .NET 8 SDK, Azure Functions Core Tools
- **Database**: Python 3.x, Azure Cosmos DB account

### Jekyll Site Development

```bash
cd docs
bundle install
bundle exec jekyll serve
# Site available at http://localhost:4000
```

### Azure Functions Development

```bash
cd functions
dotnet build
func start
# API available at http://localhost:7071
```

### Database Setup

```bash
cd db
pip install -r requirements.txt
# Configure environment variables: COSMOS_URI, COSMOS_KEY, COSMOS_DB
python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db
```

## Key Features

### 🎯 Lineup Explorer

Interactive tool for experimenting with player combinations:

- Real-time skill cap validation (23-point APA limit)
- Drag & drop interface for lineup building
- Player filtering and sorting options
- Integration with match scheduling

### 📅 Schedule Management

- Session/season organization with start and end dates
- Active session filtering and display
- Week-by-week match display with session assignments
- Player availability tracking
- Direct links to lineup planning tools
- Match status and results tracking
- Automatic match assignment to newest active session
- **Admin-only Session Management**: Create, edit, and delete sessions via Admin Dashboard
- **Team Active Session**: Captains can select which active session their team participates in
- **Match Outcome Recording** (Captain-only):
  - Record Win/Loss/Forfeit for any team member
  - Available for matches on or after scheduled date
  - Edit/overwrite previously recorded scores
  - Results display alongside planned lineups
  - Players don't need to be in planned lineup to record results

### 👥 Team Administration

- Player roster management with skill levels
- Team membership tracking
- Captain-level administrative functions (roster, active session selection)
- APA number integration
- **Admin Dashboard** (`/admin.html`): Global session management for administrators

### 🎥 Observations (Pool Practice & Match Recording)

Video observation tool for capturing and reviewing pool play sessions:

- **Observation Management**:
  - Create observations for practice or match sessions
  - Track start/end times with automatic duration calculation
  - Active/completed status workflow
  
- **Multi-Part Video Support**:
  - Attach multiple sequential video recordings to single observation
  - Automatic filename parsing extracts UTC timestamps and duration from metadata
  - DateTime-based timeline alignment for precise note correlation
  - Seamless auto-transition between video parts during playback
  
- **Timestamped Notes**:
  - Add notes during or after observation
  - Click note timestamps to jump to specific moments in video
  - DateTime-based timestamps enable precise cross-video seeking
  - General notes (not tied to specific time) also supported
  
- **Video Playback**:
  - HTML5 video player with full seeking/scrubbing controls
  - Multi-part video selector for easy navigation
  - Note-based bookmarking for quick review of key moments
  - Azure CDN delivery for optimal streaming performance
  
- **Azure Integration**:
  - Video storage in Azure Blob Storage with CDN
  - Blob picker UI for easy video attachment
  - Bulk selection and processing of multiple video files
  - Cosmos DB storage for observation metadata and notes

**Technical Notes**:
- Use `video-processing.ps1` script to process, rename, and upload videos automatically
- New filename format: `YYYYMMDD_HHmmss_D{duration}_{seq}_{name}.mp4` (e.g., `20260129_004011_D211_001_MVI_0066.MP4`)
- Metadata (creation_time, duration) extracted using ffprobe for precise timeline correlation
- Videos processed with ffmpeg's `faststart` flag for browser seeking
- See `Tools/PoolIngest/README.md` for video processing details
- See `_docs/OBSERVATIONS_SETUP.md` for detailed setup and usage instructions

## Deployment

### Frontend Deployment

- **Automatic**: GitHub Actions workflow deploys Jekyll site to GitHub Pages on every push to `main` branch
- **Custom Domain**: Configured with CNAME for `sidespins.com`

### Backend Deployment

- **Manual**: Azure Functions deployed to Azure cloud
- **Configuration**: Environment variables for Cosmos DB connection and API secrets
- **Monitoring**: Application Insights integration for logging and performance tracking

### Database Management

- **Production**: Azure Cosmos DB with global distribution
- **Development**: Local Cosmos DB emulator or Azure development account
- **Migration**: Python scripts for data import and schema updates

## API Documentation

The Azure Functions API provides secure endpoints for all CRUD operations. See `functions/README.md` for detailed API documentation including:

- Authentication requirements
- Request/response formats
- Error handling
- Example cURL commands

## Security

- **User Authentication**: Stytch integration for SMS/Email-based user login and signup
- **API Security**: JWT middleware validates all requests with role-based authorization
- **Authorization Levels**: Hierarchical access control (player < captain/manager < admin)
  - Session CRUD operations restricted to admin role
  - Team roster and match management available to captains
  - Admin role set via Stytch `trusted_metadata.sidespins_role`
- **Team Scoping**: Users can only access data for their assigned teams (admins have global access)
- **CORS Policy**: Configured to allow requests only from authorized domains
- **Environment Variables**: Sensitive configuration stored securely
- **Testing**: Dedicated test scripts for authentication flows (`test-auth-middleware.ps1`, `test-signup-flow.ps1`)
