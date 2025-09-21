# Constitutional Future Considerations

This document captures forward-looking items derived from Constitution v1.0.0 that impact upcoming feature work, with emphasis on authentication tightening, deprecation schedules, and avoidance of parallel code paths.

## 1. Authentication Strategy Roadmap

| Phase | Goal | Scope | Conditions to Advance |
|-------|------|-------|-----------------------|
| A (Current MVP) | Transitional dual mode | API secret allowed for read (list/detail); JWT required for mutations (captain/admin) with temporary secret override flag | JWT login UI shipped & stable session handling |
| B | Read paths prefer JWT | UI fetches include bearer token; secret still accepted only in lower envs | Secret usage telemetry < 30% of read calls (prod) |
| C | Secret reads deprecated in prod | Production rejects secret on read endpoints; allowed only for maintenance endpoints list (seed/import) | Maintenance endpoint list ratified; errors < 0.1% post cutover |
| D | Secret mutations fully disabled | All write operations JWT-only; secret accepted only for offline tooling function(s) not exposed publicly | No secret mutation attempts for 14 days |
| E | Fine-grained role claims | Distinct captain vs admin vs read-only claims; per-division constraints | Role claims present on >95% tokens |

### Planned Flags (Rationalized)

| Flag | Type | Default (Local) | Default (Prod) | Notes |
|------|------|-----------------|----------------|-------|
| ALLOW_SECRET_MATCH_READS | bool | true | false | Governs secret access to read endpoints |
| ALLOW_SECRET_MATCH_WRITES | bool | true | false | Future default becomes false everywhere but local/dev |
| DISABLE_GAMESWON_FALLBACK | bool | false | false (until metric threshold met) | Disables legacy fallback scoring |

Deprecated Flags (to remove after one release cycle): `ALLOW_SECRET_MUTATIONS`, `DISABLE_API_SECRET_MUTATIONS`.

### Required Middleware Enhancements

- Enrich auth context with `AuthMode` (ApiSecret/Jwt) and optional `UserId`.
- Deny secret-based writes if `ALLOW_SECRET_MATCH_WRITES` is false (log structured warning).
- Correlate requests with correlation id header or generated value.

## 2. Logging & Observability Minimum Set

All match-related endpoints MUST log structured fields:

- `correlationId`
- `endpoint`
- `authMode`
- `principalUserId` (nullable)
- `divisionId` (when present)
- `teamMatchId` / `playerMatchId` (when present)
- `latencyMs` + derived `latencyBucket` (<100 / <250 / <500 / >=500)
- `outcome` (Success | ClientError | ServerError)

Additional Telemetry Events:

- `match_secret_read` (read via secret)
- `match_secret_mutation_attempt_blocked`
- `scoring_mode_used` (properties: `mode=points|gamesWon_fallback`)

## 3. Scoring Fallback Deprecation Path

| Milestone | Metric | Action |
|-----------|--------|--------|
| Telemetry Enabled | Event volume recorded | Monitor daily fallback count |
| Threshold Achieved | fallback usage <5% over 14 days | Set `DISABLE_GAMESWON_FALLBACK=true` in staging |
| Production Cutover | fallback usage <2% over prior 14 days | Enable flag in production |
| Post-Cut Verification | 0 fallback events for 14 days | Remove fallback code path in MAJOR or MINOR update (documented) |

## 4. Parallel Code Path Guardrails

- No new file introducing alternative match endpoints (e.g., `CaptainMatchesFunctions.cs`) without: updated spec, Complexity Tracking entry, architectural diff.
- CI script (SAFE01) will grep for forbidden filenames; build fails if present.

## 5. Future Principle-Driven Enhancements (Not Yet Scheduled)

| Topic | Rationale | Trigger |
|-------|-----------|---------|
| Aggregated match detail endpoint | Reduce N+1 calls & latency | Mobile p95 > 250ms for detail view |
| Event-sourced audit trail | Security & integrity (Principle 5) | First disputed score incident or compliance request |
| Role-based per-division access | Principle 4 least privilege | Multi-division deployment scenario |
| Scoring strategy abstraction | Maintain cohesion with second format | Introduction of alternate league format |
| Accessibility audit automation | Sustain Principle 3 UX | Two consecutive UI features shipped |

## 6. Documentation Synchronization Checklist

When modifying auth or scoring behavior, update in same PR:

- `specs/001-match-management/plan.md` (Constitution Check table)
- `specs/001-match-management/tasks.md` (add/remove tasks)
- `specs/constitutional-future-considerations.md` (this file)
- `README.md` or `/docs/auth/` if public-facing behavior changes.

## 7. Open Questions (To Capture in Future Specs)

- Will captains need historical revision view (diff of score changes)?
- Do we require per-rack timing or pacing metrics for performance insight?
- Should secret usage be fully disabled in staging earlier than production? (Proposal: yes, one release prior.)

---
Prepared: 2025-09-21 (post-constitution v1.0.0 adoption). Amend alongside any governance changes.
