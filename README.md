# SideSpins

A pool league management system with three main components:

## Project Structure

- `site/` - Jekyll-based static website for the public-facing site
- `functions/` - Azure Functions API for backend services
- `db/` - Cosmos DB setup and configuration scripts

## Development

### Jekyll Site

```bash
cd site
bundle install
bundle exec jekyll serve
```

### Azure Functions

```bash
cd functions
dotnet build
func start
```

### Database Setup

```bash
cd db
# Follow instructions in db/README.md
```

## Deployment

The Jekyll site is automatically deployed to GitHub Pages via GitHub Actions when changes are pushed to the main branch.
