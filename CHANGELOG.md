## 📦 v0.4.44 — 28 Jun 2026

> **Cabo Verde World Cup flag fix**
> World Cup fixtures now use FIFA's current Cabo Verde display name consistently, and the UI maps Cabo Verde to the local Cape Verde flag asset.

---

## 📦 v0.4.43 — 15 Jun 2026

> **Started-game prediction drill-downs**
> Game scores are now clickable after kickoff, including live games, so players can view all predictions once prediction secrecy has ended instead of waiting for the final whistle.

---

## 📦 v0.4.42 — 13 Jun 2026

> **Live FIFA updater script fix**
> Corrected the tracked World Cup sync script to call the score-sync endpoint for live games, store FIFA status/time, and avoid finalising live scores.

---

## 📦 v0.4.41 — 13 Jun 2026

> **EF migration attribute fix**
> Restored the EF infrastructure import required for the migration designer `DbContext` attribute.

---

## 📦 v0.4.40 — 13 Jun 2026

> **EF migration designer compile fix**
> Corrected the generated migration designer imports and target-model override so backend CI can compile and production can apply the FIFA score/status schema.

---

## 📦 v0.4.39 — 13 Jun 2026

> **Migration discovery fix**
> Added the generated EF migration designer metadata for the FIFA match status columns so production applies the schema before reading live score/status fields.

---

## 📦 v0.4.38 — 13 Jun 2026

> **Live FIFA score/status sync**
> World Cup score sync now stores FIFA match status and live scores on each run without marking games finished until FIFA reports a final status. Knockout finalisation keeps the full-time score rule instead of using extra-time or penalty shootout scores for prediction scoring.

---

## 📦 v0.4.37 — 13 Jun 2026

> **Safer World Cup score settlement**
> Production score-settlement scripts now treat FIFA `MatchStatus: 0` as the only automatic finalisation signal, while live status `3` and special statuses require waiting or manual review even when score fields are present.

---

## 📦 v0.4.36 — 13 Jun 2026

> **Compact prediction UI labels**
> Prediction popups now use smaller typography with compact `Pred`/`Pts` columns, and standings use abbreviated `CS`, `1X2`, `PG`, `#TP`, and `PTS` labels with an explanatory legend.

---

## 📦 v0.4.35 — 13 Jun 2026

> **Prediction detail popup polish**
> Game and player prediction popups now keep outcome coloring consistent: green for exact scores, yellow for correct outcomes, and muted gray for misses. Popup close buttons now center the X inside the circular button.

---

## 📦 v0.4.34 — 13 Jun 2026

> **Mobile-fit prediction standings**
> Prediction standings now use compact responsive columns and abbreviated mobile headers so all leaderboard columns fit on narrow screens without horizontal scrolling.

---

## 📦 v0.4.33 — 13 Jun 2026

> **Started-game prediction counts**
> Prediction standings now count only predictions for games that have already started or finished, keeping future predicted games out of the visible totals.

---

## 📦 v0.4.32 — 13 Jun 2026

> **Prediction leaderboard drill-downs**
> Prediction standings now show a leaderboard-only table, finished fixture scores open all-player prediction details, and clickable player rows open started-game prediction profiles without exposing upcoming predictions.

---

## 📦 v0.4.31 — 10 Jun 2026

> **Pre-kickoff prediction privacy**
> Prediction standings detail rows now keep users' exact scores hidden until each game has kicked off, matching the existing per-game prediction privacy rule.

---

## 📦 v0.4.30 — 3 Jun 2026

> **Prediction score outcome colors**
> Prediction score text in standings detail rows now uses the same green/orange/gray outcome colors as the row highlight and earned points.

---

## 📦 v0.4.29 — 3 Jun 2026

> **Clear standings prediction detail rows**
> Prediction detail rows now show `home team score away team` on the left, with the user's prediction and earned points right-aligned in clear columns.

---

