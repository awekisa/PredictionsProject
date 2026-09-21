# Agent access tokens

Account settings → Agent connections provides separately named credentials for
agents such as Codex and Hermes. Choose 7, 30 (default), or 90 days and explicitly
select permissions. Save the token when created: it is shown only once. Closing
the section or drawer discards the in-memory display; it is never stored in
browser local/session storage. If lost, revoke it and create another.

The API now exposes authenticated user tools at `/mcp` (SME-100). See the
[MCP server guide](mcp-server.md) for transport, tools, pagination and an SDK
connection example. SME-101 adds admin tools and tested Codex/Hermes setup.

| Scope | Allows | Default |
| --- | --- | --- |
| `app:read` | Read app information permitted to the owner | Selected |
| `predictions:write` | Save the owner's predictions | Off |
| `admin:read` | Read administration data, with a current Admin role | Off |
| `admin:write` | Perform administration actions, with a current Admin role | Off |

Scopes are independent. A current admin must explicitly grant each admin scope;
promotion to admin does not expand an existing token's scopes. Demotion removes
admin access immediately. The token remains usable for any granted ordinary
scope until expired or revoked. Account deletion invalidates all its tokens.

## Backend integration

The existing browser JWT is required for `GET`/`POST`
`/api/auth/me/agent-connections` and `DELETE .../{tokenId}`. These endpoints return
`Cache-Control: no-store`. The token creation response is the only response
containing the raw token. The database stores SHA-256 hashes of credentials with
256 bits of random entropy, metadata and the owner foreign key. Deleting an owner
cascades token deletion. Never enable request/response body logging or telemetry
capture on these credential endpoints, and never log Authorization headers.

The MCP endpoint explicitly uses `McpAuthentication.Scheme` and
`McpScopes.Policy(scope)` for each protected operation. The MCP scheme only
authenticates `/mcp` and its subpaths and is never the default REST scheme.
Do not use a plain authenticated-user check, a cached role, or a retained
session/principal as authorization for an MCP operation. Each policy evaluation
re-reads token validity, owner and current admin role through
`IMcpAccessTokenService.HasPermissionAsync`. If the transport retains a principal
for multiple tool calls, invoke the scope policy for **each call**.

Revocation takes effect on the next authorization check; already-running work
is not rolled back. Last-used updates change only that database column so they
cannot overwrite a simultaneous revocation. Tokens do not authenticate normal
REST routes and cannot issue or manage other tokens.

## Verification

- `dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj`
- Set `MCP_TEST_POSTGRES` to a disposable local PostgreSQL server connection
  with CREATE DATABASE permission to also run the relational integration test.
  It creates a uniquely named test database, verifies migration apply/rollback,
  persistence and cascade deletion, then drops only that generated database.
  CI supplies this server automatically; without the variable that test is skipped.
- `npm --prefix predictions-ui run build`
- With the Vite test server running, run Cypress specs
  `account-drawer.cy.ts` and `agent-connections.cy.ts`.
- Apply and roll back `AddMcpAccessTokens` on a disposable PostgreSQL database.
  Confirm metadata survives a new database context, only hashes are stored,
  owner deletion cascades, and no raw token appears in server logs.
