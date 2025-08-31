# High-Level Architecture: Jekyll → Azure Functions (CORS) → Cosmos DB

```
Browser (Jekyll on GitHub Pages)
        │  HTTPS (fetch)
        ▼
Azure Function App (HTTP-triggered API)  ←→  App Insights (telemetry)
        │  Cosmos SDK / REST + auth
        ▼
Azure Cosmos DB (SQL/Core API)
```

## Components

* **Jekyll site (static, GitHub Pages):** Renders UI, makes `fetch()` calls to your Function endpoints.
* **Azure Function App (HTTP triggers):** Thin API layer; enforces auth, validation, shaping, and rate limits. CORS enabled for your Pages domain.
* **Cosmos DB (SQL/Core):** Stores application data; accessed only by the Function using secure credentials.
* **Key Vault (recommended):** Stores Cosmos keys/connection strings; Function gets them via managed identity.
* **Application Insights:** Logs, traces, metrics for the Function.

## Data Flow (Request Lifecycle)

1. **User action in browser** → `fetch https://<function>.azurewebsites.net/api/<route>?q=...`
2. **CORS preflight** (if needed): Function App returns `Access-Control-Allow-Origin: https://<username>.github.io`.
3. **Function executes:**

   * Validates input, enforces auth/quotas.
   * Queries/updates Cosmos via SDK (preferred) or REST.
   * Shapes/filters results for the client.
4. **Response** → JSON payload to browser.

## Security & Access

* **CORS:** Allow only `https://<username>.github.io` (avoid `*` in production).
* **Secrets:** Store in **Key Vault**; access via **Managed Identity**. No secrets in client code.
* **Cosmos auth:** Use **primary key** (server-side only) or **RBAC/AAD** if available in your setup.
* **Client auth (optional):** Azure AD / Entra ID, GitHub OIDC, or simple Function keys for internal tools.

## Function API Design (example)

* `GET  /api/items?limit=50&continuationToken=...` – list/paginate
* `GET  /api/items/{id}` – retrieve
* `POST /api/items` – create (validate input)
* `PATCH/PUT /api/items/{id}` – update
* `DELETE /api/items/{id}` – delete
  Return compact JSON and include pagination tokens from Cosmos.

## Deployment & Config

* **Infra as Code:** Bicep/ARM/Terraform for Function, Cosmos, Key Vault, MI, Insights.
* **CI/CD:** GitHub Actions deploying Function (zip deploy/func deploy); Pages builds Jekyll separately.
* **App Settings (Function):**

  * `COSMOS_DB_ACCOUNT/DB/CONTAINER`
  * `KEY_VAULT_URI` (if using)
  * Any feature flags, rate limits, and CORS origins (can also be set in portal/CLI).

## Operational Concerns

* **Observability:** App Insights traces per request; log Cosmos RU charges for cost awareness.
* **Throughput & RU mgmt:** Pick fixed or autoscale RU; use partition keys wisely; project selective fields.
* **Performance:** Prefer Cosmos SDK; use parameterized queries, continuation tokens; cache hot reads if needed.
* **Error handling:** Normalize error shapes; map Cosmos errors to 4xx/5xx; include correlation IDs.
* **Limits & Abuse:** Per-IP/user throttling in the Function; validate payload sizes.

## Why not call Cosmos directly from Jekyll?

* Cosmos REST requires HMAC signatures with account keys; exposing those in the browser is insecure.
* Cosmos REST does not support custom CORS for public browser origins; the **Function layer** solves both.

## Optional Enhancements

* **Resource tokens flow:** Function mints scoped, time-limited tokens for limited client-side access.
* **API Management in front:** Centralized auth, throttling, and developer portal.
* **Caching layer:** Azure CDN/Front Door or in-Function memory/Redis for read-heavy endpoints.

