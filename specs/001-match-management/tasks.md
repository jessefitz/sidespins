# Phase 2 Tasks – Match Management (Captain Enhancements Integration)

Constitution Reference: v1.0.0 (Cohesion rule – extend existing `MatchesFunctions.cs`, do not create parallel functions class.)

Feature Directory: `specs/001-match-management` (directory renamed from `specs/001-captain-match-management`)
Branch: `001-match-management` (renamed from `001-captain-match-management` on 2025-09-21)
Generated: 2025-09-20 (updated 2025-09-21 for integration approach)

Legend:
LineupPlan Preservation: All tasks assume existing lineup planning (`lineupPlan` object within TeamMatch) remains untouched and continues to power lineup explorer & availability features; no migration tasks required.

- `[P]` = Can run in parallel with other `[P]` tasks (different files / no ordering dependency)
- Dependencies use task IDs; a task without unmet dependencies may start
- Contract / model tests precede implementation (TDD ordering)

## Parallel Execution Examples

Example batch (after foundational tasks):

```bash
/specify run T008 T009 T010  # Run independent model unit test creations in parallel
/specify run T021 T022 T023  # Run read endpoint implementations concurrently
```

---
 
## Task List

### Setup & Foundation (Unchanged Core Utilities)

**T001** – Create ULID utility & time provider

- Files: `functions/league/UlidGenerator.cs`, `functions/league/ITimeProvider.cs`, `functions/league/SystemTimeProvider.cs`
- Purpose: Provide deterministic id & timestamp abstractions used across services/endpoints
- Acceptance: Utility returns 26-char ULID; time provider returns UTC now; unit test skeleton added
- Dependencies: None

**T002** – Add scoring constants & enums

- Files: `functions/league/Scoring.cs`
- Content: Enum `RackWinner { Home, Away }`, static validation methods (non-negative points)
- Acceptance: Methods throw `ArgumentException` on invalid values
- Dependencies: T001

**T003** – Extend existing `LeagueModels.cs` with new / augmented models (PlayerMatch, Game, TeamMatch score fields)

- Files: `functions/league/LeagueModels.cs`
- Content: Public record types for create/request/response shapes matching `data-model.md` & `contracts/endpoints.md`
- Acceptance: Builds; JSON property names camelCase; includes `docType`
- Dependencies: T001

**T004** – Add configuration flags

- Files: `functions/Program.cs` (DI), `functions/league/FeatureFlags.cs`
- Flags: `ALLOW_SECRET_MUTATIONS`, `DISABLE_API_SECRET_MUTATIONS`, `DISABLE_GAMESWON_FALLBACK`
- Acceptance: Flags registered via options pattern; accessible in services
- Dependencies: T001

### Model & Service Test Stubs (TDD First)

**T005 [P]** – Unit test stub: TeamMatch model serialization

- Files: `functions/tests/TeamMatchModelTests.cs`
- Acceptance: Round-trip serialization preserves fields
- Dependencies: T003

**T006 [P]** – Unit test stub: PlayerMatch score accumulation logic (expected future service call)

- Files: `functions/tests/PlayerMatchModelTests.cs`
- Acceptance: Placeholder asserting initial zero state
- Dependencies: T003

**T007 [P]** – Unit test stub: Game validation (points non-negative)

- Files: `functions/tests/GameModelTests.cs`
- Dependencies: T002, T003

### Services (Score & Batch Logic)

**T008** – Implement recompute logic (initially internal to `LeagueService`)

- Files: `functions/league/IScoreRecomputeService.cs`, `functions/league/ScoreRecomputeService.cs`
- Behavior: Recompute PlayerMatch + TeamMatch aggregates from enumerated Games per `plan.md` algorithm
- Acceptance: Methods: `RecomputePlayerMatchAsync`, `RecomputeTeamMatchAsync` (points priority, fallback gamesWon if flag false)
- Dependencies: T003, T004

**T009** – Extend `LeagueService` with PlayerMatch & Game persistence methods (no new abstraction yet)

- Files: `functions/league/IMatchPersistence.cs`, `functions/league/CosmosMatchPersistence.cs`
- Behavior: CRUD helpers (CreateTeamMatchAsync, AddPlayerMatchBatchAsync, AddGameBatchAsync, Get* methods)
- Acceptance: Uses transactional batch (AddPlayerMatch, AddGame) within `/divisionId`
- Dependencies: T003, T004

