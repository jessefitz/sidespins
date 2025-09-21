# Feature Specification: Match Management (Captain Enhancements)

**Feature Branch**: `001-match-management`  
**Created**: September 20, 2025  
**Last Updated**: September 21, 2025  
**Status**: Draft (integrated with existing match codepath)  
**Input**: User description: "Team captains should be able to manage matches. Match management is a critical feature core to the SideSpins platform. The APA public website offers captains the ability to see upcoming matches and view outcomes of past matches, but lacks the flexibility for captains and players to capture additional information about matches (e.g. notes and pictures), nor can captains schedule and track the outcomes of other matches outside of official APA play. SideSpin's mission is to fill this gap and other critical gaps so that teams can experience a richer, more informed match management experience."

## Scope Breakdown

### MVP (Frontend User-Visible)

- View list of past team matches (official APA league) with final outcomes and total points.
- View details for a single team match: player matches, per-player total points, overall team total.
- (Optional if capacity) Basic upcoming matches list (read-only) sourced from manually created or externally imported entries.
- Lineup planning remains available through existing lineup explorer (no new UI beyond linking).

### MVP (API Capability)

Enhance existing `MatchesFunctions` + `LeagueService` instead of introducing a separate `CaptainMatchesFunctions`. Additive nested PlayerMatch & Game routes are backward compatible with current `/api/matches` usage.

- Extend existing TeamMatch docs (fields like `scheduledAt`, `totals`) with alias / new fields: `matchDate` (alias of `scheduledAt`), `externalLeagueMatchId`, `teamScoreHome`, `teamScoreAway`.
- Introduce PlayerMatch & Game documents in same Cosmos partition (`/divisionId`).
- CRUD for PlayerMatch & Game implemented inside existing functions class.
- Support practice/makeup match creation even if not yet surfaced in UI.
- Maintain last-write-wins semantics.
- Optional external league match identifier.

### Explicitly Deferred (Future Scope)

- Captain UI for creating practice or makeup matches.
- Advanced filtering (by type, opponent, date range) beyond simple past view.
- Player-level stat analytics rollups.
- Notifications, messaging, or opposing captain workflows.
- Audit/version history of results.
- Automatic APA GraphQL ingest/background sync (separate future feature spec).
- Validation against full APA rule set (skill cap, lineup legality, forfeiture special scoring).
- Scheduling UI, negotiation, or calendar visualization.
- Media attachments (photos, notes).
- Tournament / playoff / challenge match types.

### Guiding Principle

Design the domain model now to support future expansion without schema-breaking changes, while keeping the initial UI lean and read-focused.

## Execution Flow (main)

