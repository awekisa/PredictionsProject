#!/usr/bin/env python3
from __future__ import annotations

import datetime as dt
import json
import os
import re
import sys
import unicodedata
import urllib.error
import urllib.request
from typing import Any

API_BASE = os.environ.get("PREDICTIONS_API_BASE", "https://predictionsproject.onrender.com/api")
FIFA_URL = os.environ.get("FIFA_WORLD_CUP_URL", "https://api.fifa.com/api/v3/calendar/matches?language=en&count=500&idSeason=285023")
TOURNAMENT_ID = int(os.environ.get("PREDICTIONS_TOURNAMENT_ID", "26"))
NORMAL_FINAL_STATUSES = {0}
SCHEDULED_STATUSES = {1}
LIVE_STATUSES = {3}
MANUAL_REVIEW_STATUSES = {4, 8, 9, 12}
STATUS_NAMES = {
    0: "finished",
    1: "scheduled",
    3: "live",
    4: "abandoned_or_irregular",
    8: "cancelled_or_not_played",
    9: "awarded_or_forfeit",
    12: "final_like_unverified",
}
ALIASES = {
    "bosnia h": "bosnia and herzegovina", "bosnia-h": "bosnia and herzegovina", "bosnia-h.": "bosnia and herzegovina",
    "turkey": "turkiye", "türkiye": "turkiye", "turkiye": "turkiye",
    "iran": "ir iran", "ir iran": "ir iran",
    "cape verde": "cabo verde", "cabo verde": "cabo verde",
    "ivory coast": "cote divoire", "côte d'ivoire": "cote divoire", "côte d’ivoire": "cote divoire", "cote divoire": "cote divoire",
    "korea republic": "south korea", "south korea": "south korea",
}


def fetch_json(url: str, *, method: str = "GET", body: dict[str, Any] | None = None, headers: dict[str, str] | None = None) -> Any:
    data = None if body is None else json.dumps(body).encode("utf-8")
    req_headers = {"Accept": "application/json", "User-Agent": "PredictionsProject-fixture-audit/1.0"}
    if body is not None:
        req_headers["Content-Type"] = "application/json"
    if headers:
        req_headers.update(headers)
    req = urllib.request.Request(url, data=data, headers=req_headers, method=method)
    with urllib.request.urlopen(req, timeout=90) as response:
        if response.status == 204:
            return None
        return json.load(response)


def parse_utc(value: str | None) -> dt.datetime | None:
    if not value:
        return None
    text = value
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    parsed = dt.datetime.fromisoformat(text)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc).replace(microsecond=0)


