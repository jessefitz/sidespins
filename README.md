# SideSpins

**SideSpins** is a comprehensive pool league management system built for APA (American Poolplayers Association) teams. The project provides team management, lineup planning, and match tracking capabilities through a modern web application with RESTful API backend.

## Project Overview

SideSpins serves as the digital hub for "Break of Dawn" 9-ball team, providing tools for:

- **Match Scheduling & Tracking**: View upcoming matches and track results
- **Lineup Planning**: Interactive lineup explorer with skill cap validation (23-point APA limit)
- **Player Management**: Roster management with skill levels and availability tracking
- **Team Administration**: Membership management and team statistics

## Architecture

This is a full-stack application with three main components:

### 🌐 Frontend (`site/`)

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

- **Secure REST API** with secret key authentication (`x-api-secret` header)
- **CRUD Operations** for:
  - Players management
  - Team memberships
  - Match data and lineup planning
- **Data Models**: Division, Team, Player, TeamMembership, and TeamMatch entities
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
- **Seed Data**: JSON-based initial data structure for development and testing
- **Import Script**: Python utility for database setup and data migration

## Development Setup

### Prerequisites

- **Jekyll Site**: Ruby 3.1+, Bundler
- **Azure Functions**: .NET 8 SDK, Azure Functions Core Tools
- **Database**: Python 3.x, Azure Cosmos DB account

### Jekyll Site Development

```bash
cd site
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

- Week-by-week match display
- Player availability tracking
- Direct links to lineup planning tools
- Match status and results tracking

### 👥 Team Administration

- Player roster management with skill levels
- Team membership tracking
- Captain-level administrative functions
- APA number integration

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

- **API Security**: Secret key authentication for all backend operations
- **CORS Policy**: Configured to allow requests only from authorized domains
- **Environment Variables**: Sensitive configuration stored securely
