# Authorization System Implementation Plan

## Overview

This implementation plan covers the transformation from the current monolithic authentication system to a flexible, **team-scoped authorization** system as outlined in the authorization design specification.

---

## Phase 1: Backend Infrastructure & Data Models

### Step 1.1: Update Core Data Models

**Files to modify**

* `AuthModels.cs`
* `LeagueModels.cs`

**Changes needed**

* **Player model**: add `PhoneNumber` property to support phone reconciliation.
* **AppClaims model**: remove team-specific claims; keep only `UserId` and `SidespinsRole` (from Stytch trusted metadata).
* **New models**: create `UserTeamMembership` record for middleware use.
* **Signup models**: add `SignupInitRequest` and `SignupInitResponse` for APA-first flow.
* **Stytch models**: update `StytchUserTrustedMetadata` to remove `teams` section; keep only `sidespins_role`.

---

### Step 1.2: Create Membership Service

**New files to create**

* `functions/auth/IMembershipService.cs` — interface definition
* `functions/auth/CosmosMembershipService.cs` — Cosmos implementation

**Purpose**

* Query team memberships efficiently.
* Convert full **TeamMemberships** entities to lightweight `UserTeamMembership` records.
* Provide a clean abstraction for authorization middleware.
* Include placeholder methods for future caching implementation.

---

### Step 1.3: Create Team Role Authorization Attribute

**New file to create**

* `functions/auth/RequireTeamRoleAttribute.cs`

**Features**

* Support for minimum role requirements (`"player"`, `"captain"`, `"admin"`).
* Configurable team ID route parameter extraction (default: `teamId`).
* Clean separation from authentication concerns.

---

## Phase 2: Authentication & JWT Modernization

### Step 2.1: Update AuthService for Lean JWTs

**File to modify**

* `AuthService.cs`

**Key changes**

* **JWT generation**: remove all team-specific claims; include only:

  * `sub` (user ID)
  * `sidespinsRole` (from Stytch trusted metadata: `"admin"` or `"member"`)
  * `iat`, `exp` (timestamps)
  * Optional: `ver`, `jti`
* **JWT validation**: update to expect lean format.
* **Stytch integration**: extract `sidespins_role` from trusted metadata.

---

### Step 2.2: Implement APA-First Signup Flow

**File to modify**

* `AuthFunctions.cs`

**New endpoint**

* `POST /auth/signup/init`

**Flow logic**

1. **Validate APA number** exists in `Players` collection (exact match required).
2. **Reconcile phone number** (E.164 format; do not overwrite existing).
3. **Query active memberships** for initial UI state.
4. **Proceed to Stytch SMS OTP** only if player exists.
5. **Return** lean JWT + memberships list.

**Error handling**

* APA number not found → **409** with helpful message.
* Invalid phone format → **400** with format requirements.
* Stytch failures → return appropriate error codes.

---

### Step 2.3: Overhaul Authentication Middleware

**File to modify**

* `AuthenticationMiddleware.cs`

**Major changes**

* **Attribute detection**: support both `RequireAuthentication` and `RequireTeamRole`.
* **JWT processing**: extract only identity claims; no team context in claims.
* **Team authorization**:

  * Extract `teamId` from route parameters.
  * Query membership via `IMembershipService`.
  * Validate role hierarchy (`player = 1`, `captain = 2`, `admin = 3`).
* **Context storage**: store resolved membership in `context.Items`.
* **Error responses**: proper **401** vs **403** distinction with structured logging.

---

## Phase 3: Function Updates & New Endpoints

### Step 3.1: Update Existing Function Attributes

**Files to modify**

* `TeamsFunctions.cs`
* `PlayersFunctions.cs`
* `MatchesFunctions.cs`
* `MembershipsFunctions.cs`

**Attribute migration rules**

* **Read operations**: `[RequireAuthentication("member")]` for platform member access.
* **Write operations**: `[RequireTeamRole("captain")]` for team-scoped updates.
* **Routes**: ensure team-scoped endpoints include `{teamId}` parameter.

---

### Step 3.2: Create Membership Management Functions

**New file to create**

* `functions/auth/MembershipFunctions.cs`

**New endpoints**

* `GET /me/memberships` — list user’s active team memberships (primary endpoint for team switcher).
* `GET /me/profile` — user profile with platform role and memberships.

---

### Step 3.3: Update Dependency Injection

**File to modify**

* `Program.cs`

**Changes**

* Register `IMembershipService` and `CosmosMembershipService`.
* Ensure proper service scoping.
* Verify middleware registration order.

---

## Phase 4: Frontend Implementation

### Step 4.1: Update Signup Flow UI

**Files to modify**

* `login.html` (or create new signup page)
* `auth.js`

**Changes needed**

* **New signup form**

  * APA Number input field (**required**).
  * Phone Number input field (E.164 format with validation).
  * Clear messaging about APA verification requirement.
* **API integration**

  * Call `POST /auth/signup/init` instead of direct Stytch.
  * Handle APA validation errors with user-friendly messages.
  * Process returned memberships for initial team selection.
* **Error handling**

  * APA number not found → display “contact captain” message.
  * Phone format errors → show format examples.
  * Network errors → provide retry options.