**T010** – Recompute unit tests (happy paths)

- Files: `functions/tests/ScoreRecomputeServiceTests.cs`
- Cases: points only, legacy gamesWon only, mixed with points >0
- Dependencies: T008

**T011** – Persistence unit test (batch composition via `LeagueService` methods)

- Files: `functions/tests/CosmosMatchPersistenceTests.cs`
- Mock Cosmos client; assert correct operations count for AddGameBatch
- Dependencies: T009

### Auth & Middleware Adjustments

**T012** – Introduce `AllowApiSecretAttribute`

- Files: `functions/auth/AllowApiSecretAttribute.cs`
- Acceptance: Attribute recognized via middleware reflection
- Dependencies: Existing auth system

**T013** – Middleware enhancement for dual auth

- Files: `functions/auth/AuthenticationMiddleware.cs` (update)
- Behavior: If endpoint decorated with `AllowApiSecret` and header valid, issue limited principal (AuthMode=ApiSecret)
- Dependencies: T012

**T014** – Enforce mutation secret flags

- Files: `functions/auth/AuthenticationMiddleware.cs`
- Behavior: If mutation endpoint, secret-only request allowed only when `ALLOW_SECRET_MUTATIONS=true` and `DISABLE_API_SECRET_MUTATIONS` false
- Dependencies: T013, T004

### Endpoint Contract Tests (Before Implementations)

**T015 [P]** – Contract test: Create TeamMatch (201 shape)

- Files: `functions/tests/contracts/CreateTeamMatchContractTests.cs`
- Dependencies: T003, T009 (mock), T012

**T016 [P]** – Contract test: List Team Matches (paging shape)

- Files: `functions/tests/contracts/ListTeamMatchesContractTests.cs`
- Dependencies: T003, T009

**T017 [P]** – Contract test: Get TeamMatch (200 shape & 404)

- Files: `functions/tests/contracts/GetTeamMatchContractTests.cs`
- Dependencies: T003, T009

**T018 [P]** – Contract test: Add PlayerMatch (201 shape)

- Files: `functions/tests/contracts/AddPlayerMatchContractTests.cs`
- Dependencies: T003, T009

**T019 [P]** – Contract test: Record Game (201 shape, increments aggregates stub)

- Files: `functions/tests/contracts/RecordGameContractTests.cs`
- Dependencies: T003, T009, T008

**T020 [P]** – Contract test: List Games (array shape)

- Files: `functions/tests/contracts/ListGamesContractTests.cs`
- Dependencies: T003, T009

### Endpoint Implementations (Augment Existing `MatchesFunctions`)

**T021** – Enhance existing Create Match endpoint (add optional fields, captain auth) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (Create handler)
- Behavior: Validate body, create TeamMatch, return 201
- Dependencies: T015

**T022** – Enhance list matches (team + division filters, include new score fields) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (List handler)
- Behavior: Query by divisionId + teamId, order by matchDate DESC (limit + continuation)
- Dependencies: T016

**T023** – Enhance get match (alias `scheduledAt` → `matchDate`, include new scores) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (Get handler)
- Behavior: Retrieve by id; 404 on missing
- Dependencies: T017

**T024** – Add PlayerMatch nested route (POST `/team-matches/{id}/player-matches`) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (AddPlayerMatch handler)
- Behavior: Transactional batch add; update parent ids
- Dependencies: T018, T009

**T025** – Add Game nested route (POST `/player-matches/{id}/games` + recompute) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (RecordGame handler)
- Behavior: Transactional batch add game + update PlayerMatch + recompute TeamMatch via service
- Dependencies: T019, T008, T009

