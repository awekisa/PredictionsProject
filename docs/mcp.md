# Connect an agent to Predictions

Endpoint after release: **https://predictionsproject.onrender.com/mcp**.
Use the backend URL, not the Vercel website or `/api/mcp`. The server uses
Streamable HTTP with the official C# SDK 2.2.0. It supports the initialization
handshake (tested with 2025-11-25) and current discovery (2026-07-28).

## Create separate connections

Sign in to the website and open **Account settings → Agent connections**. Create
separate named tokens for **Codex** and **Hermes**, selecting only the permissions
you want each agent to have. Copy each token once into your local secret store or
process environment. Do not put it in prompts, URLs, committed files or screenshots.
An agent acts as the account that created its token.

| Scope | Capabilities | Additional requirement |
| --- | --- | --- |
| `app:read` | Account identity, tournaments, fixtures, own/public predictions, prediction/football standings | Valid account |
| `predictions:write` | Save/update the token owner's prediction before its deadline | Valid account |
| `admin:read` | Users, all predictions, provider quota, available leagues | Current Admin role |
| `admin:write` | Existing tournament/game management, score updates, league import/backfill/sync, user/prediction deletion | Current Admin role |

Scopes are independent. Grant `app:read` alongside admin scopes if the agent needs
ordinary fixture/standings reads. Tokens never gain permissions after promotion.
Removing Admin blocks subsequent admin calls even with an existing connection.
Deletion of the owner invalidates its connections. There are no role, password,
token-management, SQL or arbitrary HTTP tools.

## Claude (custom connector)

After the reviewed release, add `https://predictionsproject.onrender.com/mcp`
under **Customize → Connectors → Add custom connector** in Claude. Leave the
advanced client credentials empty. Sign in with your normal Predictions account,
review the client and return hostname, select permissions, and allow the connection.
Only current administrators see requested admin permissions. Start with `app:read`;
write scopes stay unchecked until you select them. Ask Claude to call
`get_current_user` and confirm the account and granted scopes.

