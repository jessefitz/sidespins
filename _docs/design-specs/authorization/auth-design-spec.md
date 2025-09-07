# SideSpins — AuthN/AuthZ & Team-Context (Phase 1)


## 1) Objectives

* Authenticate with **Stytch SMS OTP** only after confirming the user exists in **Players** by APA member number.
* Keep **JWT lean** (identity + timestamps only).
* Authorize **per request** by resolving membership/role from **TeamMemberships** (no per-team roles in JWT).
* UI is always scoped to a **single active team** via a header team switcher.
* Prove out authZ with conditional UI (e.g., “Manage lineup” visible to captains/managers only).

---

## 2) Data model (Cosmos containers)

* **Players** (pk `/id`): `id`, `apaNumber`, `firstName`, `lastName`, `phoneNumber?`, …
* **TeamMemberships** (pk `/teamId`): `id`, `playerId`, `teamId`, `divisionId`, `role` (`player|manager|admin`), `joinedAt`, `leftAt|null`, `active_flag` (derived from `leftAt==null`).

> Note: You already have `TeamMemberships`; we’ll use that exact name everywhere (not “UserTeamMemberships”).

---

## 3) Sign-up / onboarding flow (APA-first, then Stytch)

### UX (client)

1. User enters **Phone** and **APA member number**.
2. Client calls `POST /auth/signup/init`.

### API behavior

1. **Validate APA number**

   * Lookup `Players` by `apaNumber`.
   * If not found → return `409` with message “We couldn’t find that APA member number. Contact your captain.”
2. **Reconcile phone**

   * If `player.phoneNumber` missing or different, **update** the Players doc with the supplied phone (server-side).
3. **Resolve memberships**

   * Query `TeamMemberships` for `playerId` where `leftAt == null` (or `active_flag == true`) to build the initial list for UX.
4. **Gate Stytch**

   * Only if the player exists proceed to **Stytch SMS**: create/verify OTP (loginOrCreate) for that phone.
5. **Issue App JWT**

   * After Stytch session is validated, mint App JWT with **identity only**:

     * `sub` (internal user id you use to correlate with Players/playerId)
     * `iat`, `exp`
     * Optional: `ver` (token schema), `jti` (replay)
   * Return memberships along with token for initial UI state.

**Response** (example):

```json
{
  "token": "<app_jwt>",
  "memberships": [
    { "teamId": "team_sidespins", "divisionId": "div_baltimore_9b_fall2025", "role": "manager" },
    { "teamId": "team_cue_masters", "divisionId": "div_baltimore_9b_fall2025", "role": "player" }
  ],
  "profile": { "playerId": "p_ava", "firstName": "Ava", "lastName": "Nguyen" }
}
```

---

## 4) Authentication (runtime)

* **App JWT** only (short-lived; e.g., 30–60 min).
* Stored preferably in **HttpOnly cookie**; `Authorization: Bearer` also supported.
* **No team roles in JWT.**
* Optionally keep a **global role** claim (`global_role=admin`) for platform admins (rare).

**JWT claims (minimal)**

```json
{
  "sub": "u_12345",
  "iat": 1693916400,
  "exp": 1693920000,
  "ver": 1
}
```

---

## 5) Authorization & team context

### UX: Header team switcher

* Visible on all authenticated, non-admin pages.
* Lists teams from `/me/memberships`.
* Selecting a team sets the **active team** in client state and triggers re-fetch of team-scoped data.
* Persist last selection in `localStorage` for UX; server remains source of truth.

### UI conditional rendering

* If active membership role ≥ `manager`, show **“Manage lineup”** link and icon in the schedule.
* Otherwise hide (or disabled with tooltip “Captain only”).

---

## 6) API surface (Phase 1)

* `GET /me/memberships` → list `{teamId, divisionId, role}` for the authenticated user (resolved from TeamMemberships).
* `GET /teams/{teamId}/matches[?from=&to=&page=]` → requires membership in `{teamId}` (role ≥ `player`).
* `GET /teams/{teamId}/lineups/{teamMatchId}` → requires membership (role ≥ `player`).
* `PATCH /teams/{teamId}/lineups/{teamMatchId}` → requires membership (role ≥ `manager`).

> Always put `teamId` in the **route** for team-scoped endpoints so middleware can enforce policy centrally.

---

## 7) Middleware design (updated)

### Keep

* **`RequireAuthentication`** attribute: indicates the function needs a valid App JWT.
* JWT extraction from `Authorization` header or cookies.
* 401 vs 403 mapping.

### Add (new)

* **`RequireTeamRole`** attribute for team-scoped policy via membership lookup.

  ```csharp
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class RequireTeamRoleAttribute : Attribute
  {
      public string MinimumRole { get; }
      public string TeamIdRouteParam { get; }

      public RequireTeamRoleAttribute(string minimumRole, string teamIdRouteParam = "teamId")
      {
          MinimumRole = minimumRole;
          TeamIdRouteParam = teamIdRouteParam;
      }
  }
  ```

### Membership service (DI)

