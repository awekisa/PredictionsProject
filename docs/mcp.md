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

Tokens expire after the selected 7, 30 or 90 days. To rotate, create a new token
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
| OAuth login fails | This server uses account-created bearer tokens; do not run an OAuth login flow. |
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
Native no-Origin calls are allowed. Every MCP response has `Cache-Control: no-store`.

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
