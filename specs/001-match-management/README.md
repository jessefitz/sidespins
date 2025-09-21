# Match Management Feature (Spec 001)

This directory contains the canonical specification assets for the integrated Match Management enhancements (TeamMatch + nested PlayerMatch + Game) merged into existing `MatchesFunctions` / `LeagueService` code paths.

## Rename Notice

Previous path: `specs/001-captain-match-management/`
Current path: `specs/001-match-management/`
Rename Date: 2025-09-21
Rationale: Reflect shift from standalone "captain" feature track to an incremental enhancement of existing match handling while retaining lineup planning continuity.

## Key Goals

- Add granular persistence for player-vs-player results (PlayerMatch) and per-rack scoring (Game)
- Maintain existing TeamMatch `lineupPlan` structure and workflows untouched
- Introduce explicit team aggregate score fields (points-first, gamesWon fallback behind flag)
- Support dual auth (API secret read; JWT captain for mutations) during transition
- Keep single-container (`TeamMatches`) approach with partition key `/divisionId`

## Documentation Set

| File | Purpose |
|------|---------|
| `spec.md` | Full narrative spec + scope & non-goals |
| `plan.md` | Implementation strategy & sequencing |
| `data-model.md` | Canonical JSON schema drafts & batch patterns |
| `contracts/endpoints.md` | REST contract definitions |
| `tasks.md` | (Trimmed) task list reference – original detailed list in legacy dir |
| `quickstart.md` | Local exercise instructions for endpoints |
| `research.md` | Phase 0 decisions & risk ledger |
| `README.md` | (This file) orientation & rename rationale |

> Legacy directory retained temporarily for diff review; do not update it. All future edits belong here.

## LineupPlan Preservation

The embedded `lineupPlan` object in `TeamMatch` documents is intentionally unchanged. New PlayerMatch/Game documents augment scoring only. Any refactor of `lineupPlan` (e.g., normalization or versioning) is explicitly out of this feature scope.

## Feature Flags

| Flag | Purpose | Default (local) |
|------|---------|-----------------|
| ALLOW_SECRET_MUTATIONS | Temporarily allow API secret writes | true |
| DISABLE_API_SECRET_MUTATIONS | Force-disable secret writes | false |
| DISABLE_GAMESWON_FALLBACK | Remove legacy gamesWon aggregate fallback | false |

## Scoring Mode

1. Sum PlayerMatch.pointsHome/pointsAway if any points > 0 exist
2. Else fallback to gamesWonHome/gamesWonAway (until disabled by flag)
Telemetry event `scoring_mode_used` planned to track usage before removing fallback.

## Migration Checklist

- [x] Copy spec assets to new directory
- [x] Add migration README (this file)
- [x] Copy & lint endpoint contracts
- [ ] Update internal links in other docs (root README, architecture docs) to point here
- [ ] Remove legacy directory after one full release cycle

## Next (Implementation) Steps (Codebase)

1. Extend `LeagueModels.cs` with PlayerMatch / Game + new TeamMatch fields
2. Introduce recompute service (points-first fallback toggle)
3. Add persistence helpers & transactional batch logic
4. Implement endpoints incrementally with dual auth gating
5. Telemetry event + structured logging fields (authMode, divisionId, teamMatchId)

## Contact & Ownership

Owner: Match Management / League Domain (initial authoring by enhancement initiative 001)  
Questions: Open an issue with label `feature:match-management`.

---

This directory is now the single source of truth for spec 001. Please avoid divergence by editing only these files going forward.
