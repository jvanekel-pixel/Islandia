#!/usr/bin/env python3
"""Tests for the coaster piece generator (RC-01a). Run with: make tools-test

The piece list decides what a player can build and what a saved coaster's numbers mean, so most of
these prove the checks refuse something.
"""

from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "content"))

import coaster_pieces as cp  # noqa: E402


class CoasterPieceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.pieces = yaml.safe_load(cp.PIECES.read_text(encoding="utf-8"))
        self.facts = json.loads(cp.FACTS.read_text(encoding="utf-8"))

    def refused(self, pieces: dict, facts: dict | None = None) -> list[str]:
        found = cp.problems(pieces, facts or self.facts)
        self.assertTrue(found, "expected the data to be refused")
        return found

    def test_the_real_data_is_valid_and_the_generated_file_current(self) -> None:
        self.assertEqual(cp.problems(self.pieces, self.facts), [])
        self.assertEqual(cp.render(self.pieces, self.facts), cp.GENERATED.read_text(encoding="utf-8"))

    def test_every_model_was_measured_in_all_six_styles(self) -> None:
        for name, shape in self.facts["shapes"].items():
            self.assertEqual(sorted(shape["crc32"]), sorted(self.facts["styles"]), name)

    def test_an_id_used_twice_is_refused(self) -> None:
        pieces = copy.deepcopy(self.pieces)
        pieces["pieces"].append(dict(pieces["pieces"][1]))
        self.assertTrue(any("collides" in p for p in self.refused(pieces)))

    def test_an_id_outside_the_coaster_namespace_is_refused(self) -> None:
        pieces = copy.deepcopy(self.pieces)
        pieces["pieces"][1]["id"] = "Straight"
        self.refused(pieces)

    def test_an_unmeasured_shape_is_refused(self) -> None:
        pieces = copy.deepcopy(self.pieces)
        pieces["pieces"][1]["shape"] = "vertical-bend"
        self.assertTrue(any("not in the facts" in p for p in self.refused(pieces)))

    def test_a_second_station_is_refused(self) -> None:
        pieces = copy.deepcopy(self.pieces)
        pieces["pieces"][1]["role"] = "station"
        self.assertTrue(any("exactly one station" in p for p in self.refused(pieces)))

    def test_an_invented_placement_or_intensity_is_refused(self) -> None:
        for field, value in (("placed", "upside-down"), ("intensity", "extreme"), ("role", "brake")):
            pieces = copy.deepcopy(self.pieces)
            pieces["pieces"][2][field] = value
            self.refused(pieces)

    def test_a_centreline_that_misses_its_exit_is_refused(self) -> None:
        facts = copy.deepcopy(self.facts)
        facts["shapes"]["straight"]["samples"][-1] = [0, 0, 250]
        self.assertTrue(any("does not end at its exit" in p for p in self.refused(self.pieces, facts)))

    def test_the_csharp_names(self) -> None:
        self.assertEqual(cp.symbol("coaster.turn.small.right"), "TurnSmallRight")
        self.assertEqual(cp.symbol("corner-small-ramp"), "CornerSmallRamp")


if __name__ == "__main__":
    unittest.main()
