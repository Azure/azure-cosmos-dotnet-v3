"""Tests for the flakiness analyzer — fliprate and EWMA calculations."""

import unittest
from analyzer import FlakinessAnalyzer


class TestFliprateCalculation(unittest.TestCase):
    """Tests for FlkinessAnalyzer.calculate_fliprate()."""

    def test_all_pass(self):
        """A test that always passes has fliprate 0."""
        outcomes = ["Passed"] * 20
        self.assertAlmostEqual(FlakinessAnalyzer.calculate_fliprate(outcomes), 0.0)

    def test_all_fail(self):
        """A test that always fails has fliprate 0 (broken, not flaky)."""
        outcomes = ["Failed"] * 20
        self.assertAlmostEqual(FlakinessAnalyzer.calculate_fliprate(outcomes), 0.0)

    def test_alternating(self):
        """A test that alternates every run has fliprate 1.0."""
        outcomes = ["Passed", "Failed"] * 10
        self.assertAlmostEqual(FlakinessAnalyzer.calculate_fliprate(outcomes), 1.0)

    def test_single_flip(self):
        """One flip in a 10-run sequence."""
        outcomes = ["Passed"] * 5 + ["Failed"] + ["Passed"] * 4
        # Transitions: P,P,P,P,P→F,F→P,P,P,P = 2 flips out of 9 transitions
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_fliprate(outcomes), 2 / 9, places=3
        )

    def test_single_outcome(self):
        """Single outcome returns 0."""
        self.assertAlmostEqual(FlakinessAnalyzer.calculate_fliprate(["Passed"]), 0.0)

    def test_empty(self):
        """Empty list returns 0."""
        self.assertAlmostEqual(FlakinessAnalyzer.calculate_fliprate([]), 0.0)

    def test_two_outcomes_same(self):
        """Two identical outcomes = no flip."""
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_fliprate(["Passed", "Passed"]), 0.0
        )

    def test_two_outcomes_different(self):
        """Two different outcomes = 1 flip / 1 transition = 1.0."""
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_fliprate(["Passed", "Failed"]), 1.0
        )


class TestEWMAFliprateCalculation(unittest.TestCase):
    """Tests for FlakinessAnalyzer.calculate_ewma_fliprate()."""

    def test_all_pass(self):
        """Stable test has EWMA 0."""
        outcomes = ["Passed"] * 20
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_ewma_fliprate(outcomes, alpha=0.3), 0.0
        )

    def test_recent_flakiness_weighted_higher(self):
        """EWMA should be higher when flips are at the end vs. the beginning."""
        # Flips at the beginning (old)
        early_flips = ["Passed", "Failed", "Passed"] + ["Passed"] * 17
        # Flips at the end (recent)
        late_flips = ["Passed"] * 17 + ["Passed", "Failed", "Passed"]

        ewma_early = FlakinessAnalyzer.calculate_ewma_fliprate(early_flips, alpha=0.3)
        ewma_late = FlakinessAnalyzer.calculate_ewma_fliprate(late_flips, alpha=0.3)

        self.assertGreater(ewma_late, ewma_early)

    def test_alternating(self):
        """Alternating sequence converges to high EWMA."""
        outcomes = ["Passed", "Failed"] * 20
        ewma = FlakinessAnalyzer.calculate_ewma_fliprate(outcomes, alpha=0.3)
        # Should converge near the steady-state value
        self.assertGreater(ewma, 0.2)

    def test_empty(self):
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_ewma_fliprate([], alpha=0.3), 0.0
        )

    def test_single(self):
        self.assertAlmostEqual(
            FlakinessAnalyzer.calculate_ewma_fliprate(["Passed"], alpha=0.3), 0.0
        )


if __name__ == "__main__":
    unittest.main()
