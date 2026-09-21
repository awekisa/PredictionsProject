# Predictions MCP server (SME-100)

The API exposes **`/mcp`** using Streamable HTTP and
[`ModelContextProtocol.AspNetCore` 2.2.0](https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.2.0).
Integration tests use the same SDK client with both **2025-11-25** (initialize /
initialized handshake) and **2026-07-28** (server discovery) protocol revisions.
The server uses the SDK's stateless mode: every HTTP request authenticates its
bearer token, there is no session affinity, and a session ID grants no access.
The twelve user tools below and the eighteen [admin tools](mcp.md) are registered. REST continues to require browser JWTs.

Once this change is deployed, connect directly to the API at
`https://predictionsproject.onrender.com/mcp`, **not** the Vercel frontend and
not `/api/mcp`. Create a scoped token in Account settings → Agent connections
and send `Authorization: Bearer <token>` on every request. Keep tokens in the
client's secret/environment configuration, not prompts, source files or URLs.
The server does not publish OAuth discovery. See [the connection guide](mcp.md) for admin tools, Codex/Hermes setup and verification.

## Tools and permissions

Every read requires `app:read`; saving requires `predictions:write`. Scopes are
independent, so a write-only token can save without reading. Discovery exposes
tool schemas; every invocation applies a live scope policy before running code.
Tokens with only admin scopes cannot invoke these user tools.

| Tool | Inputs (in addition to pagination on lists) | Result |
| --- | --- | --- |
| `get_current_user` | none | Account ID, display name, current roles, token scopes |
| `list_tournaments` | none | Tournament page, ordered by ID |
| `get_tournament` | `tournamentId` | Tournament |
| `list_games` | `tournamentId` | Fixture page, ordered by ID |
| `get_game` | `tournamentId`, `gameId` | Fixture, including original deadline |
| `get_my_predictions` | `tournamentId` | Token owner's prediction page, ordered by ID |
| `get_game_predictions` | `gameId` | Public prediction page, ordered by ID; empty before kickoff |
| `get_tournament_standings` | `tournamentId` | Prediction standings page |
| `get_global_standings` | none | Prediction standings page |
| `get_user_prediction_details` | `userDisplayName`, optional `tournamentId`, optional `type` | Public detail page, newest match first, full row as tie breaker |
| `get_football_standings` | `tournamentId` | Flattened official standings rows `{stage, group, standing}` |
| `save_my_prediction` | `gameId`, `homeGoals`, `awayGoals` | Persisted prediction; creates or overwrites the owner's existing record |

`type` accepts the existing website filters: `all` (earned points, default),
`scores` (exact scores), `outcomes` (correct outcome but not exact score), and
`total` (all visible predictions). Omit `tournamentId` or use null for global
prediction details. Private pre-kickoff predictions and admin predictions retain
the application's existing visibility rules.

Every list accepts `offset` (default 0, nonnegative 32-bit integer) and `limit`
(default 50, from 1 to 100). Results have `items`, `offset`, `limit`, `total`, and
`nextOffset` (null when complete). Follow `nextOffset` to retrieve every row.
An offset past the end succeeds with an empty page. Standings pages are ordered
by display name with deterministic field tie breakers; `position` retains the
website's rank. Football rows are ordered by stage, group, position, then row
fields. Offsets refer to live data, not a snapshot: restart pagination if records
change during traversal. Existing services load their lists before the MCP layer
bounds the response; this does not add database-level pagination.

IDs must be positive 32-bit integers; scores must be nonnegative 32-bit integers.
Unknown/extra arguments are rejected, including a target user ID on prediction
saving. Missing records, closed predictions, bad arguments and missing scopes
produce sanitized MCP tool errors. Empty public lists are successful responses.
Nonexistent tournament/game IDs produce errors. An existing tournament with no
football standings produces an empty page, matching the service's unavailable
result. Tool schemas include descriptions, output schemas and read/mutation hints.

Saving calls the same service as REST and checks the original deadline, including
when kickoff moves later. The service uses an injectable clock (system UTC in
production) so exact cutoff behavior can be tested. Repeating a save updates the
same user/game record. **The server never retries mutations.** After an ambiguous
network failure, inspect the saved prediction before deciding whether to resend.

## Hosting and security

The existing Docker API binds port 8080. [Render web services](https://render.com/docs/web-services#connecting-from-the-public-internet)
terminate public HTTPS at their load balancer and forward HTTP to that port.
Connect using HTTPS directly, so credentials are not sent to an HTTP redirect.
This stateless endpoint requires no separate process, sticky sessions or public
GET/SSE subscription. Standard HTTP POST responses use the SDK transport. No
forwarded-header trust changes are needed. Live deployment/client validation
remains part of release verification; local protocol tests do not prove that a
production deployment has updated.

Supply exact browser origins in `Mcp:AllowedOrigins` (environment example:
`Mcp__AllowedOrigins__0=https://predictions-project.vercel.app`). When absent,
the existing comma-separated `CorsOrigins` setting is used, then the development
origin `http://localhost:5173`. A present, unlisted Origin (including `null`) is
rejected with 403 before CORS/authentication. Native clients without Origin are
allowed. Browser clients must also be allowed by the existing CORS configuration.
All MCP responses carry `Cache-Control: no-store`.

Absent, invalid, expired, revoked or deleted-account credentials receive HTTP
401. The endpoint uses only the dedicated MCP token scheme, never a browser JWT.
Each tool invokes the existing scope policy, which re-reads token validity and
ownership. Work already running when a token is revoked is not rolled back.

Mutation audits use logger category `Predictions.Mcp.Audit` at Information level:
`ActorId`, `TokenId` (metadata ID only), `Tool`, `TargetId`, `Timestamp`, `Result`,
`CorrelationId`. Outcomes include succeeded, rejected, denied, failed, cancelled.
HTTP authentication failures occur before tool execution and are not mutation
events. SDK diagnostic logging is disabled to prevent argument/exception payload
capture; backend exceptions become generic errors. Do not enable HTTP body or
Authorization-header capture in proxies, APM, or application logging. Retain the
audit category at Information or lower if overriding deployment log filters.

## SDK smoke test

Use a .NET console project with `ModelContextProtocol` 2.2.0. Set
`PREDICTIONS_MCP_URL` to the HTTPS API endpoint and `PREDICTIONS_MCP_TOKEN` to a
read-scoped token through your local secret environment. This only reads identity
and discovers tool schemas:

```csharp
using ModelContextProtocol.Client;

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new("Bearer",
    Environment.GetEnvironmentVariable("PREDICTIONS_MCP_TOKEN")!);
await using var transport = new HttpClientTransport(new()
{
    Endpoint = new Uri(Environment.GetEnvironmentVariable("PREDICTIONS_MCP_URL")!),
    TransportMode = HttpTransportMode.StreamableHttp
}, http);
await using var client = await McpClient.CreateAsync(transport,
    new() { ProtocolVersion = "2025-11-25" });
var tools = await client.ListToolsAsync();
var identity = await client.CallToolAsync("get_current_user");
// Inspect locally; do not print credentials or private prediction payloads to shared logs.
```

Run `dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj`. Set
`MCP_TEST_POSTGRES` as described in [the access-token guide](mcp-access-tokens.md)
to include PostgreSQL token persistence/migration checks. The protocol tests host
the production MCP registration/authentication with an isolated test database and
exercise both protocol generations, all tools, two-account interleaving, scopes,
expiry/revocation, exact deadlines/moved fixtures, REST parity, argument validation,
pagination, Origin validation, sanitized errors and audit metadata.
