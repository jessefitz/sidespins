<!--
Sync Impact Report
Version change: (none prior) → 1.0.0
Modified principles: N/A (initial definition)
Added sections: Core Principles; Architecture & Technology Constraints; Development Workflow & Quality Gates; Governance
Removed sections: None
Templates requiring updates:
 - .specify/templates/plan-template.md (✅ reference will need version sync on next regeneration; contains trailing line referencing old placeholder version) 
 - .specify/templates/spec-template.md (✅ aligns: mandatory sections unchanged)
 - .specify/templates/tasks-template.md (✅ aligns: TDD ordering matches Principles)
Follow-up TODOs: TODO(RATIFICATION_DATE) if historical ratification predates 2025-09-21; assumed today as initial adoption.
-->

# SideSpins Constitution

## Core Principles

### 1. Spec-First, Incremental Delivery (NON-NEGOTIABLE)
Every new feature or material change MUST begin with a human-reviewed specification in `specs/[###-feature]/spec.md` before implementation. The spec captures user value, scenarios, and testable functional requirements; it MUST exclude solution-level code details. Plans, tasks, and implementation derive strictly from the spec. Changes discovered mid‑build require spec + plan updates before code divergence. Rationale: Prevents parallel ad-hoc code paths, enforces shared understanding, and reduces rework.

### 2. Architectural Cohesion & Reuse Over Divergence
New functionality MUST integrate with existing services, models, and patterns unless a documented gap is proven (Complexity Tracking). Prohibited without justification: duplicating data models, creating parallel API endpoints with overlapping semantics, or adding alternative auth flows. Shared logic gravitates to the .NET Azure Functions backend or shared JS utilities (for frontend behavior) instead of copy-paste duplication. Rationale: Minimizes maintenance surface and regression risk.

### 3. Simplicity & Mobile-First UX
The public site (Jekyll + HTML/JS) MUST remain lightweight: fast first paint, minimal blocking JS, progressive enhancement (core flows usable without advanced scripting), and layouts optimized for small screens first. Avoid framework bloat. Interfaces MUST present clear, low-friction actions for a broad APA player skill spectrum; hidden complexity is not shifted to users. Rationale: Majority of users are on mobile; simplicity accelerates adoption and reduces support burden.

### 4. Security, Auth Discipline & Least Privilege (NON-NEGOTIABLE)
End-user operations MUST use JWT-based authentication validated by middleware (`AuthenticationMiddleware.cs`) and annotated with `[RequiresUserAuth]` where user identity matters. The `x-api-secret` header is ONLY permitted for controlled automation tooling, seeding scripts, or internal maintenance endpoints marked with `[RequiresApiSecret]`. No endpoint may simultaneously permit both without deliberate review. Sensitive operations MUST log audit-relevant events without leaking secrets. Rationale: Clear separation prevents accidental exposure and enforces consistent security posture.

### 5. Data Integrity, Observability & Test Enforcement
All persisted model changes MUST maintain referential integrity and adhere to documented skill cap rules (e.g., 23‑point APA lineup constraint) on both backend validation and (mirrored) frontend checks. New APIs REQUIRE accompanying contract tests before implementation. Critical business rules (e.g., lineup validation) REQUIRE integration tests. Logging MUST be structured (Application Insights correlation) and sufficient to reconstruct user-impacting events. Rationale: Ensures trust, rapid incident diagnosis, and regression containment.

### 6. Versioning, Backward Compatibility & Controlled Change
Breaking changes to API contracts, authentication flows, or data schemas REQUIRE a migration strategy plus MINOR or MAJOR version bump of this constitution’s version reference in affected specs/plans. Clarifications that do not alter behavior are PATCH updates. Deprecations MUST announce a sunset timeline and maintain compatibility until removed in a scheduled MAJOR change window. Rationale: Predictable evolution and ecosystem stability.

## Architecture & Technology Constraints

1. Frontend: Jekyll static site with vanilla HTML, CSS (Sass where present), and targeted JavaScript modules—avoid introducing heavy SPA frameworks unless a quantified performance/usability gap is proven.
2. Backend: .NET 8 Azure Functions with strict attribute-based auth gates. All new endpoints follow existing REST naming conventions (`/api/{plural}`) and JSON camelCase serialization.
3. Database: Azure Cosmos DB containers follow established partition key strategy; new containers require justification (throughput, access pattern, or isolation) in the feature spec.
4. Auth Provider: Stytch flows remain authoritative for user identity; any expansion (MFA, passwordless variants) enters via spec + plan.
5. Performance Expectations: Mobile initial interactive target < 3s on median 4G; backend p95 for standard CRUD < 250ms excluding network latency.
6. Tooling Secrets: Seeding or migration scripts MUST isolate use of `x-api-secret`; never embed JWT tokens in tooling artifacts.
7. Accessibility: Semantic HTML preferred; interactive elements MUST be keyboard accessible and labeled.

## Development Workflow & Quality Gates

1. Lifecycle: Spec → Plan (`plan.md`) → Design artifacts (data-model, contracts, quickstart) → Tasks (`tasks.md`) → Implementation → Validation.
2. Constitution Checks: Performed at initial plan creation and post-design before tasks generation; violations require documented justification in Complexity Tracking.
3. Testing Order: Contract & integration tests added first and MUST fail before implementation.
4. Duplicate Prevention: PR reviewers MUST challenge parallel code paths or model duplication; refusal requires explicit Complexity Tracking entry.
5. Security Review: Any auth scope change or new privileged endpoint requires reviewer with backend auth familiarity.
6. Observability: New endpoints MUST emit at least: request correlation id, principal (anonymized where needed), outcome (success/failure), latency classification.
7. Deployment Safety: Feature flags or guarded rollouts used for behavior that modifies core lineup calculations or scoring logic.
8. Documentation Sync: Changes to principles, auth usage, or lineup logic require simultaneous doc updates (`README.md` or relevant `/docs/` pages) in the same PR.
9. Performance Gate: If added JS bundle size increase > 30KB (min+gzip) or added p95 latency > 15% for target endpoint, reviewer may request optimization before merge.
10. Definition of Done: All gates passed, tests green, no unresolved `[NEEDS CLARIFICATION]`, docs updated, no unauthorized `x-api-secret` usage.

## Governance

1. Authority & Scope: This constitution supersedes informal practices; conflicts resolved in favor of the stricter requirement.
2. Amendment Process: Proposed edits land as PR modifying this file plus Sync Impact Report. Version bump determined by semantic impact (see Principle 6).
3. Versioning Policy: `MAJOR.MINOR.PATCH` where:
	- MAJOR: Removal/redefinition of a principle or governance rule.
	- MINOR: Addition of a principle/section or materially expanded guidance.
	- PATCH: Non-semantic clarifications or typo/style fixes.
4. Ratification: Initial adoption occurs upon merge of v1.0.0 to `main` (date recorded below). Historical earlier ratification may be backfilled if evidence emerges.
5. Compliance Reviews: Each feature plan & PR MUST explicitly reference the constitution version validated. Automated tooling may enforce by scanning plan/task docs.
6. Deviation Handling: Any exception recorded in Complexity Tracking with sunset/remediation date; unresolved deviations re-reviewed quarterly.
7. Archival & Traceability: Prior versions retained in Git history; no force-push altering ratified tags.
8. Security Escalations: Emergency hotfixes may temporarily bypass spec-first only to restore availability/security; retroactive spec & test backfill required within 72 hours.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): Use actual merge date to main | **Last Amended**: 2025-09-21
