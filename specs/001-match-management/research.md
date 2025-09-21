# Phase 0 Research – Match Management (Captain Enhancements Integration)

## Purpose

Establish concrete technical decisions and resolve unknowns for implementing captain match management (TeamMatch, PlayerMatch, Game hierarchy) using Azure Functions + Cosmos DB + static HTML/JS frontend.

Directory Rename: This feature directory will be renamed to `specs/001-match-management` (previously `specs/001-captain-match-management`).

LineupPlan Preservation: The embedded `lineupPlan` field inside existing TeamMatch documents (planning, alternates, availability, history, skill cap totals) is explicitly retained without structural changes; new PlayerMatch and Game documents augment results only.

## Source Inputs

- Feature Spec: `spec.md` (MVP + Future delineation; updated 2025-09-21 for integrated approach)
- Repo Architecture: .NET 8 Azure Functions backend (`functions/`), Jekyll static site (`docs/`), Cosmos DB via existing Python seed tooling (`db/`).
- Existing Patterns: Players & Teams models (partition strategies vary: players use `/id`, team memberships use `/teamId`). Existing `MatchesFunctions` + `LeagueService` reused (no parallel Captain* codepath).

## Domain Recap (MVP Scope)

- Captains need read-only view of historical matches in MVP frontend.
- API must support full CRUD for: TeamMatch, PlayerMatch, Game (aka Rack) results.
- Scoring: Support both rack-win counting (8-ball style) and per-rack point accumulation (9-ball style) via flexible Game.pointsHome/pointsAway fields; team aggregates favor summed points, falling back to gamesWon* when no points present.
- No scheduling, validation rules, notifications, audit trails, or external sync in MVP.

## Key Design Decisions

| Topic | Decision | Rationale | Alternatives Considered |
|-------|----------|-----------|--------------------------|
| Data storage model | Existing Cosmos container `TeamMatches` | Reuse deployed container & RU throughput; avoid divergent container management. | New container `matches` → fragmentation & migration overhead. |
| Partition Key | `/divisionId` | Divisions are the operational grouping for scheduling & historical queries; ensures balanced partitions even for teams with uneven activity. | `/teamId` → hotspot risk with very active teams; `/seasonId` → seasons span divisions; `/matchId` → no locality. |
| Hierarchy representation | Embedded + side-loaded hybrid | Root TeamMatch + array of PlayerMatch IDs; PlayerMatch/Game separate docs same partition. | Full embedding (size/ contention); fully normalized multi-partition lookups. |
| Identifier format | ULIDs (lexicographically sortable) | Chronological ordering & readability across new nested docs. | GUIDs unordered; nanoid less chronological utility. |
| Concurrency model | Last write wins, no ETag enforcement MVP | Matches existing repo pattern; keeps API simple initially. | Optimistic concurrency via `If-Match` ETags – add in future with auditing. |
| Soft delete | Not implemented MVP | Simplicity; spec excludes deletion recovery. | Add `isDeleted` flag now – premature. |
| Score storage | Store `teamScoreHome/teamScoreAway`, `gamesWon*`, cumulative `points*` redundantly | Dual format support, fast aggregates; backward compat with legacy totals. | Derive always (high RU); only gamesWon (blocks 9-ball points). |
| Date/time handling | Store UTC ISO8601 strings; index on `matchDate` | Consistency & ease of client formatting. | Epoch milliseconds – minor benefit; stick to readability. |
| Authorization | Extend existing auth: add AllowApiSecret for reads, enforce captain JWT for mutations | Minimal new surface, consistent with lineup code. | Parallel new auth stack (duplication). |
| Validation level | Minimal (field presence & type) | MVP defers business rule enforcement (e.g., skill cap). | Full business validation now – increases scope beyond spec. |
| Deployment path | Existing Functions project (`functions/league`) adding new Functions file | Reuse DI, logging, auth. | Separate project – fragmentation. |

## Outstanding Unknowns (Resolved or Deferred)

| Unknown | Status | Resolution / Deferral Rationale |
|---------|--------|-------------------------------|
| Need seasons/league grouping? | Deferred | Not required for MVP display; simplify partition choice. |
| Captain role claim presence | Deferred | Use API secret for MVP read; add role-based restrictions when Stytch attribute pipeline stable. |
| External APA match ID format | Deferred | Field stub can be optional future addition; exclude now. |
| Pagination requirements | Resolved | Implement basic `?limit` (default 25) and `?continuationToken` for matches listing. |
| Query filtering (date range) | Deferred | MVP just latest N matches; advanced filters future. |
| Multi-team perspective (both captains) | Resolved | Store both `homeTeamId`, `awayTeamId`; duplicate a minimal `teamPerspective` index doc if needed later (not for MVP). |

