# GitHub + Linear workflow

Use this workflow whenever a PredictionsProject change starts from a Linear issue.

## Naming convention

1. Move the Linear issue to **In Progress** before starting work.
2. Create a branch that starts with the lowercase issue key:

   ```bash
   git checkout -b sme-31-verify-linear-github-workflow
   ```

3. Include the issue key in the commit subject:

   ```bash
   git commit -m "docs(SME-31): document GitHub Linear workflow"
   ```

4. Include the issue key in the pull request body:

   ```md
   Closes SME-31
   ```

5. After opening the PR, confirm the PR appears on the Linear issue. If the integration does not attach it automatically, add a Linear comment with the PR URL and keep the issue in **In Review** until the PR is merged or closed.

## Verification checklist

For the first PR that exercises this workflow, verify all of the following:

- The branch name contains the Linear issue key.
- The commit subject contains the Linear issue key.
- The PR body contains `Closes SME-31` or the relevant Linear issue key.
- GitHub shows the pushed branch and PR.
- Linear shows the PR or has a manual comment with the PR URL if the GitHub integration does not link automatically.
- Local build/tests pass, or any blocker is recorded in the PR and Linear issue.

## Current verification issue

This document was introduced under Linear issue `SME-31` to verify the repository workflow end to end.
