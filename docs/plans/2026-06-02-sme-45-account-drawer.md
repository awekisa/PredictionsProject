# SME-45 Account Drawer Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Add a drawer-style profile panel where authenticated users can update their display name and change their password.

**Architecture:** Reuse the uploaded prototype’s drawer UI, but integrate it into the real `predictions-ui` and `PredictionsAPI` codebase with tests first. The frontend adds typed auth API calls and a new `AccountDrawer` opened from the existing `Navbar`; the backend adds authorized account-management endpoints to `AuthController` and reuses ASP.NET Identity password validation plus the existing JWT token service.

**Tech Stack:** React 19 + TypeScript + CSS Modules + Cypress + Axios frontend; ASP.NET Core 9 + Identity + xUnit/Moq backend; GitHub Actions for backend CI because `dotnet` is not installed locally on Dimitar’s Mac.

---

## Prototype vs Current Repo Comparison

### Uploaded zip contents

The uploaded file `/Users/merlinoc/.hermes/cache/documents/doc_1dc6a06dd989_PredictionsProject.zip` extracts to a `handoff/` folder containing:

- `handoff/README.md`
- `handoff/src/api/authApi.ts`
- `handoff/src/types/account-additions.ts`
- `handoff/src/components/account/AccountDrawer.tsx`
- `handoff/src/components/account/AccountDrawer.module.css`
- `handoff/src/components/layout/Navbar.tsx`
- `handoff/src/components/layout/Navbar.module.css.additions`

### What matches cleanly

- Current frontend already has `predictions-ui/src/api/authApi.ts`; prototype adds two functions only: `updateUsername` and `changePassword`.
- Current frontend already has `predictions-ui/src/types/index.ts`; prototype adds two request interfaces only.
- Current `AuthContext` already exposes `handleAuthResponse(response: AuthResponse)`, so username updates can refresh token, localStorage, and visible username without changing context shape.
- Current `Navbar.tsx` has the static `styles.userName` span at line 42; prototype replaces it with an accessible button that opens a drawer.
- Current CSS tokens include `--color-primary`, `--color-bg`, `--color-surface`, `--color-border`, `--color-text`, `--color-text-secondary`, `--color-danger`, `--color-success`, and `--color-warning`, so the drawer CSS should theme in both dark and light mode.
- Current global CSS already sets mobile `input, select { font-size: 1rem; }`; the prototype repeats the important iOS zoom guard inside the drawer.

### What is missing in the current repo

- No `predictions-ui/src/components/account/` folder.
- No frontend account drawer tests yet.
- No `UpdateUsernameRequest` / `ChangePasswordRequest` TypeScript interfaces.
- No backend DTOs for username/password updates.
- `PredictionsAPI/Controllers/AuthController.cs` currently has only `POST /api/auth/register` and `POST /api/auth/login`.
- `AuthController.cs` does not currently import `[Authorize]` or `System.Security.Claims`.
- Backend Identity password rules already exist in `ServiceCollectionExtensions.cs`: digit, lowercase, uppercase, no non-alphanumeric requirement, minimum length 6.

### Prototype gaps / adjustments before implementation

- Do not wholesale replace `Navbar.tsx` without preserving repo formatting and current link structure; patch the minimal changes instead.
- Remove handoff comments like `// REPLACE your existing file with this` before committing production code.
- Improve accessibility over the prototype:
  - Give the drawer title an id and use `aria-labelledby` instead of only `aria-label`.
  - Mark the overlay as presentation-only.
  - Use text labels for show/hide password buttons rather than emoji-only buttons, or at least make the `aria-label` state-specific.
- Consider resetting the username form when `current` changes after a successful save, because `current` is derived from context and can update after `handleAuthResponse`.
- Backend username update should update both `DisplayName` and the JWT name claim by returning a fresh `AuthResponse` using `_tokenService.GenerateTokenAsync(user)`.
- Backend password endpoint should return clear 400 string messages for bad current password and password rule failures, matching the drawer’s inline error behavior.

---

## Branch and Linear Setup

### Task 1: Start SME-45 branch

**Objective:** Prepare a clean, Linear-linked implementation branch.

