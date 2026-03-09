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