## Data Shape Drafts (High-Level)

(Full schema will be formalized in `data-model.md` Phase 1.)

TeamMatch (root)

- id (ulid)
- teamId (primary partition - owning perspective, typically home team)
- homeTeamId / awayTeamId
- matchDate (UTC string)
- status (planned|in_progress|completed) – MVP uses completed only
- playerMatchIds [] (array of child IDs)
- teamScoreHome / teamScoreAway (ints)
- createdUtc / updatedUtc

PlayerMatch (child)

- id (ulid)
- teamMatchId
- teamId (team perspective / owning partition or also store for redundancy?)
- homePlayerId / awayPlayerId
- homePlayerSkill / awayPlayerSkill (optional MVP)
- gamesWonHome / gamesWonAway
- totalRacks (int) // optional derived
- order (int) // sequence in lineup
- createdUtc / updatedUtc

Game

- id (ulid)
- playerMatchId
- rackNumber (1..n)
- winner (home|away)
- createdUtc

## Access Patterns

| Operation | Pattern | Notes |
|-----------|---------|-------|
| List recent matches for team | Query by `/divisionId` + filter team IDs then order by `matchDate` DESC | Secondary filter in query predicate; retains partition locality for division-level dashboards. |
| Get full match details | Fetch TeamMatch, then parallel query PlayerMatch + Games by `teamMatchId` | Potential later aggregation optimization with projections. |
| Create match | Insert TeamMatch (empty player array) | Further writes add player matches and games. |
| Update player match result | Patch PlayerMatch; recompute parent scores | Keep score recomputation server-side service method. |
| Add game result | Insert Game doc; update PlayerMatch & parent TeamMatch tallies | Write amplification acceptable (3 docs). |

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Partition key lock-in (`/divisionId`) may complicate single-team export tooling | Slight complexity | Provide teamId composite index or maintain materialized team index doc future. |
| Write amplification on game insert (3 updates) | RU cost | Batch update using transactional batch (all share `/divisionId`). |
| Absence of concurrency control may overwrite scores inadvertently | Data inconsistency | Add optional ETag concurrency in Phase 2 tasks or future enhancement. |
| Denormalized scores drift | Incorrect aggregates | Centralize score recompute in service; add reconciliation script later. |

## Decisions Needing Review Before Phase 1

- Confirm acceptance of single-container strategy for MVP.
- Confirm ULID adoption (introduce helper in .NET project) vs reuse existing GUID approach.
- Confirm minimal validation stance.

## Go/No-Go Criteria for Phase 1

- [x] All MUST decisions captured
- [x] No remaining NEEDS CLARIFICATION markers
- [x] Risks documented with mitigation direction
- [x] CRUD + read patterns defined at high-level

Status: READY for Phase 1.

## Migration Note: Legacy `gamesWon*` Fields

| Aspect | Current State | Transition Strategy | Removal Criteria |
|--------|---------------|---------------------|------------------|
| Dual fields | PlayerMatch stores `gamesWonHome/Away` and `pointsHome/Away` | Continue writing both when winner supplied; points authoritative when >0 | 90 days after production release with telemetry showing >95% of new Games include points |
| Game docs | Some legacy games have only `winner` (no points) | Backfill script (optional) to assign 1 point per win if we decide to eliminate pure winner model | Backfill complete + no new winner-only inserts for 30 days |
| Aggregation logic | Prefers points, falls back to gamesWon | Maintain fallback until deprecation flag enabled | Config flag `DISABLE_GAMESWON_FALLBACK=true` deployed |
| API contract | Winner optional | Keep optional; clients encouraged to send points | All active clients sending points consistently |
| Telemetry | Not yet instrumented | Add custom event `scoring_mode_used` (points\|gamesWon_fallback) | 0 fallback events over rolling 14 days |

Risk: Inconsistent historical totals if partial backfill performed.
Mitigation: Reconciliation routine scanning PlayerMatch vs sum(Game.points) and reporting drift metric.
