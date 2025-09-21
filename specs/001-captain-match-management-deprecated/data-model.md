# Phase 1 Data Model – Match Management (Integrated Enhancements)

Derived from `spec.md` and validated by `research.md` decisions.

## Container Strategy

Existing Cosmos DB container: `TeamMatches` (integration approach – no new container introduced)

- Partition Key: `/divisionId`
- Justification: Aligns with established multi-tenant division sharding. All documents (TeamMatch, PlayerMatch, Game) for a given match reside in the same division, enabling transactional batch operations while supporting cross-team scheduling inside that division. Team‑scoped queries now filter within a division partition (cheap post-filter) rather than being the partition key itself.

## Cross-Document Relationships

| Parent | Child | Relation Type | Cardinality | Storage Pattern |
|--------|-------|---------------|------------|-----------------|
| TeamMatch | PlayerMatch | Reference via `teamMatchId` + parent summary array `playerMatchIds` | 1 → many (<=5 typical APA) | Hybrid (root stores child IDs; children separate docs) |
| PlayerMatch | Game | Reference via `playerMatchId` | 1 → many (1–9 racks) | Separate docs |

## Entity Schemas (JSON Draft)

Field types use JSON schema-ish shorthand.

### TeamMatch (Document Type: `teamMatch`)

```json
{
  "id": "ulid",                 // tm_ prefixed optional (implementation detail)
  "docType": "teamMatch",        // discriminator
  "divisionId": "string",        // partition key (shared by all related docs)
  "teamId": "string",            // owning/home perspective (alias of homeTeamId for legacy patterns if needed)
  "homeTeamId": "string",
  "awayTeamId": "string",
  "scheduledAt": "string(date-time, UTC)",   // legacy persisted field
  "matchDate": "string(date-time, UTC)",     // API alias surfaced in new responses (maps to scheduledAt)
  "status": "string",            // completed (MVP) | planned | in_progress (future)
  "playerMatchIds": ["string"],  // ordered set of PlayerMatch ids
  "teamScoreHome": 0,                        // new explicit aggregate (coexists with legacy totals.* if present)
  "teamScoreAway": 0,
  "externalLeagueMatchId": "string|null",   // optional linkage (added in enhancement)
  "createdUtc": "string(date-time)",
  "updatedUtc": "string(date-time)"
}
```

Validation (MVP): require homeTeamId != awayTeamId, scheduledAt present (matchDate accepted on write then mapped). Backward compatibility: existing documents with only scheduledAt still valid.
Future: enforce max 5 player matches; enforce lineup skill cap.

LineupPlan Preservation: The pre-existing `lineupPlan` object (with ruleset, skill cap totals, availability, alternates, history) continues to be stored inside the TeamMatch document exactly as before. New PlayerMatch/Game documents DO NOT replace or refactor this structure; they coexist. This allows current lineup explorer workflows (plan, lock, update availability) to proceed unaffected while match result recording gains finer-grained persistence.

Indexes (implicit): Partition (/divisionId) + id. Consider composite or in-partition filtering strategy for (`divisionId`, `teamId`, `matchDate` DESC) via query ordering; may rely on in-memory sort for limited result sets (< 50) MVP.

### PlayerMatch (Document Type: `playerMatch`)

```json
{
  "id": "ulid",
  "docType": "playerMatch",
  "divisionId": "string",        // same partition value as parent TeamMatch.divisionId
  "teamId": "string",            // copied (home perspective) to simplify team-level filters
  "teamMatchId": "string",       // FK to TeamMatch.id
  "order": 1,                     // lineup position (1..5)
  "homePlayerId": "string",
  "awayPlayerId": "string",
  "homePlayerSkill": 4,           // optional MVP (nullable)
  "awayPlayerSkill": 3,           // optional MVP (nullable)
  "gamesWonHome": 0,              // legacy (8‑ball style) – retained for compatibility (optional future deprecate)
  "gamesWonAway": 0,              // legacy
  "pointsHome": 0,                // cumulative points (9-ball or format‑agnostic)
  "pointsAway": 0,                // cumulative points
  "totalRacks": 0,                // optional (still track count separate from points)
  "createdUtc": "string(date-time)",
  "updatedUtc": "string(date-time)"
}
```

