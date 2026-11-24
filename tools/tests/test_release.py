#!/usr/bin/env python3
"""Tests for the release planner and the gate-template check (F-09).

Run with: make tools-test

Mostly about whether they can fail: a release that goes out without its report, or a gate issue
that never reaches the owner, is found at a milestone by a session with nobody to ask.
"""

from __future__ import annotations

import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "release"))
sys.path.insert(0, str(REPO / "tools" / "lint"))

import plan  # noqa: E402
import check_release  # noqa: E402

REDISTRIBUTABLE = "allowed:\n  - id: CC0-1.0\n    attribution: false\n"
PRIVATE = "allowed:\n  - id: Some-Pack-Licence\n    private_use: true\n"


class Sandbox:
    """A throwaway releases/, docs/reports/ and licences file."""

    def __init__(self, licences: str = REDISTRIBUTABLE):
        self._dir = tempfile.TemporaryDirectory()
        self.root = Path(self._dir.name)
        self.releases = self.root / "releases"
        self.reports = self.root / "docs" / "reports"
        self.releases.mkdir()
        self.reports.mkdir(parents=True)
        self.licences = self.root / "licences.yaml"
        self.licences.write_text(licences)

    def release(self, name: str, body: str) -> Path:
        path = self.releases / name
        path.write_text(body)
        return path

    def report(self, name: str) -> Path:
        path = self.reports / name
        path.write_text("# report\n")
        return path

    def load_all(self):
        return plan.load_all(self.releases, reports=self.reports, licences=self.licences)

    def close(self):
        self._dir.cleanup()


class ReleaseFileTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()

    def tearDown(self):
        self.box.close()

    def test_a_valid_release(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: Factory online\n")
        report = self.box.report("M1-factory-online.md")
        releases, problems = self.box.load_all()
        self.assertEqual(problems, [])
        self.assertEqual(len(releases), 1)
        self.assertEqual(releases[0].tag, "M1")
        self.assertEqual(releases[0].title, "M1 — Factory online")
        self.assertEqual(releases[0].report, report)
        self.assertFalse(releases[0].draft)

    def test_no_report_is_refused(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: Factory online\n")
        _, problems = self.box.load_all()
        self.assertEqual(len(problems), 1)
        self.assertIn("no report", problems[0])

    def test_two_reports_are_refused(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: x\n")
        self.box.report("M1-a.md")
        self.box.report("M1-b.md")
        _, problems = self.box.load_all()
        self.assertIn("more than one report", problems[0])

    def test_m1_does_not_take_m10s_report(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: x\n")
        self.box.report("M10-import.md")
        _, problems = self.box.load_all()
        self.assertIn("no report", problems[0])

    def test_milestone_must_match_file_name(self):
        self.box.release("M2.yaml", "milestone: M1\ntitle: x\n")
        self.box.report("M2-x.md")
        _, problems = self.box.load_all()
        self.assertIn("file name says", problems[0])

    def test_bad_file_name_unknown_key_and_missing_title(self):
        self.box.release("first.yaml", "milestone: first\ntitle: x\n")
        self.box.release("M3.yaml", "milestone: M3\ntitle: x\nsigned: true\n")
        self.box.release("M4.yaml", "milestone: M4\n")
        self.box.release("M5.yml", "milestone: M5\ntitle: x\n")
        for n in (3, 4, 5):
            self.box.report(f"M{n}-x.md")
        _, problems = self.box.load_all()
        text = "\n".join(problems)
        self.assertIn("named M<n>.yaml", text)
        self.assertIn("unknown key", text)
        self.assertIn("needs a title", text)
        self.assertIn(".yaml extension", text)

    def test_draft_must_be_boolean(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: x\ndraft: maybe\n")
        self.box.report("M1-x.md")
        _, problems = self.box.load_all()
        self.assertIn("true or false", problems[0])

    def test_explicit_draft(self):
        self.box.release("M1.yaml", "milestone: M1\ntitle: x\ndraft: true\n")
        self.box.report("M1-x.md")
        releases, _ = self.box.load_all()
        self.assertTrue(releases[0].draft)


class PrivateUseTests(unittest.TestCase):
    """ADR-11: a build that may hold a private-use pack is only ever a draft."""

    def test_private_use_forces_a_draft(self):
        box = Sandbox(PRIVATE)
        self.addCleanup(box.close)
        box.release("M1.yaml", "milestone: M1\ntitle: x\ndraft: false\n")
        box.report("M1-x.md")
        releases, problems = box.load_all()
        self.assertEqual(problems, [])
        self.assertTrue(releases[0].draft)

    def test_flow_style_flag_is_still_found(self):
        box = Sandbox("allowed:\n  - {id: X, private_use: true}\n")
        self.addCleanup(box.close)
        self.assertTrue(plan.private_use_admitted(box.licences))

    def test_the_real_ledger_is_redistributable_today(self):
        # Wave 1 is all CC0 (STATE.md). If this fails, C-02d's draft route is now needed.
        self.assertFalse(plan.private_use_admitted(plan.LICENCES))

    def test_a_rehearsal_follows_the_same_rule(self):
        box = Sandbox(PRIVATE)
        self.addCleanup(box.close)
        self.assertTrue(plan.rehearsal("42", licences=box.licences).draft)


class ChooseTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        for n in (1, 2):
            self.box.release(f"M{n}.yaml", f"milestone: M{n}\ntitle: t{n}\n")
            self.box.report(f"M{n}-x.md")
        self.releases, _ = self.box.load_all()

    def tearDown(self):
        self.box.close()

    def test_the_one_untagged_milestone(self):
        self.assertEqual(plan.choose(self.releases, {"M1"}).tag, "M2")

    def test_nothing_left(self):
        self.assertIsNone(plan.choose(self.releases, {"M1", "M2", "assets-wave-1"}))

    def test_two_untagged_are_refused(self):
        with self.assertRaises(plan.ReleaseError):
            plan.choose(self.releases, set())


class RehearsalTests(unittest.TestCase):
    def test_tag_and_report(self):
        release = plan.rehearsal("123", licences=plan.LICENCES)
        self.assertEqual(release.tag, "rehearsal-123")
        self.assertTrue(release.rehearsal)
        self.assertTrue(release.report.is_file())

    def test_run_id_is_digits(self):
        with self.assertRaises(plan.ReleaseError):
            plan.rehearsal("1; rm -rf /", licences=plan.LICENCES)

    def test_outputs_are_written_for_github(self):
        with tempfile.NamedTemporaryFile("r", suffix=".txt", delete=False) as handle:
            target = handle.name
        self.addCleanup(os.unlink, target)
        old = os.environ.get("GITHUB_OUTPUT")
        os.environ["GITHUB_OUTPUT"] = target
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(plan.main(["--rehearsal", "7"]), 0)
        finally:
            if old is None:
                del os.environ["GITHUB_OUTPUT"]
            else:
                os.environ["GITHUB_OUTPUT"] = old
        written = dict(line.split("=", 1) for line in Path(target).read_text().splitlines())
        self.assertEqual(written["tag"], "rehearsal-7")
        self.assertEqual(written["rehearsal"], "true")
        self.assertEqual(written["report"], "tools/release/rehearsal-report.md")


TEMPLATE = """---
name: "{name}"
about: "x"
title: "x"
labels: [{labels}]
---

Body.
"""


class TemplateTests(unittest.TestCase):
    def write_all(self, folder: Path, skip: str = "", labels: dict | None = None):
        for name, wanted in check_release.REQUIRED.items():
            if name == skip:
                continue
            chosen = (labels or {}).get(name, wanted)
            (folder / f"{name.lower()}.md").write_text(
                TEMPLATE.format(name=f"{name} — something",
                                labels=", ".join(f'"{label}"' for label in sorted(chosen))))

    def test_the_repository_passes(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(check_release.main(), 0)

    def test_complete_set_passes(self):
        with tempfile.TemporaryDirectory() as folder:
            self.write_all(Path(folder))
            self.assertEqual(check_release.check_templates(Path(folder)), [])

    def test_a_missing_gate_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            self.write_all(Path(folder), skip="HG-R")
            problems = check_release.check_templates(Path(folder))
            self.assertEqual(len(problems), 1)
            self.assertIn("HG-R", problems[0])

    def test_a_gate_without_human_gate_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            self.write_all(Path(folder), labels={"HG-2": {"needs-device"}})
            problems = check_release.check_templates(Path(folder))
            self.assertIn("human-gate", problems[0])

    def test_no_front_matter_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            self.write_all(Path(folder))
            (Path(folder) / "stray.md").write_text("just text\n")
            problems = check_release.check_templates(Path(folder))
            self.assertIn("front matter", problems[0])

    def test_hg_1_does_not_also_count_as_hg_10(self):
        with tempfile.TemporaryDirectory() as folder:
            self.write_all(Path(folder), skip="HG-1")
            (Path(folder) / "hg-10.md").write_text(
                TEMPLATE.format(name="HG-10 — later", labels='"human-gate"'))
            problems = check_release.check_templates(Path(folder))
            self.assertTrue(any("HG-1 " in p or p.endswith("HG-1 in .github/ISSUE_TEMPLATE")
                                for p in problems), problems)


if __name__ == "__main__":
    unittest.main()