**Files:** none.

**Steps:**

```bash
cd /Users/merlinoc/Projects/PredictionsProject
git checkout master
git pull --ff-only origin master
git checkout -b sme-45-profile-panel
```

**Verification:**

```bash
git status --short --branch
```

Expected: branch is `sme-45-profile-panel`, working tree clean except ignored `predictions-ui/dist/` and `predictions-ui/node_modules/` if present.

**Commit:** none yet.

---

## Backend Tasks

### Task 2: Add failing backend tests for authorized username updates

**Objective:** Prove the API must update `DisplayName` for the authenticated user and return a refreshed `AuthResponse`.

**Files:**
- Create: `PredictionsAPI.Tests/AuthControllerTests.cs`
- Modify only if needed: `PredictionsAPI.Tests/PredictionsAPI.Tests.csproj`

**Step 1: Write failing tests**

Add tests covering:

- `UpdateUsername_AuthenticatedUser_UpdatesDisplayNameAndReturnsAuthResponse`
- `UpdateUsername_NameTooShort_ReturnsBadRequest`
- `UpdateUsername_MissingUser_ReturnsUnauthorized`

Use Moq to create `UserManager<ApplicationUser>` and `ITokenService`. Set `controller.ControllerContext.HttpContext.User` to a `ClaimsPrincipal` with `ClaimTypes.NameIdentifier`.

Important test assertions:

- `DisplayName` is trimmed before saving.
- `_userManager.UpdateAsync(user)` is called once.
- response body has same shape as login/register: `{ token, email, displayName }`.
- too-short display name returns `BadRequestObjectResult` with a human-readable string.

**Step 2: Verify RED**

Local machine note: `dotnet` is currently not installed locally. If available in the execution environment, run:

```bash
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj --filter AuthControllerTests
```

Expected: FAIL because request DTOs and controller endpoint do not exist.

If local `dotnet` is unavailable, rely on GitHub CI after pushing the failing commit only if you explicitly want remote RED verification; otherwise document the local blocker and continue with frontend-local verification plus CI after implementation.

**Step 3: Commit failing tests**

```bash
git add PredictionsAPI.Tests/AuthControllerTests.cs
git commit -m "test(SME-45): cover account username updates"
```

---

### Task 3: Add backend DTOs for account updates

**Objective:** Add explicit request DTOs for the two account-management endpoints.

**Files:**
- Create: `PredictionsAPI/DTOs/Auth/UpdateUsernameRequest.cs`
- Create: `PredictionsAPI/DTOs/Auth/ChangePasswordRequest.cs`

**Implementation:**

```csharp
namespace PredictionsAPI.DTOs.Auth;

public class UpdateUsernameRequest
{
    public string DisplayName { get; set; } = string.Empty;
}
```

```csharp
namespace PredictionsAPI.DTOs.Auth;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
```

**Verification:**

```bash
git diff --check
```

Expected: no whitespace errors.

**Commit:** combine with Task 4 implementation if preferred, because DTOs alone do not make tests pass.

---

### Task 4: Implement `PUT /api/auth/me/username`

**Objective:** Make username update tests pass with minimal backend code.

**Files:**
- Modify: `PredictionsAPI/Controllers/AuthController.cs`

**Implementation details:**

- Add imports:
  - `using Microsoft.AspNetCore.Authorization;`
  - `using System.Security.Claims;`
- Add `[Authorize]` to the new action only.
- Get the current user via `await _userManager.GetUserAsync(User)`; this matches the prototype and Identity conventions.
- Validate `request.DisplayName.Trim().Length >= 2`.
- Update `user.DisplayName` with trimmed value.
- Call `_userManager.UpdateAsync(user)` and handle failure as `BadRequest(...)`.
- Generate a fresh token via `_tokenService.GenerateTokenAsync(user)`.
- Return `Ok(new AuthResponse { Token = token, Email = user.Email!, DisplayName = user.DisplayName })`.

**Step 1: Verify GREEN**

If local `dotnet` is available:

```bash
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj --filter AuthControllerTests
```

Expected: username tests pass; password tests do not exist yet.