## 📦 v0.4.28 — 3 Jun 2026

> **Left-aligned World Cup playoff rows**
> Knockout matchup rows now start from the left after the match number, giving long team names and seed placeholders more usable width before truncation.

---

## 📦 v0.4.27 — 3 Jun 2026

> **Mobile-fit World Cup knockout labels**
> Knockout seed placeholders are shorter on narrow screens (`Winner A`, `Runner-up B`, `Best 3rd from C/E/F/H/I`, `Winner M101`) and the `vs` divider is centered with equal visible spacing between both teams.

---

## 📦 v0.4.26 — 3 Jun 2026

> **World Cup knockout schema**
> The World Cup tournament-format panel now shows the official knockout path from Round of 32 through the Final using FIFA seed placeholders, and replaces seeds with real teams once knockout fixtures are populated.

---

## 📦 v0.4.25 — 3 Jun 2026

> **Device-local game start times**
> Game start parsing is now centralized so UTC API instants render in the browser/device timezone even if an API timestamp arrives without an explicit `Z` suffix. Added Sofia summer-time regression coverage for game-card/admin formatting and deadline helpers.

---

## 📦 v0.4.24 — 3 Jun 2026

> **FIFA World Cup group tie-breakers**
> World Cup group standings now sort equal-points teams by FIFA-style head-to-head criteria first: head-to-head points, head-to-head goal difference, and head-to-head goals scored, before falling back to all-group GD/GF and a deterministic team-name fallback.

---

## 📦 v0.4.23 — 3 Jun 2026

> **Goals for/against in World Cup groups**
> Group panels now show GF and GA alongside P, GD, and Pts, all derived from stored finished-game scores.

---

## 📦 v0.4.22 — 3 Jun 2026

> **World Cup group standings**
> Group panels now show locally derived standings from stored fixture results: played, goal difference, and points. Teams sort by points, goal difference, goals for, then name, without calling external football standings APIs.

---

## 📦 v0.4.21 — 3 Jun 2026

> **Removed external football API admin controls**
> The admin tournament page now keeps only manual/agent-friendly controls: New Tournament, Games, Edit, and Delete. Removed the quota badge, Import from API, API badges, Backfill Fixtures, Sync Scores, the frontend football API client, and stale provider DTO/styles.

---

## 📦 v0.4.20 — 3 Jun 2026

> **Removed unreliable football standings API from UI**
> Tournament pages no longer call the external football standings endpoint. World Cup-style pages always show locally derived fixture groups and the knockout placeholder; non-World-Cup pages no longer show the provider standings sidebar.

---

## 📦 v0.4.19 — 3 Jun 2026

> **World Cup GROUP_STAGE table override**
> Production verification showed football-data returns the World Cup as one ungrouped `GROUP_STAGE` table. The UI now treats any single ungrouped World Cup standings table as unsuitable and displays fixture-derived groups instead.

---

## 📦 v0.4.18 — 3 Jun 2026

> **World Cup groups forced over provider league table**
> Production verification showed the provider returned World Cup standings as one 48-team regular-season table. World Cup pages now ignore that ungrouped table and show fixture-derived groups plus the knockout placeholder.

---

## 📦 v0.4.17 — 3 Jun 2026

> **World Cup tournament-format standings**
> World Cup pages now show a compact tournament format panel with fixture-derived group cards and a knockout-bracket placeholder when provider standings are unavailable, instead of presenting the competition as one league table.

---

## 📦 v0.4.16 — 3 Jun 2026

> **UTC game storage, local-time display/deadlines**
> Admin-created/edited game starts are normalized to UTC, displayed in each browser's local timezone, and prediction/result deadline checks compare UTC instants consistently.

---

## 📦 v0.4.15 — 3 Jun 2026

> **World Cup 2026 Flag Mappings**
> The new World Cup 2026 tournament now uses existing 4x3 flag assets for Bosnia-H., Congo DR, Czechia, Iraq, Sweden, and Turkey instead of falling back to missing/old crest paths.

