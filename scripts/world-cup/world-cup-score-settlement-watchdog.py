#!/usr/bin/env python3
"""World Cup 2026 FIFA score/status sync.

Fetches production games for tournament 26, compares started games to FIFA's
official calendar endpoint, and finalises production scores only when FIFA
clearly reports a normal finished match. Live FIFA scores are intentionally not
persisted as final game scores.

For knockout/elimination games, prediction scoring should use the full-time
(90-minute) score, not extra-time/penalty shootout scores. If FIFA exposes
extra-time/penalty fields and a clear full-time score is not available, the
script refuses to finalise and reports manual review instead of guessing.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import sys
import unicodedata
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any

DEFAULT_API_BASE = "https://predictionsproject.onrender.com/api"
DEFAULT_FIFA_URL = "https://api.fifa.com/api/v3/calendar/matches?language=en&count=500&idSeason=285023"
DEFAULT_TOURNAMENT_ID = 26
FINAL_STATUSES = {0}
SCHEDULED_STATUSES = {1}
LIVE_STATUSES = {3}
MANUAL_REVIEW_STATUSES = {4, 8, 9, 12}
MIN_FINAL_ELAPSED_MINUTES = 115
STATUS_NAMES = {
    0: "finished",
    1: "scheduled",
    3: "live",
    4: "abandoned_or_irregular",
    8: "cancelled_or_not_played",
    9: "awarded_or_forfeit",
    12: "final_like_unverified",
}
ALIASES = {"bosnia h": "bosnia and herzegovina", "bosnia-h": "bosnia and herzegovina", "bosnia-h.": "bosnia and herzegovina", "turkey": "turkiye", "türkiye": "turkiye", "iran": "ir iran", "cape verde": "cabo verde", "ivory coast": "cote divoire", "côte d'ivoire": "cote divoire", "côte d’ivoire": "cote divoire", "korea republic": "south korea"}


def fetch_json(url: str, *, method: str = "GET", body: dict[str, Any] | None = None, headers: dict[str, str] | None = None) -> Any:
    data = None if body is None else json.dumps(body).encode("utf-8")
    req_headers = {"Accept": "application/json"}
    if body is not None:
        req_headers["Content-Type"] = "application/json"
    if headers:
        req_headers.update(headers)
    req = urllib.request.Request(url, data=data, headers=req_headers, method=method)
    with urllib.request.urlopen(req, timeout=60) as response:
        if response.status == 204:
            return None
        return json.load(response)


def parse_utc(value: str | None) -> dt.datetime | None:
    if not value:
        return None
    if value.endswith("Z"):
        value = value[:-1] + "+00:00"
    parsed = dt.datetime.fromisoformat(value)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


def localized_text(value: Any) -> str:
    if isinstance(value, list):
        for item in value:
            if isinstance(item, dict) and item.get("Description"):
                return str(item["Description"])
        return ""
    if isinstance(value, dict):
        if value.get("TeamName"):
            return localized_text(value["TeamName"])
        if value.get("Name"):
            return localized_text(value["Name"])
        return str(value.get("ShortClubName") or "")
    return str(value or "")


def norm_team(name: str | None) -> str:
    value = (name or "").strip().lower().replace("’", "'")
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    value = re.sub(r"[^a-z0-9]+", " ", value).strip()
    value = ALIASES.get(value, value)
    return re.sub(r"[^a-z0-9]+", " ", value).strip()


def score_value(match: dict[str, Any], side: str) -> Any:
    field = f"{side}TeamScore"
    value = match.get(field)
    if value is None and isinstance(match.get(side), dict):
        value = match[side].get("Score")
    return value


def has_extra_or_penalty_data(match: dict[str, Any]) -> bool:
    return any(match.get(field) is not None for field in (
        "HomeTeamPenaltyScore",
        "AwayTeamPenaltyScore",
        "FirstHalfExtraTime",
        "SecondHalfExtraTime",
    ))


def _period_score(period: Any) -> tuple[int, int] | None:
    if not isinstance(period, dict):
        return None
    home = period.get("HomeTeamScore", period.get("HomeScore"))
    away = period.get("AwayTeamScore", period.get("AwayScore"))
    if home is None or away is None:
        return None
    return int(home), int(away)


def full_time_score(match: dict[str, Any]) -> tuple[int, int] | None:
    """Return the 90-minute score when FIFA separates periods for knockouts."""
    first = _period_score(match.get("FirstHalfTime"))
    second = _period_score(match.get("SecondHalfTime"))
    if first and second:
        return first[0] + second[0], first[1] + second[1]
    return None


def fifa_score_for_sync(match: dict[str, Any], *, finalising: bool) -> tuple[int, int] | tuple[str, str] | None:
    if finalising and has_extra_or_penalty_data(match):
        ft = full_time_score(match)
        if ft is not None:
            return ft
        return ("manual", "extra-time/penalty fields present; no clear 90-minute full-time score in FIFA payload")
    home = score_value(match, "Home")
    away = score_value(match, "Away")
    if home is None or away is None:
        return None
    return int(home), int(away)


def classify_match_status(status: Any) -> str:
    if status in FINAL_STATUSES:
        return "finished"
    if status in SCHEDULED_STATUSES:
        return "scheduled"
    if status in LIVE_STATUSES:
        return "live"
    if status in MANUAL_REVIEW_STATUSES:
        return "manual_review"
    return "unknown"


@dataclass(frozen=True)
class ScoreSyncDecision:
    should_sync: bool
    is_finished: bool
    classification: str
    reason: str
    score: tuple[int, int] | None = None


def score_sync_decision(match: dict[str, Any]) -> ScoreSyncDecision:
    status = match.get("MatchStatus")
    classification = classify_match_status(status)
    if classification == "scheduled":
        return ScoreSyncDecision(False, False, classification, "FIFA match is scheduled/upcoming")
    if classification in {"manual_review", "unknown"}:
        return ScoreSyncDecision(False, False, classification, "FIFA status requires manual result review")

    is_finished = classification == "finished"
    score = fifa_score_for_sync(match, finalising=is_finished)
    if score is None:
        return ScoreSyncDecision(False, is_finished, classification, "FIFA status is syncable but score is missing")
    if isinstance(score[0], str):
        return ScoreSyncDecision(False, is_finished, classification, str(score[1]))
    home_goals = int(score[0])
    away_goals = int(score[1])
    return ScoreSyncDecision(True, is_finished, classification, "Sync FIFA score/status", (home_goals, away_goals))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-base", default=os.environ.get("PREDICTIONS_API_BASE", DEFAULT_API_BASE))
    parser.add_argument("--fifa-url", default=os.environ.get("FIFA_WORLD_CUP_URL", DEFAULT_FIFA_URL))
    parser.add_argument("--tournament-id", type=int, default=int(os.environ.get("PREDICTIONS_TOURNAMENT_ID", DEFAULT_TOURNAMENT_ID)))
    parser.add_argument("--dry-run", action="store_true", help="Report intended score/status syncs without PUTting changes")
    parser.add_argument("--use-seeded-admin", action="store_true", help="Allow the approved seeded admin fallback credentials")
    args = parser.parse_args()

    email = os.environ.get("PREDICTIONS_ADMIN_EMAIL")
    password = os.environ.get("PREDICTIONS_ADMIN_PASSWORD")
    if not email or not password:
        if args.use_seeded_admin or os.environ.get("PREDICTIONS_ALLOW_SEEDED_ADMIN") == "1":
            email = "admin@predictions.com"
            password = "Admin123!"
        else:
            print(json.dumps({"blocker": "Missing PREDICTIONS_ADMIN_EMAIL/PREDICTIONS_ADMIN_PASSWORD; pass --use-seeded-admin to use approved seeded admin fallback"}))
            return 0

    output: dict[str, Any] = {"blocker": None, "synced": [], "manual": []}
    try:
        auth_response = fetch_json(f"{args.api_base}/auth/login", method="POST", body={"email": email, "password": password})
        token = auth_response["token"]
        auth = {"Authorization": f"Bearer {token}"}
        games = fetch_json(f"{args.api_base}/admin/tournaments/{args.tournament_id}/games", headers=auth)
        fifa_response = fetch_json(args.fifa_url)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode(errors="replace")[:300]
        output["blocker"] = f"HTTP {exc.code} while fetching/authenticating production or FIFA data: {detail}"
        print(json.dumps(output, ensure_ascii=False))
        return 0
    except Exception as exc:
        output["blocker"] = f"{type(exc).__name__} while fetching/authenticating production or FIFA data: {str(exc)[:300]}"
        print(json.dumps(output, ensure_ascii=False))
        return 0

    now = dt.datetime.now(dt.timezone.utc)
    fifa_matches = fifa_response.get("Results", fifa_response if isinstance(fifa_response, list) else [])
    by_pair: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for match in fifa_matches:
        key = (norm_team(localized_text(match.get("Home"))), norm_team(localized_text(match.get("Away"))))
        if key[0] and key[1]:
            by_pair.setdefault(key, []).append(match)

    for game in games:
        start = parse_utc(game.get("startTime"))
        if not start or now <= start:
            continue
        match_label = f"{game.get('homeTeam')} vs {game.get('awayTeam')}"
        key = (norm_team(game.get("homeTeam")), norm_team(game.get("awayTeam")))
        matches = by_pair.get(key, [])
        if not matches:
            output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": "No FIFA match found by normalized home/away team pair", "fifaDataObserved": None})
            continue
        fifa_match = min(matches, key=lambda item: abs(((parse_utc(item.get("Date")) or start) - start).total_seconds()))
        status = fifa_match.get("MatchStatus")
        status_name = STATUS_NAMES.get(status, str(status))
        decision = score_sync_decision(fifa_match)
        if not decision.should_sync:
            if decision.classification in {"manual_review", "unknown"}:
                output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": decision.reason, "fifaDataObserved": {"idMatch": fifa_match.get("IdMatch"), "matchStatus": status, "statusName": status_name, "matchTime": fifa_match.get("MatchTime"), "homeTeamScore": fifa_match.get("HomeTeamScore"), "awayTeamScore": fifa_match.get("AwayTeamScore"), "resultType": fifa_match.get("ResultType")}})
            continue

        home_goals, away_goals = decision.score or (None, None)
        already_correct = (
            game.get("homeGoals") == home_goals
            and game.get("awayGoals") == away_goals
            and game.get("isFinished") is decision.is_finished
            and game.get("fifaMatchStatus") == status
            and (game.get("fifaMatchTime") or None) == (fifa_match.get("MatchTime") or None)
        )
        if already_correct:
            continue

        payload = {
            "homeGoals": home_goals,
            "awayGoals": away_goals,
            "isFinished": decision.is_finished,
            "fifaMatchStatus": status,
            "fifaMatchTime": fifa_match.get("MatchTime"),
        }
        action = "finalised" if decision.is_finished else "live_sync"
        summary = {"match": f"{game.get('homeTeam')} {home_goals}-{away_goals} {game.get('awayTeam')}", "fifaMatchStatus": status_name, "fifaMatchTime": fifa_match.get("MatchTime"), "productionGameId": game.get("id"), "action": action, "previousProduction": {field: game.get(field) for field in ("homeGoals", "awayGoals", "isFinished", "fifaMatchStatus", "fifaMatchTime")}}
        if args.dry_run:
            summary["dryRun"] = True
            output["synced"].append(summary)
            continue

        fetch_json(f"{args.api_base}/admin/games/{game.get('id')}/score-sync", method="PUT", headers=auth, body=payload)
        confirmed = fetch_json(f"{args.api_base}/admin/tournaments/{args.tournament_id}/games/{game.get('id')}", headers=auth)
        if confirmed.get("homeGoals") == home_goals and confirmed.get("awayGoals") == away_goals and confirmed.get("isFinished") is decision.is_finished and confirmed.get("fifaMatchStatus") == status:
            summary["confirmed"] = {field: confirmed.get(field) for field in ("homeGoals", "awayGoals", "isFinished", "fifaMatchStatus", "fifaMatchTime")}
            output["synced"].append(summary)
        else:
            output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": "Score sync response did not verify on refetch", "intended": payload, "confirmed": {field: confirmed.get(field) for field in ("homeGoals", "awayGoals", "isFinished", "fifaMatchStatus", "fifaMatchTime")}})

    if not output["blocker"] and not output["synced"] and not output["manual"]:
        print("[SILENT]")
    else:
        print(json.dumps(output, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