Validation (MVP minimal): homePlayerId != awayPlayerId.
Future: enforce valid skill ranges, order uniqueness per match.

### Game (Document Type: `game`)

```json
{
  "id": "ulid",
  "docType": "game",
  "divisionId": "string",       // inherited partition (copied from parent TeamMatch.divisionId)
  "teamId": "string",           // copied for simplified filtering (not key)
  "playerMatchId": "string",
  "rackNumber": 1,               // sequential within player match
  "pointsHome": 0,               // points earned by home player this rack
  "pointsAway": 0,               // points earned by away player this rack
  "winner": "home" | "away" | null, // optional; derived when points system implies discrete winner
  "createdUtc": "string(date-time)"
}
```

Validation MVP: rackNumber >=1; pointsHome >=0; pointsAway >=0; winner optional (if provided must be home/away consistent with points).
Future: ensure no duplicate rackNumber per playerMatchId; add format rules (e.g., max points per rack per format).

## Derived / Aggregated Fields

- TeamMatch.teamScoreHome / teamScoreAway recomputed after PlayerMatch or Game changes using either (priority order): pointsHome/pointsAway sums OR fallback to gamesWon* if points not present (controlled by `DISABLE_GAMESWON_FALLBACK` feature flag).
- PlayerMatch.totalRacks = max(rackNumber) or count(Game) documents (independent of points).
- Legacy Compatibility: During transition both gamesWon fields and points fields may coexist; recompute logic prefers points if any Game has points > 0.

## Write Flows

1. Create TeamMatch → insert TeamMatch doc (empty playerMatchIds). All subsequent docs share the same `divisionId`.
2. Add PlayerMatch → insert PlayerMatch doc + patch TeamMatch.playerMatchIds (transactional batch within `/divisionId`).
3. Record Game Result → insert Game doc + patch PlayerMatch (increment pointsHome/pointsAway, optionally gamesWon* if winner present) + recompute parent TeamMatch scores (transactional batch; all share `divisionId`).

## Transactional Batch Composition Examples

Add PlayerMatch batch operations (same division partition):

- Create PlayerMatch
- Patch TeamMatch (append playerMatchId)

Add Game batch operations (same division partition):

- Create Game
- Patch PlayerMatch (increment points & optionally gamesWon*, update totals)
- Patch TeamMatch (recompute teamScoreHome/away from cumulative points)

## Error Handling & Integrity

- If recompute fails in batch → entire batch aborts (atomicity guaranteed inside partition).
- Out-of-order racks accepted MVP (no uniqueness enforcement) – UI can sort.

## Migration / Future Evolution

| Change | Impact | Strategy |
|--------|--------|----------|
| Introduce seasons | Need seasonId field | Backfill script; new queries filter by seasonId |
| Expose matchDate only | Clients may rely on scheduledAt | Continue dual fields until telemetry shows new field adoption >95% |
| Remove gamesWon fallback | Some historical matches rely on gamesWon* | Telemetry + feature flag `DISABLE_GAMESWON_FALLBACK=true` after stability window |
| Add validation rules | Might invalidate historical data | Apply only to new writes; add `schemaVersion` field |
| Add audit trail | Additional container / event sourcing | Append-only events referencing entity IDs |
| Change partition key | Requires re-partitioning | Data export + reimport into new container, dual-write interim |

## Open Questions (Logged for Future Phase, not blocking MVP)

- Should we pre-compute per-player win/loss stats? (Defer until dedicated analytics requirement.)
- Will we need global match search across teams? (Would motivate alternative partition or secondary index approach.)

## Ready Checklist

- [x] Schemas defined with discriminator `docType`.
- [x] Partition & batching strategy defined.
- [x] CRUD flows mapped to batch operations.
- [x] Validation boundaries explicit (MVP vs Future).

Status: READY for API contract drafting.
