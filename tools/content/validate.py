#!/usr/bin/env python3
"""Check every catalogue item against its budget, and against the asset it claims.

Each item has a **facts file**: a small JSON document the ingest pipeline writes beside the asset,
recording what the mesh actually is — triangle counts per LOD, bounds, texture sizes, edge counts,
collider. The validator reads those rather than re-deriving them from the GLB, because Blender has
exact topology at the moment it exports and reconstructing it afterwards is both lossy and a second
implementation to keep correct.

That only works if the facts cannot describe a different file from the one shipped, so every facts
file carries the CRC-32 of its asset and the validator recomputes it. Facts and asset are bound
together or the item is refused; the pipeline that writes them is deterministic (`C-02`), so there
is no legitimate way for them to disagree.

Budgets live in `content/budgets.yaml` as data, not here as code, so that recalibrating them from
device readings is an edit to a table rather than a change to a program.
"""

from __future__ import annotations

import json
import sys
import zlib
from dataclasses import dataclass
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))

from catalogue import CONTENT, REPO, Item  # noqa: E402

BUDGETS = CONTENT / "budgets.yaml"

#: Smallest acceptable thumbnail. Small enough to be cheap, large enough to be recognisable in the
#: inventory browser at arm's length on a Quest 2 panel.
MIN_THUMBNAIL = 128

FACTS_REQUIRED = ("id", "generator", "asset", "lods", "bounds", "topology", "textures",
                  "collider", "thumbnail")


@dataclass
class Budget:
    triangles: int
    lods: int
    texture: int
    bounds: tuple[float, float, float]


