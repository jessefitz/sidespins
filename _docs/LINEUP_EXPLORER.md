# Lineup Explorer - What-If Sandbox

The Lineup Explorer is a public, anonymous tool that allows visitors to experiment with different player combinations for team matches and see how they stack up against the APA 23-point skill cap rule.

## Features

- **Match Selection**: Choose from available matches to explore lineups
- **Interactive Lineup Builder**: Drag & drop or click to add players to 5 lineup slots
- **Live Skill Cap Tracking**: Real-time display of total skill level vs. 23-point cap
- **Player Filtering & Sorting**: Filter by skill level, sort by name or skill
- **Captain Lineup Option**: Start from existing captain lineups when available
- **Mobile Responsive**: Works on desktop and mobile devices

## Access

- **Direct Link**: `/lineup-explorer.html`
- **From Schedule**: Each match on the schedule page has a "🎯 Lineup Explorer" button
- **From Home Page**: Quick link in the navigation menu

## API Endpoints

The tool uses two new public API endpoints:

### GET /public/matches?divisionId={divisionId}
Returns simplified match data for public consumption:
```json
[
  {
    "id": "match_id",
    "date": "2025-09-15",
    "scheduledAt": "2025-09-15T19:00:00Z",
    "week": 3,
    "divisionId": "div_id",
    "homeTeamId": "home_team_id",
    "awayTeamId": "away_team_id",
    "status": "scheduled",
    "hasLineup": true,
    "lineupPlan": { ... }
  }
]
```

### GET /public/matches/{matchId}/roster?divisionId={divisionId}
Returns available players for a specific match:
```json
[
  {
    "playerId": "player_id",
    "name": "Player Name",
    "skill": 4
  }
]
```

## Usage Flow

1. **Select Match**: Choose a match from the dropdown
2. **Choose Starting Point**: Start empty or from captain lineup (if available)
3. **Build Lineup**: 
   - Drag players from bench to slots
   - Or click "+" on slots to select players
   - Or click "Add" on player cards
4. **Monitor Cap**: Watch the sticky footer for skill total and cap status
5. **Experiment**: Try different combinations, clear and start over

## Technical Details

- **Frontend**: Vanilla JavaScript with modern CSS
- **Backend**: Azure Functions with Cosmos DB
- **Authentication**: Anonymous access (read-only)
- **Data**: Live data from your team management system

## Design Principles

- **No Authentication Required**: Completely public access
- **Read-Only**: No lineup changes are saved
- **Real-Time Feedback**: Instant skill cap validation
- **Mobile First**: Touch-friendly interactions
- **Accessible**: Keyboard navigation and screen reader friendly

## Integration

The Lineup Explorer integrates seamlessly with your existing team site:

- Schedule page includes direct links to explore each match
- Home page navigation includes access link
- Uses existing team data and player rosters
- Maintains consistent visual design with your site theme