```csharp
public interface IMembershipService
{
    Task<UserTeamMembership?> GetAsync(string userId, string teamId, CancellationToken ct = default);
}
public sealed record UserTeamMembership(string UserId, string TeamId, string Role, bool Active);
```

> Implementation queries **TeamMemberships** where `playerId/userId` + `teamId` and `leftAt == null` (or `active_flag==true`). (Map `userId`→`playerId` per your identity model.)

### Middleware logic changes (high-level)

1. **Discover attributes** on the function method:

   * `RequireAuthentication` (boolean)
   * `RequireTeamRole` (optional; capture `MinimumRole`, `TeamIdRouteParam`)
2. **Validate JWT**:

   * If missing/invalid → **401**.
   * Store only identity in `context.Items["UserId"] = claims.Sub`.
   * Do **not** inject `TeamId`/`TeamRole` from claims anymore.
3. **If `RequireTeamRole` present**:

   * Extract `teamId` from route (`TeamIdRouteParam`, default `teamId`).
   * Optional global admin bypass if `global_role` claim is present and sufficient.
   * Call `_membershipService.GetAsync(userId, teamId)`.
   * If null/inactive → **403**.
   * If role rank < minimum → **403**.
   * Put resolved membership into `context.Items["ActiveMembership"]` for handlers.
4. **Invoke next**.

**Role ranking helper** (server-side, used by middleware):

```csharp
private static bool IsAtLeast(string? userRole, string requiredRole)
{
    var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["player"] = 1,
        ["manager"] = 2, // use "captain" if that’s your canonical label
        ["admin"] = 3
    };
    return rank.GetValueOrDefault(userRole ?? "", 0) >= rank.GetValueOrDefault(requiredRole, int.MaxValue);
}
```

### Registration (Program.cs / DI)

* Register `AuthService`, `IMembershipService` (Cosmos-backed), and this middleware:

  ```csharp
  builder.Services.AddSingleton<AuthService>();
  builder.Services.AddSingleton<IMembershipService, CosmosMembershipService>();
  builder.UseMiddleware<AuthenticationMiddleware>();
  ```

---

## 8) Function usage pattern

```csharp
[Function("GetTeamMatches")]
[RequireAuthentication]
[RequireTeamRole("player")] // must belong to {teamId}
public async Task<IActionResult> GetTeamMatches(
  [HttpTrigger(AuthorizationLevel.Anonymous, "get",
     Route = "teams/{teamId}/matches")] HttpRequest req,
  FunctionContext ctx, string teamId)
{
    // Optionally read resolved membership:
    var m = (UserTeamMembership?)ctx.Items["ActiveMembership"];
    // …fetch matches for teamId and return
}

[Function("UpdateLineup")]
[RequireAuthentication]
[RequireTeamRole("manager")] // captain/manager+ for {teamId}
public async Task<IActionResult> UpdateLineup(
  [HttpTrigger(AuthorizationLevel.Anonymous, "patch",
     Route = "teams/{teamId}/lineups/{teamMatchId}")] HttpRequest req,
  FunctionContext ctx, string teamId, string teamMatchId)
{
    var payload = await req.ReadFromJsonAsync<UpdateLineupDto>();
    var updated = await _lineupSvc.UpdateAsync(teamId, teamMatchId, payload, ctx.Items["UserId"]?.ToString()!);
    return new OkObjectResult(updated);
}
```

---

## 9) Client integration (quick notes)

* On first load after login, call **`/me/memberships`** to hydrate the team switcher.
* Keep `activeTeamId` in client state; always fetch data with routes containing `{activeTeamId}`.
* Conditional render “Manage lineup” if client-side role for `activeTeamId` is ≥ `manager` (UI nicety).
  **Server is the source of truth** (middleware enforces role).

---

## 10) Errors & telemetry

* **401 Unauthorized** — missing/invalid/expired JWT.
* **403 Forbidden** — not a member of team OR insufficient role.
* Log structured events on 403 with: `userId`, `teamId`, `endpoint`, `requiredRole`, `resolvedRole/null`.
* Audit write operations (who/when/what changed).

---

## 11) Test plan (Phase 1)

### Backend

* JWT required: protected endpoints w/o token → 401.
* Membership required: non-member hitting team route → 403.
* Role gate: `player` attempting lineup update → 403; `manager` → 200.
* Lost access mid-session: membership removed → next request → 403.

### Frontend

* Team switcher shows only teams from `/me/memberships`.
* Switching team updates data; “Manage lineup” appears/disappears accordingly.
* On 403 from actions, show inline banner and revert to next available team if needed.

---

## 12) Future phases

* **Redis** cache for membership lookups.
* **Short-lived team-context tokens** (optional optimization).
* **Granular permissions** beyond roles if needed.
* **Admin** global views/impersonation tools.

---

### Summary

This spec keeps identities stable and tokens small, performs **authoritative** authorization against `TeamMemberships` on every team-scoped call, and gives you a clean, reusable **middleware-based policy** (`RequireTeamRole`) so handlers stay tidy. The APA-first onboarding guarantees we only create sessions for real league members and keeps your Players data consistent (phone reconciliation).