**T026 [P]** – List Games nested route (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs` (ListGames handler)
- Behavior: Query games by playerMatchId ordered by rackNumber
- Dependencies: T020

**T027** – (Optional) Patch PlayerMatch Function (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs`
- Behavior: Allow manual correction (gamesWon/points) with recompute
- Dependencies: T010, T018

**T028** – (Optional) Delete TeamMatch Function (with cascading child cleanup) (File: `functions/league/MatchesFunctions.cs`)

- Files: `functions/league/MatchesFunctions.cs`
- Behavior: Delete TeamMatch + child docs (iterative) – best-effort MVP
- Dependencies: T023, T024, T025

### Integration Tests & Flows

**T029** – Integration: Create → Add PlayerMatch → Record Games flow

- Files: `functions/tests/integration/MatchEndToEndTests.cs`
- Dependencies: T021, T024, T025

**T030 [P]** – Integration: List & Get match after game inserts

- Files: `functions/tests/integration/MatchReadTests.cs`
- Dependencies: T022, T023, T025

**T031 [P]** – Integration: Auth modes (secret read allowed, secret mutate flagged)

- Files: `functions/tests/integration/AuthModesTests.cs`
- Dependencies: T014, endpoints implemented

### Telemetry & Logging

**T032** – Add structured logging fields (authMode, divisionId, teamMatchId, correlationId, latencyBucket, principalUserId) to enhanced handlers

- Files: `functions/league/MatchesFunctions.cs`, `functions/auth/AuthenticationMiddleware.cs`
- Dependencies: T021–T026, T014

**T033** – Emit custom event `scoring_mode_used` (points vs gamesWon fallback) in recompute logic

### Flag Rationalization & Additional Telemetry (New)

**T046** – Instrument secret usage telemetry events

- Files: `functions/auth/AuthenticationMiddleware.cs`, `functions/league/MatchesFunctions.cs`, `functions/league/ScoreRecomputeService.cs`
- Events: `match_secret_read` (on successful read via ApiSecret), `match_secret_mutation_attempt_blocked` (when secret write denied)
- Acceptance: Events emit with properties: `endpoint`, `authMode`, `divisionId` (if available), `teamMatchId`/`playerMatchId` (if available)
- Dependencies: T014, T021–T026

**T047** – Introduce refined flags `ALLOW_SECRET_MATCH_READS`, `ALLOW_SECRET_MATCH_WRITES`

- Files: `functions/league/FeatureFlags.cs`, `functions/Program.cs`, `functions/auth/AuthenticationMiddleware.cs`
- Behavior: Replace usage of `ALLOW_SECRET_MUTATIONS` / `DISABLE_API_SECRET_MUTATIONS` (keep legacy names mapped but mark deprecated in comments)
- Acceptance: Middleware enforces reads vs writes independently; unit tests updated (extend T031)
- Dependencies: T014

**T048** – Update integration tests for new flags

- Files: `functions/tests/integration/AuthModesTests.cs`
- Cases: read allowed / write blocked matrix for combinations of READS/WRITES flags; legacy flags path still passes when mapped
- Dependencies: T047, T031

### Integration / Deprecation & Backward Compatibility

**T036** – Add deprecation logging for legacy admin-only mutation routes (if still invoked)

**T037** – Alias handling: surface `matchDate` in responses while persisting `scheduledAt`

**T038** – Add feature flag `DISABLE_GAMESWON_FALLBACK`; wire into recompute logic

- Files: `functions/league/ScoreRecomputeService.cs`
- Trigger: On recompute path deciding fallback to gamesWon vs points
- Dependencies: T008

### Documentation & Polish

**T034 [P]** – Update `quickstart.md` with JWT vs secret examples placeholder

- Files: `specs/001-captain-match-management/quickstart.md`
- Dependencies: Auth tasks (T014)

**T035 [P]** – Add migration doc snippet referencing telemetry thresholds

- Files: `specs/001-captain-match-management/research.md` (append) OR separate `migration.md`
- Dependencies: T033

**T036 [P]** – Performance test script stub (PowerShell) for batch game inserts

- Files: `functions/test-performance-match-inserts.ps1`
- Dependencies: T025

**T037 [P]** – Lint / style pass & build verification

- Files: solution-wide
- Dependencies: All implementation tasks (T021–T033)

**T038** – Final readiness checklist & remove optional endpoints if deferred

- Files: `specs/001-captain-match-management/plan.md` (update Phase status)
- Dependencies: All

### Minimal Frontend Outcomes Tasks (MVP Read-Only Viewer)

These tasks (prefixed MF) are REQUIRED for the basic outcomes viewer and are distinct from the previously deferred advanced browsing tasks.

**MF01** – Past Matches Page Scaffold

- Files: `docs/matches.html`
- Content: Basic HTML shell with container, form for divisionId/teamId (if not provided via query params), table skeleton, "Load more" button
- Acceptance: Page loads without JS errors; placeholder message when no data
- Dependencies: T022 (list endpoint)

**MF02** – Match Detail Page Scaffold

- Files: `docs/match.html`
- Content: Shell with heading, summary section, expandable container for player matches
- Acceptance: Shows loading state, then basic match metadata after fetch
- Dependencies: T023 (get endpoint)

**MF03** – Shared Results JS Module

- Files: `docs/assets/js/match-results.js`
- Functions: `fetchJson(path)`, `loadMatches(divisionId,teamId,limit,continuation)`, `loadMatch(matchId)`, `loadPlayerMatch(id)`, `loadGames(playerMatchId)`
- Acceptance: Each function returns parsed JSON or throws with descriptive error
- Dependencies: T022, T023, T026

**MF04** – Render & Interaction Logic

- Files: `docs/assets/js/match-results.js`
- Implement: `renderMatchList(tableEl, items)`, `appendMatches(...)`, `renderMatchDetail(root, teamMatch)`, lazy load player matches/games on expand
- Acceptance: Expanding a player match fetches (if not already loaded) and displays games list
- Dependencies: MF01–MF03

**MF05** – Accessibility & Semantics Pass

- Files: `docs/matches.html`, `docs/match.html`, JS module
- Add: Table headers `scope="col"`, accordion buttons with `aria-expanded`, regions with `role="region"`, loading live region
- Acceptance: Basic axe / manual audit passes (no critical A violations)
- Dependencies: MF04

**MF06** – Outcome Label & Points Formatting

- Files: `docs/assets/js/match-results.js`
- Add helper: `computeOutcome(home, away)` returning Win/Loss/Tie + CSS class mapping
- Acceptance: Correct outcome shown for sampled values; tie case handled
- Dependencies: MF04

**MF07** – Quickstart Documentation Update (Viewer Section)

- Files: `specs/001-match-management/quickstart.md`
- Add: Section "Viewing Match Outcomes in Browser" with instructions & sample markup expectations
- Dependencies: MF01–MF06

**MF08** – Smoke Script (Optional Minimal)

- Files: `scripts/test-match-viewer.ps1`
- Script: curl list endpoint; parse JSON; output count; (optional) open matches.html guidance comment
- Dependencies: MF07

### Minimal Outcomes Viewer Exit Criteria

- Past matches page lists real data
- Detail page shows player matches & games on demand
- Outcome label derived correctly
- Accessibility basics implemented
- Quickstart updated

### Frontend Browsing Tasks (Deferred to Future Implementation)

### Safeguard Task

**SAFE01** – Verify no new match functions class introduced

- Scan repo for `CaptainMatchesFunctions.cs` or similarly named new files. Fail CI if found.
- Add check script or lightweight grep in build pipeline.
- Acceptance: Pipeline script returns zero matches.

> **Note**: The following frontend browsing tasks (T039-T050) were originally planned for advanced UI features but have been **deferred to future implementation**. The core captain match management functionality is complete through Phase 8 with:
>
> - ✅ **Complete backend API** (Phases 1-7)
> - ✅ **Functional frontend foundation** (Phase 8)
> - ✅ **Production-ready match management interface**
>
> Advanced browsing features and interactive scoring UI will be implemented in a future enhancement phase.

**T039** – ~~Create matches list page scaffold~~ [DEFERRED]

- Files: `docs/matches.html`
- Content: Basic HTML structure with container, placeholder div `#match-list-root`, query param parsing stub
- Dependencies: T022 (list endpoint)
- Status: Deferred - Basic match listing already available in main interface

**T040** – ~~Create match detail page scaffold~~ [DEFERRED]

- Files: `docs/match-detail.html`
- Content: Basic HTML with container, placeholder div `#match-detail-root` reading `id` & `divisionId` params
- Dependencies: T023 (get endpoint)
- Status: Deferred - Match details available through tabbed interface

**T041 [P]** – ~~JS module for API fetch helpers~~ [DEFERRED]

- Files: `docs/assets/js/matches-api.js`
- Functions: `fetchWithSecret`, `getTeamMatches`, `getTeamMatch`, `getPlayerMatch`, `getGames`
- Dependencies: T022, T023
- Status: Deferred - API integration already complete in CaptainMatchManager

**T042 [P]** – ~~JS module for list rendering & pagination~~ [DEFERRED]

- Files: `docs/assets/js/matches-list.js`
- Features: Render table, load more button, continuation token management, empty state
- Dependencies: T041
- Status: Deferred - Match listing functionality already implemented

**T043 [P]** – ~~JS module for match detail rendering~~ [DEFERRED]

- Files: `docs/assets/js/match-detail.js`
- Features: Fetch TeamMatch, lazy load player matches & games on expand, accessible accordion
- Dependencies: T041, T025 (games recorded)
- Status: Deferred - Match details available in current interface

**T044** – ~~Wire scripts into pages~~ [DEFERRED]

- Files: `docs/matches.html`, `docs/match-detail.html`
- Add `<script>` tags and init functions on DOMContentLoaded
- Dependencies: T042, T043
- Status: Deferred - Script integration complete in main page

**T045 [P]** – ~~Accessibility pass~~ [DEFERRED]

- Files: `docs/matches.html`, `docs/match-detail.html`, JS modules
- Add ARIA attributes, semantic table headers, keyboard navigation for accordion
- Dependencies: T044
- Status: Deferred - Current implementation includes accessibility features

**T046 [P]** – ~~LocalStorage preferences & error handling~~ [DEFERRED]

- Files: `docs/assets/js/matches-preferences.js`
- Store last divisionId/teamId; implement retry with capped attempts; show alert region
- Dependencies: T042
- Status: Deferred - Basic preferences and error handling already implemented

**T047** – ~~Performance / network optimization~~ [DEFERRED]

- Files: `docs/assets/js/match-detail.js`
- Implement lazy load of games (fetch only on accordion expand), simple in-memory cache
- Dependencies: T043
- Status: Deferred - Performance optimizations planned for future enhancement

**T048 [P]** – ~~Quickstart doc update (frontend usage)~~ [DEFERRED]

- Files: `specs/001-captain-match-management/quickstart.md`
- Add section "Viewing Matches in Browser" with examples
- Dependencies: T044
- Status: Deferred - Current documentation covers implemented functionality

**T049** – ~~Plan & tasks alignment update~~ [DEFERRED]

- Files: `specs/001-captain-match-management/plan.md`, `specs/001-captain-match-management/tasks.md`
- Mark frontend section referenced; update Phase status note
- Dependencies: T048
- Status: **COMPLETED** - Documentation updated to reflect current implementation status

**T050** – ~~Frontend smoke test script~~ [DEFERRED]

- Files: `scripts/test-frontend-matches.ps1`
- Steps: curl list endpoint, validate JSON shape fields; optional open matches.html check instructions
- Dependencies: T042
- Status: Deferred - API testing already comprehensive with existing PowerShell scripts

---
 
## Dependency Overview (Graph Summary)

- Foundation: T001 → T002/T003/T004
- Services: T003+T004 → T008/T009 → T010/T011
- Auth: T012 → T013 → T014
- Contract Tests: T015–T020 depend on models/persistence/services as noted
- Endpoints: Each implementation depends on its contract test
- Integration: After core endpoints
- Telemetry: After scoring & auth
- Docs/Polish: After telemetry + auth

---
 
## Parallel Scheduling Cheat Sheet

Stage 1 (after T003/T004): Run T005 T006 T007 concurrently
Stage 2 (after T008/T009): Run T010 T011 in parallel
Stage 3 (after T014): Run contract tests T015–T020 in parallel
Stage 4: Implement endpoints T021–T026 (T026 parallel once list/get started)
Stage 5: Run integration tests T029 T030 T031 (T030 & T031 parallel after T029 base path validated)
Stage 6: Docs & polish T034–T037 in parallel, then T038 final

---
 
## Exit Criteria

- All endpoints implemented & passing contract + integration tests
- Score recompute service covers points-first and legacy fallback paths
- Telemetry event present & observable
- Auth secret mutation gating enforced by flag
- Documentation updated (quickstart, research migration thresholds)
- Build, lint, and basic performance script succeed

---
(End of tasks)
