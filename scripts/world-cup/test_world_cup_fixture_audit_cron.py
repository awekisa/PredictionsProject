#!/usr/bin/env python3
import importlib.util
import pathlib
import sys
import unittest
from unittest.mock import patch

SCRIPT_PATH = pathlib.Path(__file__).with_name("world-cup-fixture-audit-cron.py")
spec = importlib.util.spec_from_file_location("world_cup_fixture_audit_cron", SCRIPT_PATH)
assert spec is not None
cron = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = cron
assert spec.loader is not None
spec.loader.exec_module(cron)


def text(value: str):
    return [{"Locale": "en-GB", "Description": value}]


def team(name: str):
    return {"TeamName": text(name)}


class KnockoutFixtureCreationTests(unittest.TestCase):
    def test_candidate_skips_first_stage_and_undetermined_knockout_teams(self):
        first_stage = {
            "StageName": text("First Stage"),
            "Date": "2026-06-11T19:00:00Z",
            "Home": team("Mexico"),
            "Away": team("South Africa"),
        }
        blank_knockout = {
            "StageName": text("Round of 32"),
            "Date": "2026-06-30T21:00:00Z",
            "Home": team(""),
            "Away": team(""),
        }
        placeholder_knockout = {
            "StageName": text("Round of 32"),
            "Date": "2026-06-30T21:00:00Z",
            "Home": team("Winner Group A"),
            "Away": team("Runner-up Group B"),
        }

        self.assertIsNone(cron.knockout_fixture_candidate(first_stage))
        self.assertIsNone(cron.knockout_fixture_candidate(blank_knockout))
        self.assertIsNone(cron.knockout_fixture_candidate(placeholder_knockout))

    def test_create_missing_knockout_fixtures_creates_only_known_non_existing_games(self):
        existing_games = [
            {"homeTeam": "South Africa", "awayTeam": "Canada", "startTime": "2026-06-28T19:00:00Z"},
        ]
        matches = [
            {
                "IdMatch": 1,
                "StageName": text("Round of 32"),
                "Date": "2026-06-28T19:00:00Z",
                "Home": team("South Africa"),
                "Away": team("Canada"),
            },
            {
                "IdMatch": 2,
                "StageName": text("Round of 32"),
                "Date": "2026-06-29T17:00:00Z",
                "Home": team("Brazil"),
                "Away": team("Japan"),
            },
            {
                "IdMatch": 3,
                "StageName": text("Round of 32"),
                "Date": "2026-06-30T21:00:00Z",
                "Home": team(""),
                "Away": team(""),
            },
        ]

        created_payloads = []

        def fake_fetch_json(url, *, method="GET", body=None, headers=None):
            self.assertEqual(method, "POST")
            self.assertIsInstance(body, dict)
            assert isinstance(body, dict)
            created_payloads.append(body)
            return {"id": 99, **body}

        with patch.object(cron, "fetch_json", side_effect=fake_fetch_json):
            result = cron.create_missing_knockout_fixtures(existing_games, matches, headers={"Authorization": "Bearer test"})

        self.assertEqual(result["skippedExisting"], 1)
        self.assertEqual(result["skippedUndetermined"], 1)
        self.assertEqual(len(result["created"]), 1)
        self.assertEqual(result["created"][0]["homeTeam"], "Brazil")
        self.assertEqual(result["created"][0]["awayTeam"], "Japan")
        self.assertEqual(result["created"][0]["productionGameId"], 99)
        self.assertEqual(created_payloads, [{"homeTeam": "Brazil", "awayTeam": "Japan", "startTime": "2026-06-29T17:00:00Z"}])

    def test_dry_run_reports_would_create_without_posting(self):
        matches = [
            {
                "IdMatch": 2,
                "StageName": text("Round of 32"),
                "Date": "2026-06-29T17:00:00Z",
                "Home": team("Brazil"),
                "Away": team("Japan"),
            },
        ]

        with patch.object(cron, "fetch_json") as fetch_json:
            result = cron.create_missing_knockout_fixtures([], matches, headers={}, dry_run=True)

        fetch_json.assert_not_called()
        self.assertEqual(result["created"], [])
        self.assertEqual(len(result["wouldCreate"]), 1)
        self.assertEqual(result["wouldCreate"][0]["fifaMatchId"], 2)

    def test_existing_knockout_fixture_with_small_time_drift_prevents_duplicate_creation(self):
        existing_games = [
            {"id": 2383, "homeTeam": "Mexico", "awayTeam": "Ecuador", "startTime": "2026-07-01T01:00:00Z"},
        ]
        matches = [
            {
                "IdMatch": 99,
                "StageName": text("Round of 32"),
                "Date": "2026-07-01T02:00:00Z",
                "Home": team("Mexico"),
                "Away": team("Ecuador"),
            },
        ]

        with patch.object(cron, "fetch_json") as fetch_json:
            result = cron.create_missing_knockout_fixtures(existing_games, matches, headers={})

        fetch_json.assert_not_called()
        self.assertEqual(result["skippedExisting"], 1)
        self.assertEqual(result["created"], [])
        self.assertEqual(result["wouldCreate"], [])


if __name__ == "__main__":
    unittest.main()