def iso_z(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def localized_text(value: Any) -> str:
    if isinstance(value, list):
        for item in value:
            if isinstance(item, dict) and item.get("Locale") in ("en-GB", "en-US", "en") and item.get("Description"):
                return str(item["Description"])
        for item in value:
            if isinstance(item, dict) and item.get("Description"):
                return str(item["Description"])
        return ""
    if isinstance(value, dict):
        for key in ("TeamName", "Name"):
            if value.get(key):
                return localized_text(value[key])
        return str(value.get("ShortClubName") or "")
    return str(value or "")


def norm_team(name: str | None) -> str:
    value = (name or "").strip().lower().replace("’", "'")
    direct = ALIASES.get(value)
    if direct:
        value = direct
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    value = re.sub(r"[^a-z0-9]+", " ", value).strip()
    value = ALIASES.get(value, value)
    return re.sub(r"[^a-z0-9]+", " ", value).strip()


def is_first_stage(match: dict[str, Any]) -> bool:
    stage = localized_text(match.get("StageName"))
    return stage.lower() in {"first stage", "group stage"}


def team_names(match: dict[str, Any]) -> tuple[str, str]:
    return localized_text(match.get("Home")), localized_text(match.get("Away"))


def calendar_score(match: dict[str, Any]) -> tuple[int, int] | tuple[str, str] | None:
    home = match.get("HomeTeamScore")
    away = match.get("AwayTeamScore")
    if home is None and isinstance(match.get("Home"), dict):
        home = match["Home"].get("Score")
    if away is None and isinstance(match.get("Away"), dict):
        away = match["Away"].get("Score")
    if home is None or away is None:
        return None
    if match.get("HomeTeamPenaltyScore") is not None or match.get("AwayTeamPenaltyScore") is not None:
        return ("manual", "penalty score present; 90-minute score not clearly separated in FIFA calendar payload")
    if match.get("FirstHalfExtraTime") is not None or match.get("SecondHalfExtraTime") is not None:
        return ("manual", "extra-time fields present; 90-minute score not clearly separated in FIFA calendar payload")
    return int(home), int(away)


def classify_match_status(status: Any) -> str:
    if status in NORMAL_FINAL_STATUSES:
        return "finished"
    if status in SCHEDULED_STATUSES:
        return "scheduled"
    if status in LIVE_STATUSES:
        return "live"
    if status in MANUAL_REVIEW_STATUSES:
        return "manual_review"
    return "unknown"


def is_normal_final_status(status: Any) -> bool:
    return classify_match_status(status) == "finished"


def is_placeholder(name: str | None) -> bool:
    n = (name or "").lower()
    return any(s in n for s in ("winner", "runner-up", "runner up", "best 3rd", "placeholder", "tbd", "to be determined"))


def auth_headers() -> dict[str, str]:
    email = os.environ.get("PREDICTIONS_ADMIN_EMAIL") or "admin@predictions.com"
    password = os.environ.get("PREDICTIONS_ADMIN_PASSWORD") or "Admin123!"
    auth_response = fetch_json(f"{API_BASE}/auth/login", method="POST", body={"email": email, "password": password})
    token = auth_response["token"]
    return {"Authorization": f"Bearer {token}"}


def build_fifa_index(fifa_matches: list[dict[str, Any]]) -> dict[tuple[str, str], list[dict[str, Any]]]:
    index: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for match in fifa_matches:
        home, away = team_names(match)
        key = (norm_team(home), norm_team(away))
        if key[0] and key[1]:
            index.setdefault(key, []).append(match)
    return index


def choose_match(game: dict[str, Any], index: dict[tuple[str, str], list[dict[str, Any]]]) -> dict[str, Any] | None:
    key = (norm_team(game.get("homeTeam")), norm_team(game.get("awayTeam")))
    candidates = index.get(key, [])
    if not candidates:
        return None
    start = parse_utc(game.get("startTime"))
    if start:
        return min(candidates, key=lambda m: abs(((parse_utc(m.get("Date")) or start) - start).total_seconds()))
    return candidates[0]


def compare(games: list[dict[str, Any]], fifa_index: dict[tuple[str, str], list[dict[str, Any]]], *, now: dt.datetime) -> dict[str, Any]:
    diffs = []
    missing = []
    manual = []
    matched = 0
    for game in games:
        match = choose_match(game, fifa_index)
        label = f"{game.get('homeTeam')} vs {game.get('awayTeam')}"
        if not match:
            missing.append({"productionGameId": game.get("id"), "match": label, "startTime": game.get("startTime")})
            continue
        matched += 1
        fifa_home, fifa_away = team_names(match)
        fifa_start = parse_utc(match.get("Date"))
        prod_start = parse_utc(game.get("startTime"))
        if fifa_start and prod_start and fifa_start != prod_start:
            diffs.append({"type": "time", "productionGameId": game.get("id"), "match": label, "fifaMatchId": match.get("IdMatch"), "before": iso_z(prod_start), "after": iso_z(fifa_start)})
        # Report potential team replacement only when production still has placeholders.
        if (is_placeholder(game.get("homeTeam")) or is_placeholder(game.get("awayTeam"))) and fifa_home and fifa_away:
            if norm_team(game.get("homeTeam")) != norm_team(fifa_home) or norm_team(game.get("awayTeam")) != norm_team(fifa_away):
                diffs.append({"type": "teams", "productionGameId": game.get("id"), "match": label, "fifaMatchId": match.get("IdMatch"), "before": {"homeTeam": game.get("homeTeam"), "awayTeam": game.get("awayTeam")}, "after": {"homeTeam": fifa_home, "awayTeam": fifa_away}})
        if game.get("isFinished") is not True:
            prod_start_for_score = prod_start or fifa_start
            if prod_start_for_score and now > prod_start_for_score:
                status = match.get("MatchStatus")
                if is_normal_final_status(status):
                    score = calendar_score(match)
                    if score is None:
                        manual.append({"productionGameId": game.get("id"), "match": label, "reason": "FIFA final status but full-time score missing", "fifaMatchId": match.get("IdMatch")})
                    elif isinstance(score[0], str):
                        manual.append({"productionGameId": game.get("id"), "match": label, "reason": score[1], "fifaMatchId": match.get("IdMatch")})
                elif now - prod_start_for_score > dt.timedelta(minutes=105):
                    status_label = STATUS_NAMES.get(int(status), str(status)) if isinstance(status, int) else str(status)
                    classification = classify_match_status(status)
                    reason = "FIFA reports live status; live score must not finalise production result" if classification == "live" else "Game is after stored/FIFA start but FIFA is not clearly final"
                    manual.append({"productionGameId": game.get("id"), "match": label, "reason": reason, "fifaMatchId": match.get("IdMatch"), "fifaStatus": status_label})
    return {"checked": len(games), "matched": matched, "missing": missing, "diffs": diffs, "manual": manual}

def main() -> int:
    report: dict[str, Any] = {"source": FIFA_URL, "productionTournamentId": TOURNAMENT_ID, "blocker": None, "timeUpdates": [], "scoreFinalisations": [], "manualReview": []}
    try:
        headers = auth_headers()
        raw_fifa = fetch_json(FIFA_URL)
        all_fifa = raw_fifa.get("Results", raw_fifa if isinstance(raw_fifa, list) else [])
        first_stage_fifa = [m for m in all_fifa if is_first_stage(m)]
        fifa_index = build_fifa_index(first_stage_fifa)
        games = fetch_json(f"{API_BASE}/admin/tournaments/{TOURNAMENT_ID}/games", headers=headers)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode(errors="replace")[:300]
        report["blocker"] = f"HTTP {exc.code} while fetching/authenticating production or FIFA data: {detail}"
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0
    except Exception as exc:
        report["blocker"] = f"{type(exc).__name__} while fetching/authenticating production or FIFA data: {str(exc)[:300]}"
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0

    now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    before = compare(games, fifa_index, now=now)
    report["productionGamesChecked"] = before["checked"]
    report["fifaFirstStageMatches"] = len(first_stage_fifa)

    # Apply time updates (and placeholder team replacements, if any) using the admin update DTO.
    try:
        by_id = {g.get("id"): g for g in games}
        team_diffs_by_id = {d["productionGameId"]: d for d in before["diffs"] if d["type"] == "teams"}
        for diff in [d for d in before["diffs"] if d["type"] == "time"]:
            game = by_id.get(diff["productionGameId"])
            if not game:
                continue
            team_diff = team_diffs_by_id.get(game.get("id"))
            home = game.get("homeTeam")
            away = game.get("awayTeam")
            if team_diff:
                home = team_diff["after"]["homeTeam"]
                away = team_diff["after"]["awayTeam"]
            body = {"homeTeam": home, "awayTeam": away, "startTime": diff["after"]}
            updated = fetch_json(f"{API_BASE}/admin/tournaments/{TOURNAMENT_ID}/games/{game.get('id')}", method="PUT", headers=headers, body=body)
            confirmed_start = parse_utc(updated.get("startTime"))
            if confirmed_start == parse_utc(diff["after"]):
                report["timeUpdates"].append({"productionGameId": game.get("id"), "match": f"{home} vs {away}", "fifaMatchId": diff.get("fifaMatchId"), "beforeUtc": diff["before"], "afterUtc": diff["after"]})
            else:
                report["manualReview"].append({"productionGameId": game.get("id"), "match": diff["match"], "reason": "Time update did not verify immediately", "intendedUtc": diff["after"], "observedUtc": updated.get("startTime")})

        # Refetch after time updates before score settling.
        games = fetch_json(f"{API_BASE}/admin/tournaments/{TOURNAMENT_ID}/games", headers=headers)
        for game in games:
            if game.get("isFinished") is True:
                continue
            match = choose_match(game, fifa_index)
            if not match:
                continue
            start = parse_utc(game.get("startTime"))
            if not start or now <= start:
                continue
            status = match.get("MatchStatus")
            label = f"{game.get('homeTeam')} vs {game.get('awayTeam')}"
            if not is_normal_final_status(status):
                continue
            score = calendar_score(match)
            if score is None:
                report["manualReview"].append({"productionGameId": game.get("id"), "match": label, "reason": "FIFA final status but full-time score missing", "fifaMatchId": match.get("IdMatch")})
                continue
            if isinstance(score[0], str):
                report["manualReview"].append({"productionGameId": game.get("id"), "match": label, "reason": score[1], "fifaMatchId": match.get("IdMatch")})
                continue
            home_goals, away_goals = score
            fetch_json(f"{API_BASE}/admin/games/{game.get('id')}/result", method="PUT", headers=headers, body={"homeGoals": home_goals, "awayGoals": away_goals})
            # Verify through full relist (GET by id may not exist on this admin route in every deploy).
            verify_games = fetch_json(f"{API_BASE}/admin/tournaments/{TOURNAMENT_ID}/games", headers=headers)
            confirmed = next((g for g in verify_games if g.get("id") == game.get("id")), None)
            if confirmed and confirmed.get("homeGoals") == home_goals and confirmed.get("awayGoals") == away_goals and confirmed.get("isFinished") is True:
                report["scoreFinalisations"].append({"productionGameId": game.get("id"), "match": f"{game.get('homeTeam')} {home_goals}-{away_goals} {game.get('awayTeam')}", "fifaMatchId": match.get("IdMatch"), "fullTimeScoreUsed": f"{home_goals}-{away_goals}"})
                games = verify_games
            else:
                report["manualReview"].append({"productionGameId": game.get("id"), "match": label, "reason": "Score finalisation did not verify on refetch", "intendedScore": f"{home_goals}-{away_goals}"})
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode(errors="replace")[:300]
        report["blocker"] = f"HTTP {exc.code} while updating/verifying production: {detail}"
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0
    except Exception as exc:
        report["blocker"] = f"{type(exc).__name__} while updating/verifying production: {str(exc)[:300]}"
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0

    final_games = fetch_json(f"{API_BASE}/admin/tournaments/{TOURNAMENT_ID}/games", headers=headers)
    after = compare(final_games, fifa_index, now=now)
    # Only carry manual-review entries from final comparison; avoids stale pre-update time diffs.
    # Suppress missing future/non-first-stage placeholders unless there were other actions; include counts for useful reports.
    report["verification"] = {"missingCount": len(after["missing"]), "diffCount": len(after["diffs"]), "manualReviewCount": len(after["manual"]) + len(report["manualReview"]), "matchedCount": after["matched"]}
    report["remainingDiffs"] = after["diffs"]
    report["manualReview"].extend(after["manual"])

    if not report["timeUpdates"] and not report["scoreFinalisations"] and not report["manualReview"] and not report["remainingDiffs"]:
        print("[SILENT]")
    else:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
