# PredictionsProject

A football predictions web app where users predict match scores and compete on a leaderboard.

## Stack
- **Backend**: .NET 8 ASP.NET Core Web API — `PredictionsAPI/`
- **Frontend**: React 19 + Vite + TypeScript — `predictions-ui/`
- **Database**: PostgreSQL via Npgsql EF Core
- **Auth**: ASP.NET Identity + JWT Bearer
- **Tests**: `PredictionsAPI.Tests/`

## Local Dev
- API: http://localhost:5039
- Frontend: http://localhost:5173
- Start API: `dotnet run` inside `PredictionsAPI/`
- Start frontend: `npm run dev` inside `predictions-ui/`
- Frontend env: `predictions-ui/.env` with `VITE_API_URL=http://localhost:5039/api`

## Key Files
- `PredictionsAPI/Program.cs` — startup, CORS, auto-migration, seeding
- `PredictionsAPI/Extensions/ServiceCollectionExtensions.cs` — DI, DB, Identity, JWT config
- `PredictionsAPI/appsettings.json` — connection string, CORS origins, JWT settings
- `predictions-ui/src/api/apiClient.ts` — axios client, reads `VITE_API_URL`

## Database
- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` v8.0.11
- Connection string format: `Host=...;Database=...;Username=...;Password=...`
- Auto-migration runs on startup via `db.Database.Migrate()` in `Program.cs`

## Deployment (free tier)
- **Frontend**: Vercel — set `VITE_API_URL` env var to the Render backend URL
- **Backend**: Render — set `ConnectionStrings__DefaultConnection` and `CorsOrigins` env vars
- **Database**: Supabase free PostgreSQL
- Production URL: https://predictions-project.vercel.app

## UI Design
- Dark charcoal theme: `--color-bg: #2d2d2d`, `--color-surface: #3a3a3a`
- Accent gold: `--color-primary: #c9a020` — gold buttons use `color: #1a1a1a`
- Light/dark theme toggle stored in localStorage
- Game card layout: time + status badge | home team — score — away team | predict action
- Score inputs inline in match row for upcoming games
- Tabs: "Games" + "Prediction Standings"
- Date filter: Yesterday / Today / Tomorrow / This Week | All
- Design reference: `designs/visual1.png`

## Release Process
Deployments are triggered by git tags, not by pushes to master.

- `master` pushes → tests only (no deploy)
- `v*.*.*` tag pushes → tests + deploy to Render + Vercel

**To release a new version:**
```bash
./scripts/release.sh 1.2.0
```
This bumps `predictions-ui/package.json`, commits `chore: release v1.2.0`, tags `v1.2.0`, and pushes both the commit and tag.

**To deploy a specific older commit:**
```bash
git tag v1.1.0 <commit-hash>
git push origin v1.1.0
```

## Admin Credentials (default — change in production)
- Email: admin@predictions.com
- Password: Admin123!

## Connecting to the App (Browser Automation)
Use the MCP Playwright tool to interact with the live app. When asked to "connect to the app" or "open the app":
1. Navigate to https://predictions-project.vercel.app/login
2. Login with the test user credentials:
   - Email: testuser@predictions.com
   - Password: Test123!
3. You will land on the Tournaments page — ready to interact.

Note: The backend runs on Render free tier and may take 30–60 seconds to cold-start on first request.