```text
1. Parse user description from Input
   → Feature: Captain Match Management for enhanced APA team operations
2. Extract key concepts from description
   → Actors: Team captains
   → Actions: schedule matches, track outcomes, view matches
   → Data: match information, schedules, results
   → Constraints: APA pool league context, captain permissions
3. Clarifications integrated (MVP scope):
   → Captain role already determined by existing team membership flag; multiple captains supported
   → Match types: league, makeup, practice (scrimmage); playoffs/tournament future consideration
   → No approval workflow; captains directly manage their own team's matches only
4. Fill User Scenarios & Testing section
   → Core flow: captain views → manages lineup (via lineup explorer) → records / updates results
5. Generate Functional Requirements
   → Each requirement focused on captain abilities and match lifecycle
6. Identify Key Entities: Matches, Teams, Captains, Match Results
7. Run Review Checklist
   → No outstanding clarification markers (MVP intentionally defers advanced features)
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines

- ✅ Focus on WHAT captains need for match management and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, database design)
- 👥 Written for business stakeholders and league administrators

---

## User Scenarios & Testing *(mandatory)*

 
### Primary User Story

As a team captain in an APA pool league, I need comprehensive match management capabilities so I can schedule matches beyond the standard league schedule, track all match outcomes in detail, and provide my team with better coordination and planning tools than what's available on the basic APA website.

### Acceptance Scenarios

MVP Focus (Frontend):

1. **Given** I am a captain, **When** I open Past Matches, **Then** I see a list of completed league matches with total team points and result (win/loss/other).
2. **Given** I select a past match, **When** I open its details, **Then** I see the breakdown of player matches and their points.
3. **Given** a match result was entered incorrectly, **When** an updated result is submitted via API, **Then** subsequent frontend views reflect the updated totals.

API Capability (Beyond Immediate UI):

1. **Given** I POST a practice match with type=practice, **When** I later GET the match, **Then** the stored type is returned (even if UI does not surface it yet).
2. **Given** I PUT updated game-level data for a player match, **When** I GET the containing team match, **Then** recalculated aggregates reflect the change.

### Edge Cases

- Fewer than five player matches: still valid team result (frontend must not assume always five).
- Unplayed player match: counts zero points (future configurable rule).
- Overwrite semantics: last write wins; no merge required.
- Orphan external league match id: allowed (frontend may ignore until sync feature exists).
- Player removed post-match: historical record retains original reference.

## Requirements *(mandatory)*

### Functional Requirements

Legend: (MVP-FE) Frontend MVP | (MVP-API) API MVP | (FUTURE) Deferred

- **FR-001 (MVP-API)**: Restrict match creation/update/delete to captains of that team (migrating from admin-only; legacy admin route logs deprecation until UI updated).
- **FR-002 (MVP-FE)**: Display past league team matches with opponent, date, final total points, outcome.
- **FR-003 (MVP-API)**: Allow creation of practice and makeup matches (UI may not expose yet).
- **FR-004 (MVP-API)**: Allow entry and overwrite of detailed results: up to five player matches each with games (nested routes on existing functions class).
- **FR-005 (MVP-API)**: Calculate player match totals from constituent games.
- **FR-006 (MVP-API)**: Aggregate team match total points from player match totals; treat missing player matches as zero.
- **FR-007 (MVP-API)**: Support fewer than five player matches while still finalizing a team match.
- **FR-008 (MVP-API)**: Restrict player assignment to rostered players of the team.
- **FR-009 (FUTURE)**: Integrate enhanced lineup planning automation (current manual tool linkage only in MVP).
- **FR-010 (MVP-API)**: Overwrite semantics: last submitted full set or incremental game additions replace prior data (preserve existing last-write-wins behavior).
- **FR-011 (MVP-API)**: Accept optional external league match identifier on league matches.
- **FR-012 (FUTURE)**: Advanced filtering (by type, opponent, date range) in UI.
- **FR-013 (MVP-FE)**: Provide Past Matches view (Upcoming tab deferred unless trivial to include read-only).
- **FR-014 (MVP-API)**: Enforce authorization preventing cross-team management.
- **FR-015 (FUTURE)**: Background APA sync job to auto-import scheduled/past matches.
- **FR-016 (FUTURE)**: Validation against APA rule constraints (skill cap, forfeits scoring).
- **FR-017 (FUTURE)**: Notification or messaging features.
- **FR-018 (FUTURE)**: Audit/version history of match results.
- **FR-019 (FUTURE)**: Media attachments (photos, notes) for matches.
- **FR-020 (FUTURE)**: Tournament/playoff/challenge match types.

Removed previously implied: separate Upcoming tab requirement now deferred; lineup explorer integration reframed (basic linkage only implicit, not a new feature requirement).

Out-of-scope (Reiterated / Future): opposing captain workflow, conflict resolution automation, performance targets, metrics/KPIs, roster eligibility enforcement beyond membership, forfeiture special point rules, lineup reveal sequencing, scheduling negotiation UI.

### Key Entities *(include if feature involves data)*

- **TeamMatch**: A match between two teams (types: league, makeup, practice) containing up to five PlayerMatches; may reference `externalLeagueMatchId` (optional). Existing `scheduledAt` is ALIASED to `matchDate`; legacy aggregate object (`totals`) retained alongside new `teamScoreHome/teamScoreAway` during transition.
- **PlayerMatch**: A contest between one player from each team; consists of one or more Games; produces player match points total and outcome.
- **Game (Rack)**: Atomic scoring unit with winner and points earned contributing to its PlayerMatch total.
- **LineupPlan**: Planned arrangement of potential PlayerMatches for an upcoming TeamMatch (managed via lineup explorer); not all planned PlayerMatches must be realized.
- **Captain**: Team member flagged with captain privileges enabling match management operations for that specific team.
- **Team**: Rostered group; source of eligible players for PlayerMatches.

Preservation Note: Existing `TeamMatch.lineupPlan` JSON structure (including skill cap validation metadata, history entries, alternates, availability fields) remains fully supported and is intentionally UNCHANGED by this feature. All new PlayerMatch/Game persistence operates alongside (not inside) `lineupPlan`. No migration required; existing lineup editing endpoints and explorer UI retain current semantics.

Scoring Model (MVP – Integrated):

- Sum(Game.points) -> PlayerMatch.points
- Sum(PlayerMatch.points for completed PlayerMatches) -> TeamMatch.teamScoreHome/teamScoreAway (new explicit fields; legacy `totals` also populated initially)
- Fallback: if all PlayerMatches have zero points but legacy `gamesWon*` present, use gamesWon (flag-controlled deprecation later)
- Unplayed/forfeited PlayerMatch -> zero points (future configurable rule)

---

## Review & Acceptance Checklist

GATE: Automated checks run during main() execution

### Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous  
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [ ] Dependencies and assumptions identified

---

## Execution Status

Updated by main() during processing

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] Review checklist passed

---
