# PredictionsProject

PredictionsProject is a football prediction app with a React/Vite frontend and an ASP.NET Core backend.

## Repository layout

- `predictions-ui/` — React + TypeScript UI built with Vite.
- `PredictionsAPI/` — ASP.NET Core API.
- `PredictionsAPI.Tests/` — backend test project.
- `scripts/` — release and project utility scripts.
- `docs/` — contributor and workflow documentation.

## Local verification

Run frontend checks from `predictions-ui/`:

```bash
npm install
npm run build
```

Run backend tests from the repository root:

```bash
dotnet test PredictionsAPI.Tests/PredictionsAPI.Tests.csproj
```

## Linear + GitHub workflow

Work that starts from Linear should include the Linear issue key in the branch, commit, and pull request body so the systems can link reliably.

See [`docs/github-linear-workflow.md`](docs/github-linear-workflow.md) for the exact convention used by this repository.