**Step 2: Commit**

```bash
git add PredictionsAPI/Controllers/AuthController.cs PredictionsAPI/DTOs/Auth/UpdateUsernameRequest.cs PredictionsAPI.Tests/AuthControllerTests.cs
git commit -m "feat(SME-45): add username update endpoint"
```

---

### Task 5: Add failing backend tests for password changes

**Objective:** Prove password changes require the current password, enforce existing Identity rules, and return `204 No Content` on success.

**Files:**
- Modify: `PredictionsAPI.Tests/AuthControllerTests.cs`

**Step 1: Write failing tests**

Add tests covering:

- `ChangePassword_ValidRequest_ReturnsNoContent`
- `ChangePassword_CurrentPasswordMismatch_ReturnsBadRequestMessage`
- `ChangePassword_IdentityPasswordRuleFailure_ReturnsPasswordRuleMessage`
- `ChangePassword_MissingUser_ReturnsUnauthorized`

Mock `_userManager.ChangePasswordAsync(user, currentPassword, newPassword)` to return:

- `IdentityResult.Success`
- `IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch", Description = "Incorrect password." })`
- `IdentityResult.Failed(new IdentityError { Code = "PasswordRequiresDigit", Description = "Passwords must have at least one digit." })`

**Step 2: Verify RED**

```bash
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj --filter AuthControllerTests
```

Expected: FAIL because `ChangePassword` endpoint does not exist.

---

### Task 6: Implement `POST /api/auth/me/password`

**Objective:** Make password tests pass with minimal backend code.

**Files:**
- Create: `PredictionsAPI/DTOs/Auth/ChangePasswordRequest.cs`
- Modify: `PredictionsAPI/Controllers/AuthController.cs`

**Implementation details:**

- Add `[Authorize]` and `[HttpPost("me/password")]`.
- Get user through `_userManager.GetUserAsync(User)`.
- Return `Unauthorized()` when user is missing.
- Call `_userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)`.
- If `PasswordMismatch`, return `BadRequest("Current password is incorrect.")`.
- If any error code starts with `Password`, return `BadRequest("Password must be at least 6 characters and contain an uppercase letter, a lowercase letter, and a digit.")`.
- Otherwise return `BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)))`.
- On success return `NoContent()`.

**Step 1: Verify GREEN**

```bash
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj --filter AuthControllerTests
```

Expected: all new auth controller tests pass.

**Step 2: Commit**

```bash
git add PredictionsAPI/Controllers/AuthController.cs PredictionsAPI/DTOs/Auth/ChangePasswordRequest.cs PredictionsAPI.Tests/AuthControllerTests.cs
git commit -m "feat(SME-45): add password change endpoint"
```

---

## Frontend Tasks

### Task 7: Add failing Cypress tests for the account drawer entry point

**Objective:** Prove the navbar username is clickable and opens a drawer with Profile and Security sections.

**Files:**
- Create: `predictions-ui/cypress/e2e/account-drawer.cy.ts`

**Step 1: Write failing test**

Use existing `cy.visitAuthenticated('/', 'Admin')` helper and intercept `GET **/api/tournaments` with an empty list.

Test behavior:

```ts
cy.visitAuthenticated('/', 'Admin');
cy.contains('button', /admin user/i).click();
cy.findByRole?. // not available unless Cypress Testing Library is installed, so use built-in selectors
cy.get('[role="dialog"][aria-modal="true"]').should('be.visible');
cy.contains(/profile/i).should('be.visible');
cy.contains(/security/i).should('be.visible');
cy.contains('label', /username/i).should('be.visible');
cy.contains('label', /current password/i).should('be.visible');
cy.contains('label', /new password/i).should('be.visible');
cy.contains('label', /confirm new password/i).should('be.visible');
```

Use Cypress built-ins only; no new test dependency.

**Step 2: Verify RED**

```bash
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
```

Expected: FAIL because the navbar username is still a static span and no drawer exists.

**Commit:**

```bash
git add predictions-ui/cypress/e2e/account-drawer.cy.ts
git commit -m "test(SME-45): cover account drawer opening"
```