Claude can use its hosted Client ID Metadata Document or Dynamic Client
Registration. Both use authorization code + S256 PKCE. The callback is exactly
`https://claude.ai/api/mcp/auth_callback`, confirmed against
[Anthropic's authentication documentation](https://claude.com/docs/connectors/building/authentication).
Claude Code's loopback callbacks are outside this hosted-connector change.

OAuth connections appear in **Account settings → Agent connections**, labeled
**OAuth sign-in**, with client name, permissions, created date and last used date.
Revoke there to invalidate both access and refresh tokens. Access lasts one hour;
refresh tokens rotate, with a fixed 90-day connection lifetime. Reusing an old
refresh token revokes that connection. Reconnect to grant different permissions
or after expiry. Refresh never adds a permission.

Claude's connector traffic comes from Anthropic's cloud. No-Origin calls and the
exact `https://claude.ai` Origin are accepted; arbitrary Origins remain blocked.
Anthropic does not document a guaranteed Origin value, so actual client headers
still need confirmation in the post-release smoke test (record only Origin and
status, never credentials). A Claude organization's separate network/proxy allowlist
is managed by its administrator, outside this repository.

### Server configuration and implementation

- `Mcp__OAuth__Issuer`: canonical API origin, default `https://predictionsproject.onrender.com`.
- `Mcp__OAuth__WebsiteOrigin`: consent website origin, default `https://predictions-project.vercel.app`.
- `Mcp__OAuth__AllowedRedirectUris__0`: exact HTTPS callback above; any additional callbacks must be explicitly configured. Wildcards, fragments and user information are rejected.

Origins have no trailing slash. For disposable local testing, set the issuer and
website origin to their respective localhost ports. Keep the deployed frontend's
`VITE_API_URL` pointing to the matching API and its origin in `CorsOrigins`.
No client credentials belong in frontend configuration.

The existing C# MCP SDK 2.2.0 supplies RFC 9728 metadata and the 401 challenge.
Both `/.well-known/oauth-protected-resource` and its `/mcp` suffix work; the
resource is the canonical issuer plus `/mcp`. The API also exposes RFC 8414 metadata
at `/.well-known/oauth-authorization-server`, `/oauth/register`, `/oauth/authorize`
and `/oauth/token`. Issuer/audience values never come from untrusted Host headers,
including behind the TLS-terminating proxy. `resource` is required for authorization,
code exchange and refresh. Only the MCP endpoint accepts the issued access tokens.

Registration supports public (`none`) and confidential (`client_secret_post`,
`client_secret_basic`) clients. Confidential secrets are returned once and hashed
at rest. Client metadata is fetched only over public HTTPS on port 443, without
redirects or proxies, with DNS/socket checks, a five-second timeout and a 64 KiB
limit. Additional advertised metadata grant types do not enable additional grants.
Authorization requests expire after ten minutes, approved codes after two minutes.
Codes, refresh credentials and access credentials are stored only as hashes.
Concurrent exchanges and revocation use database concurrency checks and atomic
transactions. Existing account/role/scope checks govern both connection types.

OAuth endpoints have a shared 120-request/minute limit per API process. PostgreSQL
stores registrations, pending requests and consumed refresh hashes; monitor table
growth and retain consumed hashes for the connection lifetime to detect replay.
`Mcp__Enabled=false` disables OAuth endpoints and metadata along with `/mcp`.
Do not log authorization parameters, token bodies, passwords or credentials.

Protocol references: [current MCP authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization),
[C# SDK authentication implementation at v2.2.0](https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.2.0/src/ModelContextProtocol.AspNetCore/Authentication),
and [Claude's live client metadata](https://claude.ai/oauth/mcp-oauth-client-metadata).

## Codex

[Official Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)
supports a bearer-token environment reference. Add this entry to your Codex MCP
configuration (or use `codex mcp add` with its bearer-token environment option):

```toml
[mcp_servers.predictions]
url = "https://predictionsproject.onrender.com/mcp"
bearer_token_env_var = "PREDICTIONS_CODEX_TOKEN"
startup_timeout_sec = 30
tool_timeout_sec = 60
```

Supply `PREDICTIONS_CODEX_TOKEN` securely to the process that launches Codex. An
export in a terminal does not automatically reach an already-running desktop
application; restart it with that environment available. Keep normal client
approval policies, especially for deletes and result changes. Start a new task
or refresh MCP connections after adding/changing the configuration. Ask the agent
to call `get_current_user` first and verify the expected account and scopes.

## Hermes

[Hermes MCP configuration](https://hermes-agent.nousresearch.com/docs/user-guide/features/mcp)
uses a remote URL and headers. In the active Hermes profile's MCP configuration:

```yaml
mcp_servers:
  predictions:
    url: "https://predictionsproject.onrender.com/mcp"
    headers:
      Authorization: "Bearer ${PREDICTIONS_HERMES_TOKEN}"
    connect_timeout: 30
    timeout: 60
```

Supply `PREDICTIONS_HERMES_TOKEN` securely in that profile/process environment.
Environment interpolation was verified against the installed client; an unset
variable must be fixed before connecting. Run `hermes mcp test predictions`, then
start a new session or use `/reload-mcp`. Hermes exposes names prefixed with
`mcp_predictions_`, for example `mcp_predictions_get_current_user`. Keep ordinary
client approvals enabled; these examples do not auto-approve destructive actions.

## Tool reference

The [user-tool guide](mcp-server.md) covers the eleven ordinary reads and
`save_my_prediction`. The admin additions are:

| Tools | Inputs / effects |
| --- | --- |
| `admin_list_users`, `admin_list_predictions` | `offset`, `limit`; include admin/private data permitted by the website |
| `admin_get_football_status` | No inputs; last known request quota, null when unknown |
| `admin_list_football_leagues` | `offset`, `limit`; available provider competitions |
| `admin_create_tournament`, `admin_update_tournament` | `request: {name}`; update also requires `tournamentId` |
| `admin_delete_tournament` | `tournamentId`; deletes all its games and predictions |
| `admin_create_game`, `admin_update_game` | `tournamentId`, `request: {homeTeam, awayTeam, startTime}`; update also requires `gameId` |
| `admin_delete_game` | `tournamentId`, `gameId`; deletes that game's predictions |
| `admin_set_game_result` | `gameId`, `request: {homeGoals, awayGoals}`; final score after kickoff |
| `admin_sync_game_score` | `gameId`, `request: {homeGoals?, awayGoals?, isFinished, fifaMatchStatus?, fifaMatchTime?}`; applies a supplied live/final score, not a provider fetch |
| `admin_clear_game_result` | `gameId`; clears scores and finished flag |
| `admin_import_league` | `request: {leagueId, season, name}`; creates a new tournament and imports eligible fixtures |
| `admin_backfill_fixtures` | `tournamentId`; links existing provider fixtures and adds missing ones, returns counts |
| `admin_sync_tournament_scores` | `tournamentId`; fetches provider results and returns the updated-game count |
| `admin_delete_user` | Exact `userId`; deletes the account, its predictions and agent tokens |
| `admin_delete_prediction` | `predictionId`; deletes that prediction |

IDs are positive integers except account IDs (opaque strings). Scores are
nonnegative integers; score sync accepts both goals null or both populated,
and final scores require both. Game times require ISO-8601 with an explicit UTC
offset. DTO validation is applied before service calls, including required names.
Unknown fields are rejected. Existing service rules, cascades and standings
calculation apply. Moving kickoff later does not reopen a prediction deadline.

List tools default to 50 rows and cap at 100. Follow `nextOffset` until null.
Admin lists are ordered by user/prediction/league ID. Writes return changed objects,
a deleted target ID, or affected counts. Mutations are never retried by the server.
Creation/import is not idempotent; check state after an uncertain network result.
Tool annotations describe risks but do not grant permission or replace approval.

## Expiry, rotation and revocation

Manually created tokens expire after the selected 7, 30 or 90 days. To rotate, create a new token
with the same required scopes, replace that client's secret environment value,
restart/reconnect and verify identity, then revoke the old connection. Revoke a
lost token immediately in Agent connections. A revoked/expired token fails the
next HTTP request. Already-running work is not rolled back. Tokens cannot manage
other tokens and never authenticate normal website REST routes.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| HTTP 401 | Token missing, expired, revoked, wrong type or owner deleted. Confirm the launching process sees the secret variable and use an agent token, not a website JWT. |
| Scope/role tool error | Recreate the token with the required scope; admin calls also require the owner's current Admin role. Promotion alone does not expand old tokens. |
| HTTP 403 before a tool runs | A supplied Origin is not allowed. Native clients normally omit Origin; browser clients need an exact configured Origin and CORS permission. |
| Unavailable server, 404 or timeout | Check the backend endpoint, deployed release and `Mcp__Enabled`. A sleeping host may need startup time. Inspect status-only server logs; don't automatically retry writes. |
| Tools missing/stale | Refresh/restart the client; check client include/exclude filters and server release. This version advertises 30 tools. Knowing a tool name does not bypass permissions. |
| OAuth login fails | Confirm the OAuth-capable release is deployed, metadata is reachable, issuer/website origins match, and the callback is exact. Restart an expired consent request from Claude. Codex/Hermes manual token configurations do not need an OAuth login. |
| Football provider failure | Check the configured provider credentials/quota through the website or status tool. Errors intentionally omit provider response bodies. |

## Release and emergency disable

Review and merge the PR first. Use the repository's existing release process:
a version tag matching `v*.*.*` runs `.github/workflows/release.yml`, which verifies
the backend/frontend and invokes the configured Render and Vercel deployment hooks.
Do not assume merge or a Vercel preview updates the API. Confirm the actual backend
release and health before connecting production clients.

Render terminates public HTTPS and forwards to the Docker API on port 8080; see
[Render's documented proxy behavior](https://render.com/docs/web-services#connecting-from-the-public-internet).
Use HTTPS directly. No affinity or long-lived GET/SSE session is required. Set
`Mcp__AllowedOrigins__0` to exact allowed browser origins or retain `CorsOrigins`.
Native no-Origin calls and the exact Claude Origin are allowed. Every MCP response has `Cache-Control: no-store`.

To disable only MCP, set **`Mcp__Enabled=false`** in the API environment and restart
or redeploy the service. `/mcp` then returns 404; website JWT routes keep working.
Unset it or set true and restart to restore MCP. This switch does not revoke tokens;
revoke them separately if needed. Structured write audits remain under
`Predictions.Mcp.Audit`. Preserve Information logging for that category, and never
capture Authorization headers or request/response bodies in proxy/APM settings.

After the reviewed release, use a read-only production token to check identity,
30-tool discovery and list reads over HTTPS. Do not test deletion or score changes
against real production records. Record the release tag/commit and the read-only
result in the verification record below.

## Verification record (2026-09-21)

A disposable local deployment used the published .NET 8 API with PostgreSQL and
the real Vite web application. No production data or provider credentials were
used. Each client received its own token on the same test admin account:

| Installed client | Real connection checks |
| --- | --- |
| Codex `0.155.0-alpha.9.2` (bundled CLI) | Actual app-server MCP connection: discovered 30 tools, verified account, read games, saved a 2–1 prediction, updated a disposable tournament name |
| Hermes `0.21.3 (2026.9.14)`, commit `a51143fb` | Installed Hermes MCP transport with environment interpolation: discovered 30 tools, verified the same account, read games, updated that prediction to 3–1, updated the disposable tournament name |

Both changes were independently checked by logging into the real web UI with
Cypress: tournament name and prediction score were visible after each client.
These were deterministic client-transport tests, not model-generated agent turns.
The test connections were revoked afterwards. No persistent Codex/Hermes MCP
configuration or destructive auto-approval setting was added.

Automated evidence: 97 backend tests including all admin tools, scope/role denial,
live role removal, real PostgreSQL cascades/owner deletion, standings updates,
provider-error redaction and the disable switch; frontend production build;
23 existing account/prediction/admin Cypress checks plus two live UI smoke checks.

**Pending after review:** release tag/deployment and read-only production HTTPS
identity/discovery/read smoke. Local client tests do not establish production
proxy compatibility or a completed release. Do not mark this release step complete
until those results are recorded.

## OAuth verification record (SME-106, 2026-09-29)

Local .NET 9 tests: 135 passed, none skipped, with disposable PostgreSQL databases.
Coverage includes metadata/challenge, PKCE and redirect rejection, public/confidential
registration, CIMD metadata validation, code/refresh expiry and replay, concurrent
exchanges, refresh racing revocation, scope/role checks, owner cascade, manual tokens,
and real SDK tool discovery and calls. The frontend production build and changed-file
lint pass; the complete Electron Cypress suite passes 55 tests, including four OAuth
journeys. Global lint reports seven pre-existing errors in unchanged files.

The configured Codex connection still returned the expected account/scopes using its
existing production token; that is a compatibility baseline, not a test of unreleased
code. `hermes mcp test predictions` reports that the server is absent from the current
Hermes configuration; no profile or client configuration was changed to manufacture
a pass. Claude's public client metadata and documented callback were checked, but no
production OAuth flow or actual Claude Origin capture was performed.

**Human gate:** review this authentication change and the unprotected branch checks
before merging. After a separately approved tagged release, verify Claude sign-in,
selected scopes, tools, refresh and revocation against the released HTTPS endpoint;
repeat the existing Codex/Hermes read-only client checks. Do not equate local tests
with those pending acceptance checks.
