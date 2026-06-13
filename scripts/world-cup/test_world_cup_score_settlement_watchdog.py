#!/usr/bin/env python3
import datetime as dt
import importlib.util
import pathlib
import sys
import unittest

SCRIPT_PATH = pathlib.Path(__file__).with_name("world-cup-score-settlement-watchdog.py")
spec = importlib.util.spec_from_file_location("world_cup_score_settlement_watchdog", SCRIPT_PATH)
assert spec is not None
watchdog = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = watchdog
assert spec.loader is not None
spec.loader.exec_module(watchdog)


class FifaStatusSettlementTests(unittest.TestCase):
    def test_status_3_with_live_score_is_not_settleable(self):
        match = {
            "MatchStatus": 3,
            "MatchTime": "43'",
            "HomeTeamScore": 0,
            "AwayTeamScore": 1,
            "ResultType": 0,
        }
        start = dt.datetime(2026, 6, 13, 19, 0, tzinfo=dt.timezone.utc)
        now = dt.datetime(2026, 6, 13, 21, 0, tzinfo=dt.timezone.utc)

        decision = watchdog.settlement_decision(match, start, now)

        self.assertFalse(decision.should_settle)
        self.assertEqual(decision.classification, "live")
        self.assertIn("live", decision.reason.lower())

    def test_status_0_with_score_after_elapsed_guard_is_settleable(self):
        match = {
            "MatchStatus": 0,
            "MatchTime": "98'",
            "HomeTeamScore": 2,
            "AwayTeamScore": 0,
            "ResultType": 1,
        }
        start = dt.datetime(2026, 6, 11, 19, 0, tzinfo=dt.timezone.utc)
        now = dt.datetime(2026, 6, 11, 21, 0, tzinfo=dt.timezone.utc)

        decision = watchdog.settlement_decision(match, start, now)

        self.assertTrue(decision.should_settle)
        self.assertEqual(decision.classification, "finished")

    def test_status_1_is_scheduled_not_settleable(self):
        match = {
            "MatchStatus": 1,
            "MatchTime": None,
            "HomeTeamScore": None,
            "AwayTeamScore": None,
            "ResultType": 0,
        }
        start = dt.datetime(2026, 6, 13, 22, 0, tzinfo=dt.timezone.utc)
        now = dt.datetime(2026, 6, 13, 21, 0, tzinfo=dt.timezone.utc)

        decision = watchdog.settlement_decision(match, start, now)

        self.assertFalse(decision.should_settle)
        self.assertEqual(decision.classification, "scheduled")

    def test_special_status_with_score_requires_manual_review(self):
        match = {
            "MatchStatus": 9,
            "MatchTime": "0'",
            "HomeTeamScore": 0,
            "AwayTeamScore": 3,
            "ResultType": 12,
        }
        start = dt.datetime(2026, 6, 13, 19, 0, tzinfo=dt.timezone.utc)
        now = dt.datetime(2026, 6, 13, 21, 0, tzinfo=dt.timezone.utc)

        decision = watchdog.settlement_decision(match, start, now)

        self.assertFalse(decision.should_settle)
        self.assertEqual(decision.classification, "manual_review")


if __name__ == "__main__":
    unittest.main()