def load_budgets(path: Path | None = None) -> tuple[dict[tuple[str, str], Budget], float, list[str]]:
    """Read the budget table. Returns (budgets by category+kind, lod_reduction, problems)."""
    source = path or BUDGETS
    problems: list[str] = []

    if not source.is_file():
        return {}, 0.0, [f"{source.name}: missing — the validator has nothing to check against"]

    try:
        document = yaml.safe_load(source.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:
        return {}, 0.0, [f"{source.name}: will not parse: {error}"]

    if not isinstance(document, dict):
        return {}, 0.0, [f"{source.name}: must be a mapping"]

    reduction = document.pop("lod_reduction", None)
    if not isinstance(reduction, (int, float)) or not 0 < reduction < 1:
        problems.append(f"{source.name}: lod_reduction must be between 0 and 1")
        reduction = 1.0

    budgets: dict[tuple[str, str], Budget] = {}
    for category, kinds in document.items():
        if not isinstance(kinds, dict):
            problems.append(f"{source.name}: {category} must be a mapping of kind to budget")
            continue
        for kind, raw in kinds.items():
            where = f"{source.name}: {category}.{kind}"
            if not isinstance(raw, dict):
                problems.append(f"{where} must be a mapping")
                continue
            missing = [f for f in ("triangles", "lods", "texture", "bounds") if f not in raw]
            if missing:
                problems.append(f"{where} is missing: {', '.join(missing)}")
                continue
            bounds = raw["bounds"]
            if not isinstance(bounds, list) or len(bounds) != 3:
                problems.append(f"{where}: bounds must be three numbers, x/y/z in metres")
                continue
            budgets[(category, kind)] = Budget(
                triangles=int(raw["triangles"]), lods=int(raw["lods"]),
                texture=int(raw["texture"]), bounds=tuple(float(value) for value in bounds),
            )

    return budgets, float(reduction), problems


def validate(items: tuple[Item, ...], budgets: dict[tuple[str, str], Budget],
             reduction: float) -> list[str]:
    """Every problem across every item. An empty list means the catalogue is shippable."""
    problems: list[str] = []
    for item in items:
        problems.extend(_validate_one(item, budgets, reduction))
    return problems


def _validate_one(item: Item, budgets: dict[tuple[str, str], Budget],
                  reduction: float) -> list[str]:
    problems: list[str] = []

    def complain(message: str) -> None:
        problems.append(f"{item.id}: {message}")

    budget = budgets.get((item.category, item.kind))
    if budget is None:
        complain(f"no budget for {item.category}.{item.kind} in budgets.yaml")
        return problems

    facts, faults = read_facts(item)
    if faults:
        return [f"{item.id}: {fault}" for fault in faults]
    assert facts is not None

    problems.extend(_check_asset(item, facts, complain) or [])
    _check_lods(facts, budget, reduction, complain)
    _check_textures(facts, budget, complain)
    _check_bounds(facts, budget, complain)
    _check_topology(facts, complain)
    _check_collider(item, facts, complain)
    _check_thumbnail(item, facts, complain)
    return problems


def read_facts(item: Item) -> tuple[dict | None, list[str]]:
    """Read and shape-check an item's facts file."""
    path = item.path("facts")
    if not path.is_file():
        return None, [f"no facts file at {item.facts} — the ingest pipeline writes one per item"]

    try:
        facts = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        return None, [f"{item.facts} will not parse: {error}"]

    if not isinstance(facts, dict):
        return None, [f"{item.facts} must be a JSON object"]

    missing = [name for name in FACTS_REQUIRED if name not in facts]
    if missing:
        return None, [f"{item.facts} is missing: {', '.join(missing)}"]

    if facts["id"] != item.id:
        return None, [f"{item.facts} describes {facts['id']!r}, not this item"]

    return facts, []


def _check_asset(item: Item, facts: dict, complain) -> list[str]:
    """The facts must describe the asset that ships, byte for byte."""
    declared = facts["asset"]
    if not isinstance(declared, dict) or "path" not in declared or "crc32" not in declared:
        complain("facts asset must carry a path and a crc32")
        return []

    if declared["path"] != item.asset:
        complain(f"facts name asset {declared['path']!r}, the catalogue says {item.asset!r}")

    path = item.path("asset")
    if not path.is_file():
        complain(f"no asset at {item.asset}")
        return []

    actual = f"{zlib.crc32(path.read_bytes()) & 0xFFFFFFFF:08x}"
    if str(declared["crc32"]).lower() != actual:
        # Either the asset changed without the pipeline re-running, or the facts were edited by
        # hand. Both mean every number below describes a file that is not the one shipping.
        complain(f"facts are for a different asset (crc32 {declared['crc32']}, file is {actual})"
                 " — re-run the ingest pipeline")
    return []


def _check_lods(facts: dict, budget: Budget, reduction: float, complain) -> None:
    lods = facts["lods"]
    if not isinstance(lods, list) or not lods:
        complain("facts must list at least one LOD")
        return

    for index, lod in enumerate(lods):
        if not isinstance(lod, dict) or "level" not in lod or "triangles" not in lod:
            complain(f"LOD {index} must carry a level and a triangle count")
            return
        if lod["level"] != index:
            complain(f"LOD levels must run 0, 1, 2… — found {lod['level']} at position {index}")
            return

    if len(lods) != budget.lods:
        complain(f"{len(lods)} LOD(s), the budget for this kind is exactly {budget.lods}")

    first = int(lods[0]["triangles"])
    if first > budget.triangles:
        complain(f"LOD0 is {first:,} triangles, over the {budget.triangles:,} triangle budget")

    # A chain that does not reduce is the defect this is here for: the blueprint shipped "LODs"
    # that were the same mesh repeated, which costs the memory of three LODs and saves nothing.
    for index in range(1, len(lods)):
        previous = int(lods[index - 1]["triangles"])
        current = int(lods[index]["triangles"])
        allowed = int(previous * reduction)
        if current > allowed:
            complain(f"LOD{index} is {current:,} triangles against LOD{index - 1}'s {previous:,}"
                     f" — must be {allowed:,} or fewer, or it is not a LOD")


def _check_textures(facts: dict, budget: Budget, complain) -> None:
    textures = facts["textures"]
    if not isinstance(textures, list):
        complain("facts textures must be a list")
        return

    for texture in textures:
        if not isinstance(texture, dict) or "width" not in texture or "height" not in texture:
            complain("each texture must carry a width and a height")
            continue
        name = texture.get("path", "a texture")
        width, height = int(texture["width"]), int(texture["height"])
        if width > budget.texture or height > budget.texture:
            complain(f"{name} is {width}×{height}, over the {budget.texture} budget")
        for label, value in (("width", width), ("height", height)):
            if value < 1 or value & (value - 1):
                # Not pedantry: ASTC blocks and the mip chain both want powers of two, and a
                # non-conforming texture is silently rescaled or left without mips on device.
                complain(f"{name} has a {label} of {value}, which is not a power of two")


def _check_bounds(facts: dict, budget: Budget, complain) -> None:
    bounds = facts["bounds"]
    if not isinstance(bounds, dict) or "size" not in bounds:
        complain("facts bounds must carry a size")
        return
    size = bounds["size"]
    if not isinstance(size, list) or len(size) != 3:
        complain("facts bounds size must be three numbers, x/y/z in metres")
        return

    for axis, value, limit in zip("xyz", size, budget.bounds):
        if float(value) > limit:
            complain(f"is {float(value):.2f} m on {axis}, over the {limit:g} m budget"
                     " — it will not place or snap correctly")
        if float(value) <= 0:
            complain(f"has no size on {axis}")


def _check_topology(facts: dict, complain) -> None:
    topology = facts["topology"]
    if not isinstance(topology, dict):
        complain("facts topology must be a mapping")
        return

    for field in ("boundaryEdges", "nonManifoldEdges"):
        if field not in topology:
            complain(f"facts topology is missing {field}")
            return

    if int(topology["nonManifoldEdges"]) > 0:
        # Never acceptable. Non-manifold geometry breaks collider generation, normals and any
        # attempt to weld assemblies together.
        complain(f"has {topology['nonManifoldEdges']} non-manifold edge(s)")

    boundary = int(topology["boundaryEdges"])
    if boundary > 0 and not topology.get("intentionallyDoubleSided", False):
        # Open geometry is fine for a grass blade or a petal, which is a flat card seen from both
        # sides. It is not fine for anything the camera can get behind, and in VR the camera can
        # get behind everything. So it has to be declared rather than assumed.
        complain(f"has {boundary} boundary edge(s) and is not declared double-sided"
                 " — in VR the player can walk behind it and see through it")


def _check_collider(item: Item, facts: dict, complain) -> None:
    collider = facts["collider"]
    if not isinstance(collider, dict) or "type" not in collider:
        complain("facts collider must carry a type")
        return

    if collider["type"] != item.collider:
        complain(f"has a {collider['type']} collider, the catalogue says {item.collider}")

    if item.collider == "none":
        return
    if int(collider.get("triangles", 0)) <= 0:
        complain(f"declares a {item.collider} collider with no geometry")


def _check_thumbnail(item: Item, facts: dict, complain) -> None:
    thumbnail = facts["thumbnail"]
    if not isinstance(thumbnail, dict):
        complain("facts thumbnail must be a mapping")
        return

    if not item.path("thumbnail").is_file():
        complain(f"no thumbnail at {item.thumbnail} — the inventory browser has nothing to show")
        return

    width = int(thumbnail.get("width", 0))
    height = int(thumbnail.get("height", 0))
    if width < MIN_THUMBNAIL or height < MIN_THUMBNAIL:
        complain(f"thumbnail is {width}×{height}, smaller than {MIN_THUMBNAIL}×{MIN_THUMBNAIL}")
    elif width != height:
        complain(f"thumbnail is {width}×{height} — the inventory browser lays out squares")


def main() -> int:
    from catalogue import load

    catalogue = load()
    if not catalogue.ok:
        print("content: the catalogue does not load:")
        for problem in catalogue.problems:
            print(f"  {problem}")
        return 1

    budgets, reduction, problems = load_budgets()
    problems.extend(validate(catalogue.items, budgets, reduction))

    if problems:
        print(f"content: {len(problems)} problem(s):")
        for problem in problems:
            print(f"  {problem}")
        return 1

    print(f"content: {len(catalogue.items)} item(s) within budget"
          f" ({len(budgets)} budgets in {BUDGETS.relative_to(REPO)})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
