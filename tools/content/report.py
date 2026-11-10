#!/usr/bin/env python3
"""Count the catalogue honestly.

The blueprint this project replaces shipped a catalogue of 631 items that resolved to about six
distinct geometries. Ninety-five architecture modules — stairs, roofs, bridges, across five themes —
all pointed at the same 2 × 3 × 0.25 m wall box. Seventy-five props pointed at one lantern box. The
count was real; the content was not; and nothing in the build could tell the difference.

So this does not count catalogue entries. It counts **distinct asset checksums**, taken from the
facts files, and reports palette variants as a separate number. Two entries sharing a mesh are one
mesh and two variants, and there is no way to write the catalogue that says otherwise.

One thing it refuses outright: two entries sharing a mesh but declaring different `kind`s. Sharing
geometry between a spring and an autumn oak is the system working. Sharing it between a stair and a
roof is the blueprint's lie, and it is the shape that lie takes.

Usage:
    python3 tools/content/report.py            # print the report
    python3 tools/content/report.py --check    # also fail on a shared mesh across kinds
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from catalogue import CATEGORIES, Item  # noqa: E402

#: ADR-11, set by the owner on 2026-11-10: about 120 distinct meshes, final rather than a waypoint.
TARGET_MESHES = 120


def mesh_of(item: Item) -> str | None:
    """The checksum of an item's asset, from its facts file. None when it cannot be read."""
    path = item.path("facts")
    if not path.is_file():
        return None
    try:
        facts = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    asset = facts.get("asset")
    if not isinstance(asset, dict):
        return None
    checksum = asset.get("crc32")
    return str(checksum).lower() if checksum else None


def variants_of(item: Item) -> int:
    """How many looks this entry yields. One, unless it carries a palette."""
    return len(item.palette_variants) or 1


def shared_meshes(items: tuple[Item, ...]) -> dict[str, list[Item]]:
    """Assets used by more than one entry, keyed by checksum."""
    by_mesh: dict[str, list[Item]] = {}
    for item in items:
        checksum = mesh_of(item)
        if checksum:
            by_mesh.setdefault(checksum, []).append(item)
    return {key: value for key, value in by_mesh.items() if len(value) > 1}


def problems(items: tuple[Item, ...]) -> list[str]:
    """Shared geometry across kinds — the shape the blueprint's dishonest count took."""
    found: list[str] = []
    for checksum, sharing in sorted(shared_meshes(items).items()):
        kinds = {item.kind for item in sharing}
        if len(kinds) > 1:
            names = ", ".join(sorted(item.id for item in sharing))
            found.append(
                f"one mesh ({checksum}) is shared across kinds {', '.join(sorted(kinds))}:"
                f" {names}. Palette variants of one thing may share a mesh; different kinds of"
                " thing may not"
            )
    return found


def render(items: tuple[Item, ...]) -> str:
    unreadable = [item.id for item in items if mesh_of(item) is None]
    checksums = {mesh_of(item) for item in items if mesh_of(item) is not None}
    meshes = len(checksums)
    variants = sum(variants_of(item) for item in items)

    lines = [
        "content: catalogue report",
        "",
        f"  entries          {len(items):>6}   rows in content/catalogue/*.yaml",
        f"  distinct meshes  {meshes:>6}   unique asset checksums — the number that means something",
        f"  palette variants {variants:>6}   looks these entries yield",
        "",
    ]

    if meshes:
        lines.append(f"  against ADR-11's target of about {TARGET_MESHES} distinct meshes:"
                     f" {meshes * 100 // TARGET_MESHES}%")
        lines.append("")

    lines.append("  per category:")
    for category in CATEGORIES:
        in_category = [item for item in items if item.category == category]
        if not in_category:
            lines.append(f"    {category:<14} —")
            continue
        category_meshes = {mesh_of(item) for item in in_category if mesh_of(item) is not None}
        lines.append(
            f"    {category:<14} {len(in_category):>4} entries"
            f"  {len(category_meshes):>4} meshes"
            f"  {sum(variants_of(item) for item in in_category):>4} variants"
        )

    sharing = shared_meshes(items)
    if sharing:
        lines += ["", f"  {len(sharing)} mesh(es) are used by more than one entry:"]
        for checksum, group in sorted(sharing.items()):
            lines.append(f"    {checksum}  {', '.join(sorted(item.id for item in group))}")

    if unreadable:
        lines += ["", f"  {len(unreadable)} entry/entries have no readable facts, so their meshes"
                      " are not counted:"]
        lines += [f"    {identifier}" for identifier in sorted(unreadable)]

    return "\n".join(lines)


def main(argv: list[str]) -> int:
    from catalogue import load

    catalogue = load()
    if not catalogue.ok:
        print("content: the catalogue does not load, so it cannot be counted")
        return 1

    checking = "--check" in argv

    if checking:
        # One line in a lint run; the full table is what `make content-report` is for. The numbers
        # still appear on every CI run, in the workflow's own summary.
        items = catalogue.items
        meshes = len({mesh_of(item) for item in items if mesh_of(item) is not None})
        variants = sum(variants_of(item) for item in items)
        print(f"content: {len(items)} entry/entries, {meshes} distinct mesh(es),"
              f" {variants} palette variant(s)")
    else:
        print(render(catalogue.items))

    found = problems(catalogue.items)
    if found:
        print(f"content: {len(found)} problem(s):")
        for problem in found:
            print(f"  {problem}")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
