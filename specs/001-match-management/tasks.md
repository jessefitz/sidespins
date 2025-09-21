# Phase 2 Tasks – Match Management (Captain Enhancements Integration)

_Migrated from `specs/001-captain-match-management/tasks.md` on 2025-09-21._

Feature Directory: `specs/001-match-management` (renamed from `specs/001-captain-match-management`)
Branch: `001-match-management` (renamed from `001-captain-match-management` on 2025-09-21)
Generated: 2025-09-20 (updated 2025-09-21 for integration approach)

Legend:
LineupPlan Preservation: All tasks assume existing lineup planning (`lineupPlan` object within TeamMatch) remains untouched and continues to power lineup explorer & availability features; no migration tasks required.

- `[P]` = Can run in parallel with other `[P]` tasks (different files / no ordering dependency)
- Dependencies use task IDs; a task without unmet dependencies may start
- Contract / model tests precede implementation (TDD ordering)

(Original detailed task list preserved exactly as source; see legacy file for historical context.)

> NOTE: Frontend browsing tasks T039–T050 deferred; core implementation marked production-ready in plan.