---

## 📦 v0.4.14 — 3 Jun 2026

> **Fixture Backfill Sync**
> Admins can now backfill missing football-data fixtures into existing tournaments without deleting games or predictions. Existing fixtures are skipped by provider ID, legacy games can be matched by teams and kickoff, and the admin screen shows a confirmation plus add/match/skip summary.

---

## 📦 v0.4.13 — 3 Jun 2026

> **South Korea Standings Flag**
> South Korea now uses the uploaded 4x3 national flag in tournament league tables instead of falling back to the older crest asset.

---

## 📦 v0.4.12 — 3 Jun 2026

> **Admin Delete Controls**
> Admins can now delete tournaments, predictions, and users from dedicated admin screens with confirmation. Tournament deletes remove related games and predictions, and Admin-role predictions are excluded from public prediction lists and standings counts.

---

## 📦 v0.4.11 — 2 Jun 2026

> **Account Drawer**
> The navbar username now opens a drawer-style account panel where signed-in users can update their display name and change their password with matching frontend and backend validation.

---

## 📦 v0.4.10 — 2 Jun 2026

> **Algeria and Croatia Flags**
> Algeria and Croatia now use the uploaded 4x3 SVG flags instead of the older crest assets.

---

## 📦 v0.4.9 — 2 Jun 2026

> **World Cup Flags Refresh**
> World Cup national teams now use the uploaded 4x3 SVG flag set, including special cases for England, Scotland, Côte d’Ivoire, Korea Republic, Curaçao, USA, South Africa, and future qualifiers from the full imported set.

---

## 📦 v0.4.5 — 13 Mar 2026

> **Redesigned Mobile Game Cards**
> Game cards on mobile now match the new compact design with absolutely positioned badges, larger time text, precisely spaced crests, and team names that scale horizontally to fit instead of being truncated.

---

## 📦 v0.4.3 — 11 Mar 2026

> **Bigger, Consistent Crests**
> Team crests in game cards are now larger and display at a consistent size on both desktop and mobile.

> **Crest Fix for Special Characters**
> Fixed local crest lookup for Curaçao, Ivory Coast, and Korea Republic whose names didn't match the local files.

> **More Room for Team Names**
> Reduced spacing in the match row so longer team names fit without being cut off.

---

## 📦 v0.4.2 — 10 Mar 2026

> **World Cup 2026 Crests**
> Added local SVG crests for all 41 WC 2026 participating nations plus light and dark tournament emblems. Falls back to the API if a local file is missing.

> **Password Requirements on Register**
> When registration fails due to an invalid password, the app now shows the actual requirements — at least 6 characters with an uppercase letter, a lowercase letter, and a digit.

> **Cleaner Tournament Logos**
> Removed the circular white background from tournament emblems so themed logos display cleanly.

---

## 📦 v0.4.1 — 10 Mar 2026

> **Theme-Specific Tournament Logos**
> Tournament emblems now swap between light and dark variants when you toggle the theme. Premier League and Champions League both have dedicated dark-mode logos.

> **Refined Text Sizing**
> The enlarged font sizes from v0.4.0 now apply only to team names in game cards instead of the entire app. Everything else is back to normal size.

> **Bigger Crests & Better Spacing**
> Team crests in game cards are 25% larger, and the match row has more breathing room below the top bar.

---

## 📦 v0.4.0 — 9 Mar 2026

> **Larger Text**
> Increased text sizes across the entire app — 50% bigger on desktop, 25% on mobile. Everything from game cards to standings tables is now easier to read.

---

## 📦 v0.3.2 — 9 Mar 2026

> **Local Crests Everywhere**
> Game cards now use local SVG crests instead of external PNGs, matching the league table and standings. Crests load faster and look sharper on all screen sizes.

---

## 📦 v0.3.1 — 9 Mar 2026

