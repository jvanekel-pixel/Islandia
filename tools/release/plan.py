#!/usr/bin/env python3
"""Plan a milestone release from `releases/M<n>.yaml` (work package F-09).

A milestone is released by adding one small file on `main` (05-OPERATIONS.md §9):

    # releases/M1.yaml
    milestone: M1
    title: Factory online
    draft: false          # optional; ADR-11 forces true whenever a private-use pack is admitted

The `release` workflow runs this to decide what to publish, then tags the commit and publishes a
GitHub Release with the APK and the milestone report `docs/reports/M<n>-<slug>.md` attached.
Sessions cannot push tags; the workflow can, with its own token.

Modes:

    plan.py --check                      every release file is valid (lint, L0)
    plan.py --plan --tags FILE           the one unreleased milestone, as GitHub outputs
    plan.py --rehearsal RUN_ID           a throwaway prerelease that proves the route end to end

Outputs (written to $GITHUB_OUTPUT when set, and always printed):

    any       true when there is something to release
    tag       the tag to create (M1, or rehearsal-<run id>)
    title     the release title
    report    the report to attach and use as the release notes
    draft     true or false
    rehearsal true or false — a rehearsal is deleted again once it has been checked

Engine-free and standard-library plus PyYAML, so it is tested with the other tooling (make check).
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
RELEASES = REPO / "releases"
REPORTS = REPO / "docs" / "reports"
LICENCES = REPO / "content" / "licences.yaml"
REHEARSAL_REPORT = REPO / "tools" / "release" / "rehearsal-report.md"

MILESTONE = re.compile(r"^M([1-9][0-9]?)$")
ALLOWED_KEYS = {"milestone", "title", "draft"}


class ReleaseError(Exception):
    """A release file, or the repository around it, is not fit to release from."""


@dataclass(frozen=True)
class Release:
    tag: str
    title: str
    report: Path
    draft: bool
    rehearsal: bool = False

    def outputs(self, repo: Path) -> dict[str, str]:
        return {
            "any": "true",
            "tag": self.tag,
            "title": self.title,
            "report": self.report.relative_to(repo).as_posix(),
            "draft": "true" if self.draft else "false",
            "rehearsal": "true" if self.rehearsal else "false",
        }


def private_use_admitted(licences: Path) -> bool:
    """ADR-11: does any admitted licence mark its packs private-use only?

    Read the same way as the android-build guard (a `private_use: true` line anywhere), and also
    parsed, so a flag in a form the regex misses is not missed.
    """
    if not licences.is_file():
        return False
    text = licences.read_text(encoding="utf-8")
    if re.search(r"^\s*private_use:\s*true\b", text, re.M):
        return True
    document = yaml.safe_load(text) or {}
    for entry in document.get("allowed") or []:
        if isinstance(entry, dict) and entry.get("private_use") is True:
            return True
    return False


def report_for(milestone: str, reports: Path) -> Path:
    """The one `docs/reports/M<n>-<slug>.md`. `M1-*` cannot match `M10-…`: the dash follows."""
    found = sorted(reports.glob(f"{milestone}-*.md")) if reports.is_dir() else []
    if not found:
        raise ReleaseError(f"{milestone}: no report docs/reports/{milestone}-<slug>.md — write it "
                           "in the same pull request as the release file (05-OPERATIONS.md §9)")
    if len(found) > 1:
        names = ", ".join(path.name for path in found)
        raise ReleaseError(f"{milestone}: more than one report ({names}); the release attaches "
                           "exactly one")
    return found[0]


def load(path: Path, *, reports: Path, licences: Path) -> Release:
    """Read and validate one release file."""
    stem = path.stem
    if not MILESTONE.match(stem):
        raise ReleaseError(f"{path.name}: release files are named M<n>.yaml")
    try:
        document = yaml.safe_load(path.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:
        raise ReleaseError(f"{path.name}: will not parse: {error}") from error
    if not isinstance(document, dict):
        raise ReleaseError(f"{path.name}: is not a mapping")

    unknown = set(document) - ALLOWED_KEYS
    if unknown:
        raise ReleaseError(f"{path.name}: unknown key(s) {', '.join(sorted(unknown))}; "
                           f"allowed: {', '.join(sorted(ALLOWED_KEYS))}")
    if document.get("milestone") != stem:
        raise ReleaseError(f"{path.name}: milestone is {document.get('milestone')!r}, "
                           f"the file name says {stem!r}")
    title = document.get("title")
    if not isinstance(title, str) or not title.strip():
        raise ReleaseError(f"{path.name}: needs a title")
    draft = document.get("draft", False)
    if not isinstance(draft, bool):
        raise ReleaseError(f"{path.name}: draft is true or false, not {draft!r}")

    report = report_for(stem, reports)
    # ADR-11: a build that may hold a private-use pack is never published, only drafted.
    draft = draft or private_use_admitted(licences)
    return Release(tag=stem, title=f"{stem} — {title.strip()}", report=report, draft=draft)


def load_all(releases: Path, *, reports: Path, licences: Path) -> tuple[list[Release], list[str]]:
    found: list[Release] = []
    problems: list[str] = []
    if not releases.is_dir():
        return found, problems
    for path in sorted(releases.glob("*.yaml")) + sorted(releases.glob("*.yml")):
        if path.suffix == ".yml":
            problems.append(f"{path.name}: use the .yaml extension")
            continue
        try:
            found.append(load(path, reports=reports, licences=licences))
        except ReleaseError as error:
            problems.append(str(error))
    return found, problems


def choose(releases: list[Release], existing_tags: set[str]) -> Release | None:
    """The one release with no tag yet.

    More than one is refused: the workflow attaches the APK of the commit it runs on, and that is
    the right build for the newest milestone only. An older milestone left untagged needs a person
    to decide which build it ought to carry.
    """
    pending = [release for release in releases if release.tag not in existing_tags]
    if len(pending) > 1:
        names = ", ".join(release.tag for release in pending)
        raise ReleaseError(f"more than one milestone is unreleased ({names}); release one at a "
                           "time, or tag the older ones by dispatching this workflow on their "
                           "own commits")
    return pending[0] if pending else None


def rehearsal(run_id: str, *, licences: Path) -> Release:
    if not re.fullmatch(r"[0-9]+", run_id):
        raise ReleaseError(f"rehearsal run id must be digits, not {run_id!r}")
    return Release(tag=f"rehearsal-{run_id}", title=f"Rehearsal {run_id} — deleted when checked",
                   report=REHEARSAL_REPORT, draft=private_use_admitted(licences), rehearsal=True)


def emit(values: dict[str, str]) -> None:
    lines = [f"{key}={value}" for key, value in values.items()]
    for line in lines:
        print(line)
    target = os.environ.get("GITHUB_OUTPUT")
    if target:
        with open(target, "a", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true", help="validate every release file")
    mode.add_argument("--plan", action="store_true", help="the unreleased milestone, if any")
    mode.add_argument("--rehearsal", metavar="RUN_ID", help="a throwaway prerelease")
    parser.add_argument("--tags", type=Path, help="file listing existing tags, one per line")
    args = parser.parse_args(argv)

    if args.rehearsal:
        try:
            release = rehearsal(args.rehearsal, licences=LICENCES)
        except ReleaseError as error:
            print(f"release: {error}")
            return 1
        emit(release.outputs(REPO))
        return 0

    releases, problems = load_all(RELEASES, reports=REPORTS, licences=LICENCES)
    if problems:
        print(f"release: {len(problems)} problem(s):")
        for problem in problems:
            print(f"  {problem}")
        return 1

    if args.check:
        print(f"release: {len(releases)} release file(s), all valid")
        return 0

    existing: set[str] = set()
    if args.tags and args.tags.is_file():
        existing = {line.strip() for line in args.tags.read_text().splitlines() if line.strip()}
    try:
        release = choose(releases, existing)
    except ReleaseError as error:
        print(f"release: {error}")
        return 1
    if release is None:
        print("release: every milestone with a release file is already tagged")
        emit({"any": "false"})
        return 0
    emit(release.outputs(REPO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