---

### Step 4.2: Implement Team Switcher Header

**Files to modify/create**

* Update main app layout (likely in `app.html`).
* `auth.js` for team switching logic.

**Components needed**

* **Header Team Selector**

  * Dropdown showing active team memberships.
  * Display team name and role for each option.
  * Persist selection in `localStorage`.
  * Update on membership changes.
* **Team Context Management**

  * Global state for `activeTeamId`.
  * Re-fetch team-scoped data on team switch.
  * Update all API calls to include team context.
* **Initial Load Logic**

  * Call `/me/memberships` on app startup.
  * Set default team from `localStorage` or first available.
  * Handle empty memberships gracefully.

---

### Step 4.3: Implement Conditional UI Based on Roles

**Files to modify**

* All pages with team-specific actions (schedule, lineup management, etc.).
* `auth.js` for role checking utilities.

**Features to implement**

* **Role-based visibility**

  * Show **“Manage Lineup”** button only for `captain+` roles.
  * Hide/disable team management features for regular players.
  * Add tooltips explaining permission requirements.
* **Client-side utilities**

  * `hasRole(teamId, minimumRole)` helper function.
  * `isTeamCaptain(teamId)` convenience method.
  * Role hierarchy checking matching backend logic.
* **Real-time updates**

  * Handle **403** responses gracefully.
  * Refresh memberships on authorization failures.
  * Auto-switch teams if current team access is lost.

---

### Step 4.4: Update Authentication Flow

**Files to modify**

* `auth.js`
* Authentication callback pages

**Changes needed**

* **Token management**

  * Store lean JWTs (identity only).
  * Remove team-specific token claims processing.
  * Implement token refresh logic for shorter expiry.
* **Session management**

  * Call `/me/memberships` after successful authentication.
  * Initialize team switcher state.
  * Handle logout and cleanup.
* **API requests**

  * Update all team-scoped API calls to use selected team context.
  * Add `teamId` to route parameters as required.
  * Handle authorization errors with team switching fallback.

---

## Phase 5: Testing & Validation

### Step 5.1: Backend Testing

* **Unit tests**

  * Membership service queries and filtering.
  * Role hierarchy validation logic.
  * JWT validation with lean format.
  * Middleware authorization decisions.
* **Integration tests**

  * APA-first signup flow end-to-end.
  * Cross-team access attempts (should fail).
  * Role escalation attempts (should fail).
  * Token expiry and refresh handling.
* **Performance tests**

  * Membership lookup performance under load.
  * Database query optimization validation.

---

### Step 5.2: Frontend Testing

* **Signup flow**

  * Valid APA number with existing phone.
  * Valid APA number with new phone.
  * Invalid APA number handling.
  * Phone format validation.
* **Team switching**

  * Switch between teams with different roles.
  * Team selection persistence.
  * Data refresh on team change.
* **Authorization UX**

  * Role-based UI visibility.
  * Graceful handling of 403 errors.
  * Team switcher updates on permission changes.

---

### Step 5.3: Migration Testing

* **Backward compatibility**

  * Existing tokens continue to work during transition.
  * Old API endpoints remain functional.
  * Gradual function migration without downtime.
* **Data integrity**

  * Phone number reconciliation accuracy.
  * Membership data consistency.
  * No data loss during migration.

---

## Phase 6: Deployment & Migration Strategy

### Step 6.1: Backend Deployment

**Deployment sequence**

* Deploy new services and middleware with backward compatibility.
* Migrate functions incrementally (start with read-only endpoints).
* Update high-traffic endpoints last.
* Remove deprecated authentication patterns after full migration.

---

### Step 6.2: Frontend Deployment

**Rollout approach**

* Deploy new signup flow behind feature flag.
* Gradually enable team switcher for user cohorts.
* Monitor error rates and user feedback.
* Full rollout after validation period.

---

### Step 6.3: Monitoring & Rollback

**Monitoring setup**

* **Key metrics**

  * Authentication success/failure rates.
  * Authorization decision latency.
  * Team switching frequency.
  * API error rates by endpoint.
* **Alerts**

  * Unusual authorization failure patterns.
  * JWT validation errors.
  * Membership service performance issues.
* **Rollback plan**

  * Feature flags for new authorization logic.
  * Database rollback scripts if needed.
  * Client-side fallback to old authentication flow.

---

## Success Criteria

### Technical Objectives

* [ ] Lean JWTs with **only identity claims** implemented.
* [ ] **Team-scoped authorization** working for all endpoints.
* [ ] **APA-first signup flow** functional.
* [ ] **Phone number reconciliation** working.
* [ ] **Team switcher UI** implemented and tested.

### User Experience Goals

* Smooth signup experience with clear error messages.
* Intuitive team switching with role-based UI.
* No functionality loss during migration.
* Improved security with proper authorization.

### Performance Targets

* Authorization decisions **< 100 ms**.
* Team membership queries optimized.
* UI response time maintained or improved.
* Database query efficiency validated.

---

*This implementation plan provides a roadmap for transforming the authorization system while maintaining system stability and user experience. Each phase builds on the previous one, allowing for incremental validation and rollback capabilities.*
