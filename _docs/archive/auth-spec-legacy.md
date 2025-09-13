# SideSpins Auth: High-Level Implementation Spec (Stytch Consumer + Azure Functions)

## 0) Summary

Introduce passwordless sign-in for SideSpins using **Stytch (consumer/B2C)** with **Email Magic Links** and **SMS Codes**, issuing an **app session cookie** from our Azure Functions. Authorization uses **trusted metadata** on the Stytch user to embed `team_id` and `team_role` into our app session for fast checks. Jekyll remains the public front-end.

Stytch provides a vanilla [javascript sdk](https://www.npmjs.com/package/@stytch/vanilla-js) that should be considered for ease of development.  Where possible, use pre-built UI components to eliminate the need for custom development.  

Our .NET backend will use, when necessary, the Stytch API endpoints. A sample [postman collection](/_docs/stytch-postman-collection.json) has been included for reference.

---

## 1) Scope & Goals

* **Auth methods**: SMS one-time code.
* **Session model**: Client obtains `session_jwt` from Stytch → backend verifies → backend issues `ssid` HttpOnly cookie.
* **Authorization**: Enforce **team-scoped** access and **role-based** permissions using claims from Stytch **trusted metadata**.
* **MVP roles**: `manager`, `player`, `admin`.
* **Team enforcement**: Manager can manage only their **own team** (e.g., `break_of_dawn_9b`), team size limit ≤ 8.

Out of scope for MVP: UI polish, email/SMS branding, self-service role elevation, admin analytics.

---

## 2) Architecture

* **Front-end**: Jekyll (static) + Stytch JS SDK.
* **Backend**: Azure Functions (HTTP triggers, .NET 8).
* **Identity**: Stytch consumer product (users, sessions, magic links, SMS codes).
* **Data**: Cosmos DB (existing SideSpins schema).
* **Secrets/Keys**: Azure Key Vault (Stytch secret, JWT signing key).
* **Observability**: App Insights (auth success/fail, rate limits).

---

## 3) Data & Claims

### Stytch user `trusted_metadata` (authoritative for app claims)

```json
{
  "trusted_metadata": {
    "teams": {
      "team": {
        "team_id": "break_of_dawn_9b",
        "team_role": "manager"
      }
    }
  }
}
```

> Note: design for future multi-team by evolving to an array `teams: [{ team_id, team_role }]`.

### App session (our JWT claims)

* `sub` (stytch user id)
* `team_id`
* `team_role` (`manager` | `player` | `admin`)
* `iat`, `exp` (e.g., 60 min)
* Optional: `amr` (auth method), `sid` (session id)

---

## 4) User Flows



### 4.2 SMS code

1. User enters E.164 phone → **Stytch JS** `otps.sms.loginOrCreate`.
2. User inputs code → **Stytch JS** `otps.sms.authenticate` → `session_jwt`.
3. Same `POST /auth/session` exchange → cookie set → `/app/`.

### 4.3 Logout

* `POST /auth/logout`: revoke Stytch session (optional) + clear `ssid` cookie.

### 4.4 Refresh (optional)

* `POST /auth/refresh`: if Stytch session valid, mint fresh `ssid` (sliding window).

---

## 5) Backend Endpoints (Azure Functions)

1. `POST /auth/session`
   **Body**: `{ "session_jwt": "<from Stytch>" }`
   **Action**:

   * Verify Stytch session (`/sessions/authenticate` via SDK/API).
   * Get user → read `trusted_metadata.teams.team`.
   * Mint app JWT with `sub`, `team_id`, `team_role` (60 min).
   * `Set-Cookie: ssid=<jwt>; HttpOnly; Secure; SameSite=Lax; Path=/`
   * Response `{ ok: true }`.

2. `POST /auth/logout`

   * Optional call to Stytch to revoke session.
   * Overwrite `ssid` with `Max-Age=0`.

3. `POST /auth/refresh` (optional)

   * Validate current `ssid`, validate Stytch session (or accept within grace).
   * Re-mint `ssid` with extended expiry.

4. **Protected team routes** (examples):

   * `POST /teams/{teamId}/members` (add member; **manager-only**)
   * `GET /teams/{teamId}/members` (list members; **manager/player**)
   * `DELETE /teams/{teamId}/members/{playerId}` (**manager-only**)

   **Authorization middleware** (pseudocode):

   ```
   extract ssid cookie → validate app JWT
   require team_id == {teamId} route param
   require team_role == "manager" for write ops
   optional: before add, check team size ≤ 8 in Cosmos
   ```

---

## 6) Front-End Integration (Jekyll)

* Include Stytch JS (`<script src="https://js.stytch.com/stytch.js"></script>`).
* **Auth Form**: build or use stytch pre-built.  email + button (magic link), phone + buttons (send code, verify code).
* **Callback Page**: parse Stytch token, exchange for `session_jwt`, call `/auth/session`, then redirect to `/app/`.
* All `fetch()` calls to API use `credentials: 'include'`.
* Minimal state: rely on cookie; do not store tokens in localStorage.

---

## 7) Security & Compliance

* **Cookies**: `HttpOnly; Secure; SameSite=Lax; Path=/` (use `SameSite=None` if cross-site is required).
* **CORS**: Function App allows only `https://<your-domain>` origins; allow credentials.
* **Secrets**: Stytch server secret + JWT signing key in Key Vault; Functions access via Managed Identity.
* **Rate limiting**: per IP/email/phone on `/auth/*`; backoff on repeated failures.
* **PII**: Do not log emails/phones or raw tokens. Log correlation IDs and coarse status only.
* **Replay/single-use**: Stytch handles link/code validity; our app JWT is short-lived.

---

## 8) Error Handling & UX

* `/auth/request` actions (Stytch JS) always return generic success message (“Check your email/text”) to avoid account enumeration.
* Show progressive disclosure for SMS verification input only after code sent.
* Handle expired/used link or wrong code with a friendly retry path.

---

## 9) Observability

* **App Insights** custom events: `auth_request`, `auth_success`, `auth_fail`, `jwt_issue`, `jwt_refresh`, `logout`.
* Dimensions: method (email/sms), reason (expired, invalid), ip hash, ua category.
* Alerts: burst of failures from same IP, high SMS send failure rate.

---

## 10) Configuration

* **Stytch**: public token (front-end), secret (backend), magic link redirect URL (`/auth/callback.html`), SMS sender.
* **Function App settings**:

  * `STYTCH_PROJECT_ID`, `STYTCH_SECRET`, `STYTCH_ENV`
  * `JWT_SIGNING_KEY_ID` (Key Vault)
  * `ALLOWED_ORIGINS` (comma-sep)
* **Domains**: ensure HTTPS on both Jekyll and Function domains; consider subdomain for API to simplify cookies/CORS.

---

## 11) Acceptance Criteria (MVP)

* [ ] Email magic link login completes and sets `ssid` cookie.
* [ ] SMS code login completes and sets `ssid` cookie.
* [ ] Protected endpoints reject requests without valid `ssid`.
* [ ] Manager can **add/list/remove** members for **their** `team_id` only.
* [ ] Player can **view** membership for **their** `team_id` but cannot modify.
* [ ] Team size capped at **8** on add.
* [ ] Logout clears cookie; subsequent calls are `401`.
* [ ] Basic rate limiting on auth requests.

---

## 12) Phase-Next Enhancements (not required now)

* Multi-team support in trusted metadata + claim embedding.
* Admin web UI for role/membership changes.
* On-demand claim refresh (pull Stytch user to re-mint `ssid` mid-session).
* Branded emails/SMS; custom domains.
* Passkeys (WebAuthn) as an additional method.

---

## 13) Dev Notes / Rough Pseudocode

**Create Session (backend)**

* Verify Stytch session (`Sessions.Authenticate`).
* `Users.Get` → read `trusted_metadata.teams.team.{team_id, team_role}`.
* `MintAppJwt(team_id, team_role)` → set `ssid` cookie.

**AuthZ middleware (backend)**

* Validate `ssid` → read `team_id`, `team_role`.
* Compare `team_id` to route param; ensure `manager` for writes.

