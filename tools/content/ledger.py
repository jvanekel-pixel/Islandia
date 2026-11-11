#!/usr/bin/env python3
"""Check every item's licence, and generate the notices the app ships.

ADR-11 admits CC0 and attribution-licensed work, because Islandia is a gift that is never published
or sold — which roughly triples the usable art. That is a decision with consequences, so the
allowlist in `content/licences.yaml` names the record in `DECISIONS.md` that admitted it, and this
tool checks the record really exists. The point is that no future session can widen what is allowed
by editing a data file: widening it means writing down why, where the owner can read it.

Attribution is then enforced rather than encouraged. An item on an attribution licence with no
author or no source url fails the build, because the alternative is a notices screen that quietly
omits people whose work is in the game.

Non-commercial licences are admitted and recorded as such. Nothing is sold today; if that ever
changed, the ledger says exactly which items would have to go.

Usage:
    python3 tools/content/ledger.py          # write content/NOTICES.md
    python3 tools/content/ledger.py --check  # fail if it would change, or if anything is unlicensed
"""

from __future__ import annotations

import sys
from dataclasses import dataclass
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))

from catalogue import CONTENT, REPO, Item  # noqa: E402

LICENCES = CONTENT / "licences.yaml"
NOTICES = CONTENT / "NOTICES.md"
DECISIONS = REPO / "docs/plan/DECISIONS.md"


@dataclass
class Licence:
    id: str
    name: str
    attribution: bool
    non_commercial: bool


def load_licences(path: Path | None = None,
                  decisions: Path | None = None) -> tuple[dict[str, Licence], list[str]]:
    """Read the allowlist and verify it cites a decision record that exists."""
    source = path or LICENCES
    if not source.is_file():
        return {}, [f"{source.name}: missing — nothing can be licensed"]

    try:
        document = yaml.safe_load(source.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:
        return {}, [f"{source.name}: will not parse: {error}"]

    if not isinstance(document, dict) or "allowed" not in document:
        return {}, [f"{source.name}: must be a mapping with allowed and decision keys"]

    problems: list[str] = []

    cited = document.get("decision")
    if not isinstance(cited, str) or not cited.strip():
        problems.append(f"{source.name}: must cite the DECISIONS.md record that admits these"
                        " licences")
    else:
        record = decisions or DECISIONS
        if not record.is_file():
            problems.append(f"{source.name}: cites a decision record but {record.name} is missing")
        elif f"## {cited.strip()}" not in record.read_text(encoding="utf-8"):
            problems.append(f"{source.name}: cites {cited.strip()!r}, which is not a heading in"
                            f" {record.name} — widening what is allowed means writing down why")

    licences: dict[str, Licence] = {}
    allowed = document["allowed"]
    if not isinstance(allowed, list) or not allowed:
        problems.append(f"{source.name}: allowed must list at least one licence")
        return licences, problems

    for index, raw in enumerate(allowed, start=1):
        where = f"{source.name} entry {index}"
        if not isinstance(raw, dict):
            problems.append(f"{where}: must be a mapping")
            continue
        missing = [f for f in ("id", "name", "attribution", "non_commercial") if f not in raw]
        if missing:
            problems.append(f"{where}: missing {', '.join(missing)}")
            continue
        if raw["id"] in licences:
            problems.append(f"{where}: {raw['id']} is listed twice")
            continue
        licences[str(raw["id"])] = Licence(
            id=str(raw["id"]), name=str(raw["name"]),
            attribution=bool(raw["attribution"]), non_commercial=bool(raw["non_commercial"]),
        )

    return licences, problems


def check(items: tuple[Item, ...], licences: dict[str, Licence]) -> list[str]:
    """Every licence fault across the catalogue."""
    problems: list[str] = []

    for item in items:
        source = item.source
        licence = licences.get(source.licence)

        if licence is None:
            problems.append(
                f"{item.id}: licence {source.licence!r} is not in licences.yaml. Admitting it means"
                " adding it there and recording why in DECISIONS.md"
            )
            continue

        if licence.attribution:
            if not source.attribution:
                problems.append(f"{item.id}: {licence.id} requires attribution and no author is"
                                " named")
            if not source.url:
                problems.append(f"{item.id}: {licence.id} requires attribution and there is no"
                                " source url to attribute to")

        if source.kind == "pack":
            # RX-02: an asset's licence turning out not to be what the pipeline recorded. The
            # mitigation is the licence text archived at the time of download, with the asset.
            if not source.licence_file:
                problems.append(f"{item.id}: a pack must record where its licence text is"
                                " archived")
            elif not (CONTENT / source.licence_file).is_file():
                problems.append(f"{item.id}: licence text missing at {source.licence_file}")

    return problems


def render(items: tuple[Item, ...], licences: dict[str, Licence]) -> str:
    """The notices screen's source: who made what, under which licence."""
    lines = [
        "# Notices",
        "",
        "*Generated by `tools/content/ledger.py` from the catalogue. Do not edit — run"
        " `make content`.*",
        "",
        "Islandia is built on work other people gave away. This is the full list, and it ships in"
        " the app.",
        "",
    ]

    if not items:
        lines += ["Nothing is in the catalogue yet.", ""]
        return "\n".join(lines)

    by_licence: dict[str, list[Item]] = {}
    for item in items:
        by_licence.setdefault(item.source.licence, []).append(item)

    for licence_id in sorted(by_licence):
        licence = licences.get(licence_id)
        heading = f"{licence.name} ({licence_id})" if licence else licence_id
        lines += [f"## {heading}", ""]

        credits: dict[tuple[str, str | None], list[Item]] = {}
        for item in by_licence[licence_id]:
            key = (item.source.attribution or "—", item.source.url)
            credits.setdefault(key, []).append(item)

        for (author, url) in sorted(credits, key=lambda key: key[0].lower()):
            entries = credits[(author, url)]
            names = ", ".join(sorted(entry.name for entry in entries))
            credit = f"[{author}]({url})" if url and author != "—" else author
            lines += [f"- **{credit}** — {len(entries)} item(s): {names}"]
        lines.append("")

    non_commercial = sorted(
        item.id for item in items
        if (licence := licences.get(item.source.licence)) and licence.non_commercial
    )
    if non_commercial:
        lines += [
            "## Non-commercial licences",
            "",
            "Admitted because Islandia is a personal gift and is never sold. If that ever changed,"
            " these would have to be replaced:",
            "",
        ] + [f"- `{item}`" for item in non_commercial] + [""]

    return "\n".join(lines)


def main(argv: list[str]) -> int:
    from catalogue import load

    checking = "--check" in argv
    catalogue = load()
    if not catalogue.ok:
        print("content: the catalogue does not load, so licences cannot be checked")
        return 1

    licences, problems = load_licences()
    problems.extend(check(catalogue.items, licences))

    if problems:
        print(f"content: {len(problems)} licence problem(s):")
        for problem in problems:
            print(f"  {problem}")
        return 1

    wanted = render(catalogue.items, licences)
    current = NOTICES.read_text(encoding="utf-8") if NOTICES.is_file() else None

    if checking:
        if current == wanted:
            print(f"content: {NOTICES.relative_to(REPO)} is current"
                  f" ({len(catalogue.items)} item(s), {len(licences)} licence(s) allowed)")
            return 0
        print(f"content: {NOTICES.relative_to(REPO)} is stale. Run: make content")
        return 1

    if current != wanted:
        NOTICES.write_text(wanted, encoding="utf-8")
        print(f"content: wrote {NOTICES.relative_to(REPO)}")
    else:
        print(f"content: {NOTICES.relative_to(REPO)} already current")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
