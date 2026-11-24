#!/usr/bin/env python3
"""Releases and gate templates are in order (work package F-09, L0).

Two things that would otherwise only be found when they were needed — at a milestone, by a session
with nobody to ask:

* every `releases/M<n>.yaml` is valid and has its report (`tools/release/plan.py --check`);
* every issue template the plan relies on exists, parses, and carries the right labels. A gate
  issue without `human-gate` never reaches the owner; a `blocked` one without its label is never
  picked up at the retro.
"""

from __future__ import annotations

import sys
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
TEMPLATES = REPO / ".github" / "ISSUE_TEMPLATE"
sys.path.insert(0, str(REPO / "tools" / "release"))

import plan  # noqa: E402

#: The template each name must match (by its `name:` prefix), and the labels it must carry.
REQUIRED: dict[str, set[str]] = {
    "HG-1": {"human-gate", "needs-device"},
    "HG-R": {"human-gate", "needs-device"},
    "HG-2": {"human-gate", "needs-device"},
    "HG-3": {"human-gate", "needs-device"},
    "HG-4": {"human-gate", "needs-device"},
    "Blocked": {"blocked"},
    "Infrastructure": {"infra"},
}


def front_matter(path: Path) -> dict | None:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        return None
    end = text.find("\n---\n", 4)
    if end < 0:
        return None
    try:
        document = yaml.safe_load(text[4:end])
    except yaml.YAMLError:
        return None
    if not isinstance(document, dict):
        return None
    document["_body"] = text[end + 5:]
    return document


def check_templates(templates: Path) -> list[str]:
    problems: list[str] = []
    found: dict[str, tuple[str, set[str]]] = {}
    for path in sorted(templates.glob("*.md")) if templates.is_dir() else []:
        meta = front_matter(path)
        if meta is None:
            problems.append(f"{path.name}: no readable front matter between --- lines")
            continue
        for key in ("name", "about", "title", "labels"):
            if not meta.get(key):
                problems.append(f"{path.name}: front matter has no {key}")
        if not meta.get("_body", "").strip():
            problems.append(f"{path.name}: has no body")
        labels = meta.get("labels") or []
        if isinstance(labels, str):
            labels = [part.strip() for part in labels.split(",")]
        name = str(meta.get("name") or "")
        for wanted in REQUIRED:
            if name == wanted or name.startswith(wanted + " "):
                if wanted in found:
                    problems.append(f"{path.name}: a second template for {wanted} "
                                    f"(the first is {found[wanted][0]})")
                found[wanted] = (path.name, set(labels))

    for wanted, labels in REQUIRED.items():
        if wanted not in found:
            problems.append(f"no issue template for {wanted} in .github/ISSUE_TEMPLATE")
            continue
        filename, has = found[wanted]
        missing = labels - has
        if missing:
            problems.append(f"{filename}: {wanted} must carry label(s) "
                            f"{', '.join(sorted(missing))}")
    return problems


def main() -> int:
    problems = check_templates(TEMPLATES)
    releases, release_problems = plan.load_all(plan.RELEASES, reports=plan.REPORTS,
                                               licences=plan.LICENCES)
    problems += release_problems

    if problems:
        print(f"release: {len(problems)} problem(s):")
        for problem in problems:
            print(f"  {problem}")
        return 1
    print(f"release: {len(REQUIRED)} issue templates present and labelled, "
          f"{len(releases)} release file(s) valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