---

### Task 8: Add frontend types and auth API functions

**Objective:** Add typed client calls for the backend account endpoints.

**Files:**
- Modify: `predictions-ui/src/types/index.ts`
- Modify: `predictions-ui/src/api/authApi.ts`

**Implementation:**

Add to `types/index.ts` near existing auth request interfaces:

```ts
export interface UpdateUsernameRequest {
  displayName: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
```

Patch `authApi.ts` import and exports:

```ts
import type {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  UpdateUsernameRequest,
  ChangePasswordRequest,
} from '../types';

export const updateUsername = (data: UpdateUsernameRequest) =>
  apiClient.put<AuthResponse>('/auth/me/username', data).then((res) => res.data);

export const changePassword = (data: ChangePasswordRequest) =>
  apiClient.post<void>('/auth/me/password', data).then((res) => res.data);
```

**Verification:**

```bash
cd predictions-ui
npm run build
```

Expected: TypeScript compiles.

**Commit:**

```bash
git add predictions-ui/src/types/index.ts predictions-ui/src/api/authApi.ts
git commit -m "feat(SME-45): add account auth API calls"
```

---

### Task 9: Add AccountDrawer component and CSS

**Objective:** Bring the drawer UI into the app, with prototype behavior and accessibility improvements.

**Files:**
- Create: `predictions-ui/src/components/account/AccountDrawer.tsx`
- Create: `predictions-ui/src/components/account/AccountDrawer.module.css`

**Implementation notes:**

Use the uploaded prototype as the base, but adjust before committing:

- Remove handoff comments.
- Add a stable title id, for example `const titleId = 'account-drawer-title';` and render `<aside role="dialog" aria-modal="true" aria-labelledby={titleId}>`.
- Render `<h2 id={titleId}>Account settings</h2>` or make the visible name subordinate to an accessible heading.
- Use state-specific reveal labels: `aria-label={reveal ? 'Hide passwords' : 'Show passwords'}`.
- Keep password validation exactly aligned with backend rule: at least 6 chars, uppercase, lowercase, digit.
- Keep body scroll lock and Escape close.
- Keep mobile full-width drawer at `max-width: 640px`.
- After username save, call `handleAuthResponse(res)` and update/reset local `name` state to the returned `displayName`.

**Verification:**

```bash
cd predictions-ui
npm run build
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
```

Expected: build passes; the drawer opening test still fails until Navbar is wired in Task 10.

**Commit:** combine with Task 10 if needed, because the component is not reachable yet.

---

### Task 10: Wire AccountDrawer into Navbar

**Objective:** Replace the static username span with a clickable account button and make the existing Cypress drawer test pass.

**Files:**
- Modify: `predictions-ui/src/components/layout/Navbar.tsx`
- Modify: `predictions-ui/src/components/layout/Navbar.module.css`

**Implementation details:**

Patch current `Navbar.tsx` rather than replacing wholesale:

- Import `AccountDrawer`.
- Add `const [accountOpen, setAccountOpen] = useState(false);`.
- Add an `initials` helper based on `user?.displayName`.
- Replace `<span className={styles.userName}>{user?.displayName}</span>` with the prototype’s `userBtn` button.
- Render `<AccountDrawer open={accountOpen} onClose={() => setAccountOpen(false)} />` before closing `</nav>`.
- Keep existing hamburger, theme toggle, logout, and mobile menu behavior.

Patch `Navbar.module.css`:

- Remove or stop using `.userName`.
- Add `.userBtn`, `.avatar`, `.userMeta`, `.userNameTxt`, and `.userRole` from `Navbar.module.css.additions`.
- At mobile breakpoint, hide `.userMeta` and show avatar only.

**Verification:**

```bash
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
npm run build
```

Expected: drawer opening test passes; build passes.

**Commit:**

```bash
git add predictions-ui/src/components/account/AccountDrawer.tsx predictions-ui/src/components/account/AccountDrawer.module.css predictions-ui/src/components/layout/Navbar.tsx predictions-ui/src/components/layout/Navbar.module.css
git commit -m "feat(SME-45): add account drawer to navbar"
```

