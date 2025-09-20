# Feature Specification: Captain Match Management

**Feature Branch**: `001-captain-match-management`  
**Created**: September 20, 2025  
**Status**: Draft  
**Input**: User description: "Team captains should be able to manage matches. Match management is a critical feature core to the SideSpins platform. The APA public website offers captains the ability to see upcoming matches and view outcomes of past matches, but lacks the flexibility for captains to schedule and track the outcomes of other matches. SideSpin's mission is to fill this gap and other critical gaps so that teams can experience a richer, more informed match management experience."

## Execution Flow (main)
```
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

1. **Given** I am a designated captain on my team, **When** I open Match Management, **Then** I see upcoming league, makeup, and practice matches for my team.
2. **Given** I have an upcoming team match, **When** I open the lineup explorer, **Then** I can plan the five (or fewer if forfeits) player matches and assign roster players.
3. **Given** a team match has concluded, **When** I enter player match game details (per-rack points), **Then** the system calculates each player match outcome and aggregates total team points.
4. **Given** I previously saved results with an error, **When** I overwrite the match data and save, **Then** the prior values are replaced (no historical audit retained in MVP).
5. **Given** a makeup or practice match needs to be recorded, **When** I create it, **Then** it is stored and clearly distinguished from official league matches.

### Edge Cases

- Fewer than five player matches played (forfeits or early end) still produce a valid team result.
- A player match not played may yield zero or configured points (APA rules mapping future consideration).
- Duplicate re-entry of the same match simply overwrites prior data.
- Player removed from roster after participating: retained on historical match record (future roster integrity checks deferred).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST restrict match management (create/update/delete match records and results) to users flagged as captains for that team.
- **FR-002**: System MUST display upcoming and past team matches (league, makeup, practice) with date, opponent, and type.
- **FR-003**: System MUST allow captains to create makeup and practice matches (league matches originate externally and are linked by external league match ID when available).
- **FR-004**: System MUST allow captains to enter and overwrite detailed results for a team match composed of up to five player matches.
- **FR-005**: System MUST calculate each player match outcome from one or more games (racks) using per-game points and aggregate player match totals.
- **FR-006**: System MUST aggregate team match total points from the sum of completed player match points (handling unplayed/forfeited player matches producing zero or configured points; forfeiture point logic future consideration—MVP uses zero).
- **FR-007**: System MUST permit fewer than five player matches to be recorded while still finalizing a team match.
- **FR-008**: System MUST allow captains to assign only active roster players from their own team to player matches.
- **FR-009**: System MUST persist lineup planning via existing lineup explorer interface before or after result entry (planning tool integration in scope, automation enhancements future consideration).
- **FR-010**: System MUST allow overwriting previously entered results with the most recent submission (no version history in MVP).
- **FR-011**: System SHOULD link league matches to an external league match identifier when provided (absence does not block entry).
- **FR-012**: System SHOULD support tagging match type (league, makeup, practice) for filtering in history views.
- **FR-013**: System MUST display separate tabs (Upcoming, Past) in the captain's match management UI.
- **FR-014**: System MUST prevent captains from managing matches for teams they are not captains of.
- **FR-015**: System SHOULD prepare for future background sync of scheduled/past league matches (not implemented in MVP).

Out-of-scope (MVP): notifications, opposing captain workflow, conflict resolution automation, audit trails, messaging, APA rule validation engine, performance targets, metrics/KPIs, roster eligibility enforcement beyond basic membership, forfeiture special point rules, advanced lineup reveal logic.

### Key Entities *(include if feature involves data)*

- **TeamMatch**: A match between two teams (types: league, makeup, practice) containing up to five PlayerMatches; may reference externalLeagueMatchId (optional).
- **PlayerMatch**: A contest between one player from each team; consists of one or more Games; produces player match points total and outcome.
- **Game (Rack)**: Atomic scoring unit with winner and points earned contributing to its PlayerMatch total.
- **LineupPlan**: Planned arrangement of potential PlayerMatches for an upcoming TeamMatch (managed via lineup explorer); not all planned PlayerMatches must be realized.
- **Captain**: Team member flagged with captain privileges enabling match management operations for that specific team.
- **Team**: Rostered group; source of eligible players for PlayerMatches.

Scoring Model (MVP):

- Sum(Game.points) -> PlayerMatch.points
- Sum(PlayerMatch.points for completed PlayerMatches) -> TeamMatch.totalPoints
- Unplayed/forfeited PlayerMatch -> zero points (future: configurable or rule-derived)

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
