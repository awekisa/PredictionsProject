#!/usr/bin/env python3
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


class FifaStatusScoreSyncTests(unittest.TestCase):
    def test_status_3_with_live_score_syncs_without_finishing(self):
        match = {
            "MatchStatus": 3,
            "MatchTime": "43'",
            "HomeTeamScore": 0,
            "AwayTeamScore": 1,
            "ResultType": 0,
        }

        decision = watchdog.score_sync_decision(match)

        self.assertTrue(decision.should_sync)
        self.assertFalse(decision.is_finished)
        self.assertEqual(decision.classification, "live")
        self.assertEqual(decision.score, (0, 1))

    def test_status_0_with_score_syncs_and_finishes(self):
        match = {
            "MatchStatus": 0,
            "MatchTime": "98'",
            "HomeTeamScore": 2,
            "AwayTeamScore": 0,
            "ResultType": 1,
        }

        decision = watchdog.score_sync_decision(match)

        self.assertTrue(decision.should_sync)
        self.assertTrue(decision.is_finished)
        self.assertEqual(decision.classification, "finished")
        self.assertEqual(decision.score, (2, 0))

    def test_status_1_is_scheduled_not_syncable_without_score(self):
        match = {
            "MatchStatus": 1,
            "MatchTime": None,
            "HomeTeamScore": None,
            "AwayTeamScore": None,
            "ResultType": 0,
        }

        decision = watchdog.score_sync_decision(match)

        self.assertFalse(decision.should_sync)
        self.assertFalse(decision.is_finished)
        self.assertEqual(decision.classification, "scheduled")

    def test_special_status_with_score_requires_manual_review(self):
        match = {
            "MatchStatus": 9,
            "MatchTime": "0'",
            "HomeTeamScore": 0,
            "AwayTeamScore": 3,
            "ResultType": 12,
        }

        decision = watchdog.score_sync_decision(match)

        self.assertFalse(decision.should_sync)
        self.assertEqual(decision.classification, "manual_review")

    def test_elimination_extra_time_final_uses_full_time_score_when_available(self):
        match = {
            "MatchStatus": 0,
            "MatchTime": "120'",
            "HomeTeamScore": 2,
            "AwayTeamScore": 1,
            "FirstHalfTime": {"HomeTeamScore": 0, "AwayTeamScore": 0},
            "SecondHalfTime": {"HomeTeamScore": 1, "AwayTeamScore": 1},
            "FirstHalfExtraTime": {"HomeTeamScore": 1, "AwayTeamScore": 0},
        }

        decision = watchdog.score_sync_decision(match)

        self.assertTrue(decision.should_sync)
        self.assertTrue(decision.is_finished)
        self.assertEqual(decision.score, (1, 1))

    def test_elimination_extra_time_final_requires_manual_review_without_full_time_score(self):
        match = {
            "MatchStatus": 0,
            "MatchTime": "120'",
            "HomeTeamScore": 2,
            "AwayTeamScore": 1,
            "FirstHalfExtraTime": {"HomeTeamScore": 1, "AwayTeamScore": 0},
        }

        decision = watchdog.score_sync_decision(match)

        self.assertFalse(decision.should_sync)
        self.assertTrue(decision.is_finished)
        self.assertIn("90-minute", decision.reason)

    def test_elimination_result_type_extra_time_requires_manual_review_without_full_time_score(self):
        match = {
            "MatchStatus": 0,
            "MatchTime": "132'",
            "HomeTeamScore": 3,
            "AwayTeamScore": 2,
            "ResultType": 3,
        }

        decision = watchdog.score_sync_decision(match)

        self.assertFalse(decision.should_sync)
        self.assertTrue(decision.is_finished)
        self.assertIn("90-minute", decision.reason)


if __name__ == "__main__":
    unittest.main()