---

### Task 11: Add Cypress tests for username save

**Objective:** Prove username saves call the new endpoint and update the visible navbar display name through `handleAuthResponse`.

**Files:**
- Modify: `predictions-ui/cypress/e2e/account-drawer.cy.ts`

**Step 1: Write failing test**

Test behavior:

- Intercept `PUT **/api/auth/me/username`.
- Return `{ token: buildFakeJwt('Admin'), email: 'admin@test.com', displayName: 'Dimitar' }`.
- Open drawer.
- Change username input from `Admin User` to `Dimitar`.
- Click `Save username`.
- Wait for intercept and assert request body `{ displayName: 'Dimitar' }`.
- Assert navbar now shows `Dimitar` and drawer success text appears.

If `buildFakeJwt` is duplicated in current specs, extract it from `login.cy.ts` into `cypress/support/commands.ts` or add a small local helper in `account-drawer.cy.ts`.

**Step 2: Verify RED/GREEN**

Run before implementation if needed to verify missing behavior, then after Tasks 8–10:

```bash
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
```

Expected after implementation: PASS.

**Commit:**

```bash
git add predictions-ui/cypress/e2e/account-drawer.cy.ts
git commit -m "test(SME-45): cover username drawer save"
```

---

### Task 12: Add Cypress tests for password validation and submit

**Objective:** Prove the drawer validates password rules client-side and posts valid password changes.

**Files:**
- Modify: `predictions-ui/cypress/e2e/account-drawer.cy.ts`

**Tests:**

1. Password rules:
   - Type weak `abc` into new password.
   - Assert `At least 6 characters`, `One uppercase letter`, and `One number` are not satisfied while `One lowercase letter` is satisfied.
   - Assert `Update password` is disabled.
2. Confirm mismatch:
   - Type valid new password `Better123` and confirm `Different123`.
   - Assert `Passwords do not match.` is visible and submit disabled.
3. Successful change:
   - Intercept `POST **/api/auth/me/password` as `204`.
   - Type current `Admin123`, new `Better123`, confirm `Better123`.
   - Click `Update password`.
   - Assert request body has `currentPassword` and `newPassword` only.
   - Assert success text `Password changed` appears and fields are cleared.
4. API error:
   - Intercept with `400` body `Current password is incorrect.`.
   - Assert inline form error appears.

**Verification:**

```bash
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
```

Expected: PASS.

**Commit:**

```bash
git add predictions-ui/cypress/e2e/account-drawer.cy.ts
git commit -m "test(SME-45): cover password drawer behavior"
```

---

### Task 13: Add mobile Cypress coverage

**Objective:** Prove the drawer works in the mobile layout Dimitar liked.

**Files:**
- Modify: `predictions-ui/cypress/e2e/account-drawer.cy.ts`

**Test behavior:**

```ts
cy.viewport(390, 844);
cy.visitAuthenticated('/', 'Admin');
cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: [] });
cy.get('button[aria-haspopup="dialog"]').click();
cy.get('[role="dialog"][aria-modal="true"]').should('be.visible');
cy.get('[role="dialog"][aria-modal="true"]').then(($drawer) => {
  expect($drawer.outerWidth()).to.be.greaterThan(360);
});
cy.contains('label', /username/i).should('be.visible');
cy.contains('label', /current password/i).should('be.visible');
```

Also check Escape close in a separate small test or inside this test.

**Verification:**

```bash
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
```

Expected: PASS.

**Commit:**

```bash
git add predictions-ui/cypress/e2e/account-drawer.cy.ts
git commit -m "test(SME-45): cover mobile account drawer"
```

---

## Final Verification Tasks

### Task 14: Run frontend verification

**Objective:** Verify all frontend work locally.

**Commands:**

```bash
cd /Users/merlinoc/Projects/PredictionsProject/predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
npm run test:flags
npm run build
```

**Expected:**

- Account drawer Cypress spec passes.
- Flag verifier still passes with 43 World Cup team mappings.
- Vite/TypeScript build passes.

**Note:** `npm run lint` currently has known unrelated existing violations from earlier work; run it if desired, but document unrelated failures rather than expanding SME-45 to fix lint debt.

