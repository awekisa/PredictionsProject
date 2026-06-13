#!/usr/bin/env python3
"""World Cup 2026 production score settlement watchdog.

Fetches unfinished production games for tournament 26, compares after-start games to
FIFA's official calendar endpoint, and finalises only clearly completed matches.
Never prints credentials/tokens. Defaults to the app's approved seeded-admin path;
PREDICTIONS_ADMIN_EMAIL/PREDICTIONS_ADMIN_PASSWORD override it when configured.
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


@dataclass(frozen=True)
class SettlementDecision:
    should_settle: bool
    classification: str
    reason: str


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


def settlement_decision(match: dict[str, Any], start: dt.datetime, now: dt.datetime) -> SettlementDecision:
    status = match.get("MatchStatus")
    classification = classify_match_status(status)
    elapsed = now - start

    if classification == "scheduled":
        return SettlementDecision(False, classification, "FIFA match is scheduled/upcoming")
    if classification == "live":
        return SettlementDecision(False, classification, "FIFA match is live; live score must not finalise production result")
    if classification == "manual_review":
        return SettlementDecision(False, classification, "FIFA status requires manual result review")
    if classification != "finished":
        return SettlementDecision(False, classification, "FIFA status is unknown; manual review required")
    if elapsed < dt.timedelta(minutes=115):
        return SettlementDecision(False, classification, "FIFA final status observed but elapsed-time guard has not passed")
    if calendar_score(match) is None:
        return SettlementDecision(False, classification, "FIFA final status observed but score is missing")
    return SettlementDecision(True, classification, "FIFA finished status with score after elapsed-time guard")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-base", default=os.environ.get("PREDICTIONS_API_BASE", DEFAULT_API_BASE))
    parser.add_argument("--fifa-url", default=os.environ.get("FIFA_WORLD_CUP_URL", DEFAULT_FIFA_URL))
    parser.add_argument("--tournament-id", type=int, default=int(os.environ.get("PREDICTIONS_TOURNAMENT_ID", DEFAULT_TOURNAMENT_ID)))
    parser.add_argument("--dry-run", action="store_true", help="Report intended settlements without PUTting results")
    args = parser.parse_args()
    email = os.environ.get("PREDICTIONS_ADMIN_EMAIL", "admin@predictions.com")
    password = os.environ.get("PREDICTIONS_ADMIN_PASSWORD", "Admin123!")
    output: dict[str, Any] = {"blocker": None, "settled": [], "manual": []}
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
        elapsed = now - start
        decision = settlement_decision(fifa_match, start, now)
        if not decision.should_settle:
            if decision.classification in {"live", "manual_review", "unknown"} and elapsed > dt.timedelta(minutes=105):
                output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": decision.reason, "fifaDataObserved": {"idMatch": fifa_match.get("IdMatch"), "matchStatus": status, "statusName": status_name, "matchTime": fifa_match.get("MatchTime"), "homeTeamScore": fifa_match.get("HomeTeamScore"), "awayTeamScore": fifa_match.get("AwayTeamScore"), "resultType": fifa_match.get("ResultType")}})
            continue
        score = calendar_score(fifa_match)
        if score is None:
            output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": "FIFA final status but full-time score missing", "fifaDataObserved": {"idMatch": fifa_match.get("IdMatch"), "matchStatus": status, "statusName": status_name}})
            continue
        if isinstance(score[0], str):
            output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": score[1], "fifaDataObserved": {"idMatch": fifa_match.get("IdMatch"), "matchStatus": status, "statusName": status_name, "homeTeamScore": fifa_match.get("HomeTeamScore"), "awayTeamScore": fifa_match.get("AwayTeamScore"), "homePenalty": fifa_match.get("HomeTeamPenaltyScore"), "awayPenalty": fifa_match.get("AwayTeamPenaltyScore")}})
            continue
        home_goals, away_goals = score
        already_correct = game.get("isFinished") is True and game.get("homeGoals") == home_goals and game.get("awayGoals") == away_goals
        if already_correct:
            continue
        previous_score = None
        action = "settled"
        if game.get("isFinished") is True:
            previous_score = f"{game.get('homeGoals')}-{game.get('awayGoals')}"
            action = "corrected"
        if args.dry_run:
            output["settled"].append({"match": f"{game.get('homeTeam')} {home_goals}-{away_goals} {game.get('awayTeam')}", "fifaMatchStatus": status_name, "fullTimeScoreUsed": f"{home_goals}-{away_goals}", "previousProductionScore": previous_score, "productionGameId": game.get("id"), "action": action, "dryRun": True})
            continue
        fetch_json(f"{args.api_base}/admin/games/{game.get('id')}/result", method="PUT", headers=auth, body={"homeGoals": home_goals, "awayGoals": away_goals})
        confirmed = fetch_json(f"{args.api_base}/admin/tournaments/{args.tournament_id}/games/{game.get('id')}", headers=auth)
        if confirmed.get("homeGoals") == home_goals and confirmed.get("awayGoals") == away_goals and confirmed.get("isFinished") is True:
            output["settled"].append({"match": f"{game.get('homeTeam')} {home_goals}-{away_goals} {game.get('awayTeam')}", "fifaMatchStatus": status_name, "fullTimeScoreUsed": f"{home_goals}-{away_goals}", "previousProductionScore": previous_score, "productionGameId": game.get("id"), "action": action, "confirmedIsFinished": True})
        else:
            output["manual"].append({"match": match_label, "productionGameId": game.get("id"), "reason": "SetResult response did not verify on refetch", "fifaDataObserved": {"intendedScore": f"{home_goals}-{away_goals}", "confirmed": {field: confirmed.get(field) for field in ("homeGoals", "awayGoals", "isFinished")}}})
    if not output["blocker"] and not output["settled"] and not output["manual"]:
        print("[SILENT]")
    else:
        print(json.dumps(output, ensure_ascii=False, indent=2))
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
