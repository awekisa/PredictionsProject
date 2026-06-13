#!/usr/bin/env python3
"""Audit PredictionsProject World Cup 2026 production fixtures against FIFA.

Safe defaults:
- Dry-run unless --apply is provided.
- Authenticates via PREDICTIONS_ADMIN_EMAIL/PREDICTIONS_ADMIN_PASSWORD.
- If those env vars are absent, allows the project-approved seeded admin fallback only when
  PREDICTIONS_ALLOW_SEEDED_ADMIN=1 is set by the operator/cron configuration.
- Never prints credentials or bearer tokens.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import unicodedata
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib import error, request

FIFA_URL = "https://api.fifa.com/api/v3/calendar/matches?language=en&count=500&idSeason=285023"
BASE_URL = "https://predictionsproject.onrender.com/api"
TOURNAMENT_ID = 26

ALIASES = {
    "bosnia h": "bosnia and herzegovina",
    "bosnia herz": "bosnia and herzegovina",
    "bosnia h": "bosnia and herzegovina",
    "bosnia h": "bosnia and herzegovina",
    "turkey": "turkiye",
    "turkiye": "turkiye",
    "iran": "ir iran",
    "cape verde": "cabo verde",
    "ivory coast": "cote divoire",
    "cote d ivoire": "cote divoire",
    "cote divoire": "cote divoire",
    "korea republic": "south korea",
}

# FIFA 2026 live feed: 0 = finished, 1 = scheduled, 3 = live/in progress.
# Never treat live status 3 as finished even when live score fields are populated.
FINISHED_STATUSES = {0}


def _description(value: Any) -> str | None:
    if isinstance(value, list):
        for item in value:
            if item.get("Locale") in {"en-GB", "en-US", "en"} and item.get("Description"):
                return item["Description"]
        return value[0].get("Description") if value else None
    if isinstance(value, dict):
        return value.get("Description")
    return value


def _team_name(team: dict[str, Any] | None) -> str | None:
    if not team:
        return None
    return _description(team.get("TeamName")) or team.get("ShortClubName") or team.get("Abbreviation")


def _normalize_name(name: str | None) -> str:
    if not name:
        return ""
    normalized = unicodedata.normalize("NFKD", str(name)).encode("ascii", "ignore").decode("ascii")
    normalized = normalized.replace("&", " and ").lower()
    normalized = re.sub(r"[^a-z0-9]+", " ", normalized).strip()
    normalized = re.sub(r"\s+", " ", normalized)
    return ALIASES.get(normalized, normalized)


def _pair_key(home: str | None, away: str | None) -> tuple[str, str]:
    return tuple(sorted((_normalize_name(home), _normalize_name(away))))  # type: ignore[return-value]


def _parse_utc(value: str | None) -> datetime | None:
    if not value:
        return None
    candidate = value[:-1] + "+00:00" if value.endswith("Z") else value
    parsed = datetime.fromisoformat(candidate)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc).replace(microsecond=0)


def _iso_z(value: datetime | None) -> str | None:
    if value is None:
        return None
    return value.astimezone(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _http_json(url: str, *, method: str = "GET", payload: Any = None, token: str | None = None) -> Any:
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Accept": "application/json"}
    if payload is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = request.Request(url, data=body, method=method, headers=headers)
    with request.urlopen(req, timeout=90) as response:
        text = response.read().decode("utf-8")
        return json.loads(text) if text else None


def _credentials(args: argparse.Namespace) -> tuple[str, str]:
    email = os.environ.get("PREDICTIONS_ADMIN_EMAIL")
    password = os.environ.get("PREDICTIONS_ADMIN_PASSWORD")
    if email and password:
        return email, password

    if args.use_seeded_admin or os.environ.get("PREDICTIONS_ALLOW_SEEDED_ADMIN") == "1":
        # Project-approved seeded admin path. Keep this local to auth; never print it.
        return "admin@predictions.com", "Admin123!"

    raise RuntimeError(
        "Missing PREDICTIONS_ADMIN_EMAIL/PREDICTIONS_ADMIN_PASSWORD. "
        "Set them, or explicitly allow the approved seeded admin fallback with "
        "PREDICTIONS_ALLOW_SEEDED_ADMIN=1 / --use-seeded-admin."
    )


def _login(args: argparse.Namespace) -> str:
    email, password = _credentials(args)
    response = _http_json(f"{args.base_url}/auth/login", method="POST", payload={"email": email, "password": password})
    token = response.get("token") or response.get("Token")
    if not token:
        raise RuntimeError("Admin login succeeded but response did not include a token.")
    return token


def _fifa_finished_score(match: dict[str, Any]) -> tuple[int, int] | None:
    home_score = match.get("HomeTeamScore")
    away_score = match.get("AwayTeamScore")
    if home_score is None and isinstance(match.get("Home"), dict):
        home_score = match["Home"].get("Score")
    if away_score is None and isinstance(match.get("Away"), dict):
        away_score = match["Away"].get("Score")
    if home_score is None or away_score is None:
        return None

    status = match.get("MatchStatus")
    match_start = _parse_utc(match.get("Date"))
    if match_start and datetime.now(timezone.utc) - match_start < timedelta(minutes=115):
        return None
    if status in FINISHED_STATUSES:
        return int(home_score), int(away_score)
    return None


def _build_fifa_index(fifa_url: str) -> tuple[list[dict[str, Any]], dict[tuple[str, str], dict[str, Any]], list[Any]]:
    data = _http_json(fifa_url)
    first_stage = [match for match in data.get("Results", []) if _description(match.get("StageName")) == "First Stage"]
    by_pair: dict[tuple[str, str], dict[str, Any]] = {}
    duplicates = []
    for match in first_stage:
        home = _team_name(match.get("Home"))
        away = _team_name(match.get("Away"))
        if not home or not away:
            continue
        item = {
            "id": match.get("IdMatch"),
            "home": home,
            "away": away,
            "date": _parse_utc(match.get("Date")),
            "group": _description(match.get("GroupName")),
            "score": _fifa_finished_score(match),
            "raw": match,
        }
        key = _pair_key(home, away)
        if key in by_pair:
            duplicates.append({"key": key, "first": by_pair[key], "second": item})
        by_pair[key] = item
    return first_stage, by_pair, duplicates


def _fetch_games(base_url: str, token: str) -> list[dict[str, Any]]:
    return _http_json(f"{base_url}/admin/tournaments/{TOURNAMENT_ID}/games", token=token)


def _compare(games: list[dict[str, Any]], fifa_by_pair: dict[tuple[str, str], dict[str, Any]]) -> dict[str, Any]:
    matched = 0
    time_diffs = []
    score_finals = []
    unmatched = []
    for game in games:
        fifa = fifa_by_pair.get(_pair_key(game.get("homeTeam"), game.get("awayTeam")))
        if not fifa:
            unmatched.append(game)
            continue
        matched += 1
        stored_start = _parse_utc(game.get("startTime"))
        if stored_start != fifa["date"]:
            time_diffs.append((game, fifa, stored_start, fifa["date"]))
        if fifa.get("score") and not game.get("isFinished"):
            score_finals.append((game, fifa, fifa["score"]))
    return {"matched": matched, "time_diffs": time_diffs, "score_finals": score_finals, "unmatched": unmatched}


def _update_time(base_url: str, token: str, game: dict[str, Any], fifa: dict[str, Any]) -> None:
    payload = {"homeTeam": game["homeTeam"], "awayTeam": game["awayTeam"], "startTime": _iso_z(fifa["date"])}
    _http_json(f"{base_url}/admin/tournaments/{TOURNAMENT_ID}/games/{game['id']}", method="PUT", payload=payload, token=token)


def _finalise_score(base_url: str, token: str, game: dict[str, Any], score: tuple[int, int]) -> None:
    _http_json(
        f"{base_url}/admin/games/{game['id']}/result",
        method="PUT",
        payload={"homeGoals": score[0], "awayGoals": score[1]},
        token=token,
    )


def _manual_review_items(unmatched: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "reason": "production game not matched to FIFA First Stage fixture",
            "gameId": game.get("id"),
            "teams": f"{game.get('homeTeam')} vs {game.get('awayTeam')}",
            "storedUtc": _iso_z(_parse_utc(game.get("startTime"))),
            "isFinished": game.get("isFinished"),
        }
        for game in unmatched
    ]


def run(args: argparse.Namespace) -> dict[str, Any]:
    first_stage, fifa_by_pair, duplicates = _build_fifa_index(args.fifa_url)
    token = _login(args)
    games = _fetch_games(args.base_url, token)
    initial = _compare(games, fifa_by_pair)

    report: dict[str, Any] = {
        "source": args.fifa_url,
        "production_games_checked": len(games),
        "fifa_first_stage_matches": len(first_stage),
        "matched_first_stage_games": initial["matched"],
        "time_updates": [],
        "score_finalisations": [],
        "manual_review": [],
        "initial_diff_count": len(initial["time_diffs"]),
        "initial_score_finalisation_count": len(initial["score_finals"]),
    }
    if duplicates:
        report["manual_review"].append({"reason": "duplicate FIFA team-pair keys", "count": len(duplicates)})

    if args.apply:
        for game, fifa, old_start, new_start in list(initial["time_diffs"]):
            _update_time(args.base_url, token, game, fifa)
            games = _fetch_games(args.base_url, token)
            refetched = next(item for item in games if item["id"] == game["id"])
            report["time_updates"].append(
                {
                    "gameId": game["id"],
                    "teams": f"{game['homeTeam']} vs {game['awayTeam']}",
                    "beforeUtc": _iso_z(old_start),
                    "afterUtc": _iso_z(_parse_utc(refetched.get("startTime"))),
                    "fifaUtc": _iso_z(new_start),
                }
            )

        after_times = _compare(games, fifa_by_pair)
        for game, _fifa, score in list(after_times["score_finals"]):
            _finalise_score(args.base_url, token, game, score)
            games = _fetch_games(args.base_url, token)
            refetched = next(item for item in games if item["id"] == game["id"])
            report["score_finalisations"].append(
                {
                    "gameId": game["id"],
                    "teams": f"{game['homeTeam']} vs {game['awayTeam']}",
                    "before": {"homeGoals": game.get("homeGoals"), "awayGoals": game.get("awayGoals"), "isFinished": game.get("isFinished")},
                    "after": {"homeGoals": refetched.get("homeGoals"), "awayGoals": refetched.get("awayGoals"), "isFinished": refetched.get("isFinished")},
                }
            )

    final_games = _fetch_games(args.base_url, token)
    final = _compare(final_games, fifa_by_pair)
    report["final_missing_count"] = len(final["unmatched"])
    report["final_diff_count"] = len(final["time_diffs"])
    report["final_score_finalisation_count"] = len(final["score_finals"])
    report["manual_review"].extend(_manual_review_items(final["unmatched"]))

    if not args.apply:
        report["pending_time_diffs"] = [
            {
                "gameId": game["id"],
                "teams": f"{game['homeTeam']} vs {game['awayTeam']}",
                "storedUtc": _iso_z(old_start),
                "fifaUtc": _iso_z(new_start),
            }
            for game, _fifa, old_start, new_start in final["time_diffs"]
        ]
        report["pending_score_finalisations"] = [
            {"gameId": game["id"], "teams": f"{game['homeTeam']} vs {game['awayTeam']}", "score": score}
            for game, _fifa, score in final["score_finals"]
        ]

    return report


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit World Cup 2026 production fixtures against FIFA.")
    parser.add_argument("--apply", action="store_true", help="Apply eligible time updates and score finalisations; default is dry-run.")
    parser.add_argument("--use-seeded-admin", action="store_true", help="Explicitly allow the project-approved seeded admin fallback when env credentials are absent.")
    parser.add_argument("--base-url", default=BASE_URL)
    parser.add_argument("--fifa-url", default=FIFA_URL)
    args = parser.parse_args()

    try:
        report = run(args)
    except (error.HTTPError, error.URLError, TimeoutError, RuntimeError) as exc:
        print(json.dumps({"blocker": f"{type(exc).__name__}: {exc}", "source": args.fifa_url}, ensure_ascii=False, indent=2))
        return 2

    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