---

### Task 15: Run backend verification through CI

**Objective:** Verify backend tests despite local `dotnet` absence.

**Commands:**

If `dotnet` becomes available locally:

```bash
cd /Users/merlinoc/Projects/PredictionsProject
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj
```

Otherwise push PR and rely on GitHub Actions backend job:

```bash
git push -u origin sme-45-profile-panel
```

Expected CI jobs:

- Backend Tests: success
- Frontend E2E Tests: success
- Vercel preview: success

---

## PR / Linear / Release Tasks

### Task 16: Open Linear-linked PR

**Objective:** Open SME-45 PR with complete linkage and verification notes.

**Commands:**

```bash
cd /Users/merlinoc/Projects/PredictionsProject
git status --short --ignored=matching
git diff --cached --check || true
gh pr create \
  --title "feat(SME-45): add account drawer for username and password" \
  --body-file /tmp/sme-45-pr-body.md
```

PR body must include:

```md
## Summary
- Adds a drawer-style account panel opened from the navbar username/avatar
- Lets authenticated users update display name with refreshed auth token
- Lets authenticated users change password with current/new/confirm validation
- Adds authorized backend account endpoints and Cypress coverage

## Verification
- npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
- npm run test:flags
- npm run build
- Backend Tests: verified in GitHub Actions

Closes SME-45
```

**Post-PR checks:**

```bash
gh pr view --json number,url,title,state,headRefName,baseRefName,mergeStateStatus,statusCheckRollup
gh pr checks --watch
```

Move Linear issue SME-45 to In Review and add a comment with PR URL, branch, commit, and verification.

---

### Task 17: Merge and release

**Objective:** Ship SME-45 after green CI and approval.

**Commands:**

```bash
gh pr merge --squash --delete-branch
git checkout master
git pull --ff-only origin master
```

Then create the next version after `0.4.10`; likely `0.4.11` unless the team wants a larger minor bump.

```bash
VERSION=0.4.11
# Add CHANGELOG.md entry for SME-45 first.
npm --prefix predictions-ui version "$VERSION" --no-git-tag-version
cd predictions-ui
npm run test:e2e -- --spec cypress/e2e/account-drawer.cy.ts
npm run test:flags
npm run build
cd ..
git diff --check
git add CHANGELOG.md predictions-ui/package.json predictions-ui/package-lock.json
git commit -m "chore: release v$VERSION"
git tag "v$VERSION"
git push origin master
git push origin "v$VERSION"
RUN_ID=$(gh run list --workflow Release --limit 1 --json databaseId --jq '.[0].databaseId')
gh run watch "$RUN_ID" --exit-status
```

**Expected release workflow jobs:**

- Backend Tests: success
- Frontend E2E Tests: success
- Deploy Backend (Render): success
- Deploy Frontend (Vercel): success
- Notify Discord: success

**Verification language:** If the workflow succeeds, say Render/Vercel deploy hooks were triggered. Do not claim the live site updated unless the deployed URL is separately checked.

---

## Acceptance Criteria

- Navbar static display name becomes a clickable username/avatar control.
- Drawer style is the only implemented account panel style; dropdown/modal prototype variations are not imported.
- Drawer supports light and dark themes through existing CSS variables.
- Mobile viewport uses a full-width drawer and avatar-only navbar control.
- Username form validates at least 2 non-whitespace characters.
- Username save calls `PUT /api/auth/me/username`, updates `DisplayName`, returns fresh `AuthResponse`, and updates visible navbar name without logout.
- Password form requires current password, new password, confirm password.
- Password validation matches backend Identity rules: min 6, uppercase, lowercase, digit.
- Password form rejects same-as-current and mismatched confirmation client-side.
- Password save calls `POST /api/auth/me/password` and shows success/error states.
- Drawer closes via close button, overlay click, and Escape.
- Cypress tests cover desktop open/save, password validation, API error, and mobile drawer behavior.
- Backend tests cover success and failure paths for both new endpoints.
- PR body contains `Closes SME-45`.
- CI is green before merge.