> **Health Check Endpoint**
> Added `/api/health` endpoint to keep the backend alive on Render free tier.

---

## 📦 v0.3.0 — 9 Mar 2026

> **Local Team Crests**
> Team crests and tournament logos are now served locally as SVGs instead of loading from an external API. Faster rendering, no more broken images if the API is down. Falls back to the external URL if no local file exists.

> **Tournament Emblem Styling**
> Tournament logos now display on a clean white circle background so they remain visible on both dark and light themes.

---

## 📦 v0.2.2 — 8 Mar 2026

> **Global Standings**
> New page aggregating prediction results across all tournaments into a single leaderboard. Accessible from the "Global Standings" link in the navigation bar. Same drill-down feature — click any stat to see the underlying predictions.

> **Mobile Standings Fix**
> Standings table no longer overflows on small screens. Column headers are abbreviated on mobile (Pts, Out, Scr, Tot).

---

## 📦 v0.2.1 — 6 Mar 2026

> **Refreshed Card Design**
> Updated colours and fonts across all game cards. Flags are now centred next to the score, team names on the outer edges. Upcoming games show "vs" instead of dashes. SET button is now gold, FINISHED badge is solid dark.

> **Short Team Names on Mobile**
> Standings drill-down panel shows three-letter abbreviations (e.g. ARS, MCI) on small screens to prevent overflow. Game cards always show full team names.

> **Knockout Match Scoring**
> Matches that go to extra time or penalties are now scored against the 90-minute result. A 1:1 draw that goes to penalties is evaluated as 1:1 for predictions.

> **Standings Table Polish**
> Numeric columns are centred. Standings drill-down panel properly truncates long team names.

---

## 📦 v0.2.0 — 6 Mar 2026

> **Redesigned Match Cards**
> Cards now use a badge system showing your prediction status at a glance — red PREDICT, yellow PREDICTED with score, green PREDICTED! for correct guesses, and red NO PREDICTION for missed games. Darker theme with updated colours.

> **Interactive Prediction Standings**
> Click any number in the standings table to drill down into the player's predictions. See exactly which games earned points, with predicted vs actual scores colour-coded by result.

> **Correct Outcomes Column**
> Standings table now shows a separate count for predictions that got the right winner/draw but not the exact score.

---



> **Discord Notifications**
> Release notes are automatically posted to Discord after each deployment.

---

## 📦 v0.1.7 — 5 Mar 2026

> **Discord Notifications**
> Release notes are automatically posted to Discord after each deployment.

---

## 📦 v0.1.6 — 4 Mar 2026

> **Match Cards**
> Rebuilt with a new design and font. Predict button reveals score inputs on click; your prediction is shown on the card after submitting.

> **Date Filter**
> Day tabs now slide to keep the selected date centred. This Week and All still available as separate buttons.

> **Live Scores**
> Scores update every minute during live matches, not just when a game finishes. Fixed live games incorrectly showing as Finished.

> **Deployments**
> Releases now require an explicit version tag instead of triggering on every push.

---

## 📦 v0.1.3 — 4 Mar 2026

> **Automatic Score Sync**
> Backend job runs every minute and fetches the latest scores from the football API, keeping results up to date without any manual action.

---

## 📦 v0.1.0 — Initial Release

> **Predictions**
> Users can predict the score for any upcoming match. Predictions can be edited up until kick-off.

> **Standings**
> Leaderboard ranks players by points — 3 for an exact score, 1 for the correct outcome.

> **Tournaments**
> Browse tournaments, view games filtered by day, and see all predictions for a match once it kicks off.

> **Admin Panel**
> Admins can create tournaments and games manually, or import a full season from the football API including fixtures and team crests.

> **Football Standings**
> Live league table shown alongside the games for tournaments linked to an external league.

> **Theme**
> Light and dark mode with the preference saved between sessions.

> **Mobile**
> Fully responsive layout for phones and tablets.
