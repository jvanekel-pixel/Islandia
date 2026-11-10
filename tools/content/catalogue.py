#!/usr/bin/env python3
"""Load and validate the content catalogue, and derive the ids runtime code uses.

The catalogue (`content/catalogue/*.yaml`, documented in that directory's README) is the source of
truth for every placeable thing. This module is the only thing that reads it, so there is one
parser, one schema, and one set of error messages rather than a loader per consumer.

Two jobs, kept together because the second depends on the first being exact:

**Schema validation.** Hand-rolled rather than delegated to a schema library, for two reasons: the
tooling has exactly one pinned dependency and adding another for this is a poor trade, and the
messages matter more than the generality. "foliage.yaml item 3 (tree.oak.broad): collider must be
one of box, capsule, mesh, none — got 'cube'" is worth more than a JSON pointer, because nobody is
watching the output and the next session has only the message to go on.

**Id derivation.** `CRC-32` of the id string's UTF-8 bytes. Stable across additions, computable in
both Python and C#, and collisions are a hard failure. See the README for why the blueprint's
sequential integers could not be kept.
"""

from __future__ import annotations

import re
import zlib
from dataclasses import dataclass, field
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
CONTENT = REPO / "content"
CATALOGUE = CONTENT / "catalogue"

#: The category files that may exist. A category is the file an entry lives in, not a field on the
#: entry, so there is one place for it to be wrong instead of two.
CATEGORIES = ("foliage", "architecture", "props", "rocks", "media")

#: Sub-types within a category. These select the budget in `content/budgets.yaml`, so an unknown
#: kind would silently escape budget checking — hence the closed list.
KINDS = {
    "foliage": ("tree", "bush", "flower", "grass", "vine"),
    "architecture": ("module", "roof", "stair", "door", "window", "pavilion"),
    "props": ("furniture", "lighting", "garden", "decor", "toy"),
    "rocks": ("boulder", "stone", "cliff", "driftwood", "shell"),
    "media": ("screen", "speaker", "radio"),
}

COLLIDERS = ("box", "capsule", "mesh", "none")
SOURCE_KINDS = ("pack", "generated")

ID = re.compile(r"^[a-z0-9]+(\.[a-z0-9]+)*$")
PACK = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")

REQUIRED = ("id", "name", "kind", "asset", "facts", "thumbnail", "collider", "source")
OPTIONAL = ("palette", "sockets")
SOURCE_REQUIRED = ("kind", "licence")
SOURCE_OPTIONAL = ("pack", "url", "licence_file", "attribution")


@dataclass
class Source:
    kind: str
    licence: str
    pack: str | None = None
    url: str | None = None
    licence_file: str | None = None
    attribution: str | None = None


@dataclass
class Item:
    id: str
    name: str
    category: str
    kind: str
    asset: str
    facts: str
    thumbnail: str
    collider: str
    source: Source
    palette_set: str | None = None
    palette_variants: tuple[str, ...] = ()
    sockets: tuple[str, ...] = ()

    @property
    def numeric_id(self) -> int:
        """CRC-32 of the id's UTF-8 bytes. Matches `Island.Core.Save.Crc32`."""
        return zlib.crc32(self.id.encode("utf-8")) & 0xFFFFFFFF

    @property
    def symbol(self) -> str:
        """The C# constant name: `tree.oak.broad` -> `TreeOakBroad`."""
        return "".join(part.capitalize() for part in self.id.replace(".", " ").split())

    def path(self, which: str) -> Path:
        return CONTENT / getattr(self, which)


@dataclass
class Catalogue:
    items: tuple[Item, ...] = ()
    problems: tuple[str, ...] = ()

    @property
    def ok(self) -> bool:
        return not self.problems

    def by_category(self) -> dict[str, list[Item]]:
        grouped: dict[str, list[Item]] = {category: [] for category in CATEGORIES}
        for item in self.items:
            grouped.setdefault(item.category, []).append(item)
        return grouped


@dataclass
class _Reader:
    """Accumulates problems rather than raising, so one run reports every fault."""

    problems: list[str] = field(default_factory=list)

    def complain(self, where: str, message: str) -> None:
        self.problems.append(f"{where}: {message}")

    def read_source(self, where: str, raw: object) -> Source | None:
        if not isinstance(raw, dict):
            self.complain(where, "source must be a mapping")
            return None

        unknown = set(raw) - set(SOURCE_REQUIRED) - set(SOURCE_OPTIONAL)
        if unknown:
            self.complain(where, f"source has unknown field(s): {', '.join(sorted(unknown))}")
        missing = [name for name in SOURCE_REQUIRED if name not in raw]
        if missing:
            self.complain(where, f"source is missing: {', '.join(missing)}")
            return None

        kind = raw["kind"]
        if kind not in SOURCE_KINDS:
            self.complain(where, f"source.kind must be one of {', '.join(SOURCE_KINDS)} — got {kind!r}")
            return None

        pack = raw.get("pack")
        if kind == "pack":
            if not pack:
                self.complain(where, "a pack source must name its pack")
            elif not PACK.match(str(pack)):
                self.complain(where, f"source.pack must be lowercase-hyphenated — got {pack!r}")
            if not raw.get("url"):
                self.complain(where, "a pack source must record the url it came from")
        elif pack:
            self.complain(where, "a generated source has no pack")

        licence = raw["licence"]
        if not isinstance(licence, str) or not licence.strip():
            self.complain(where, "source.licence must be a licence identifier")
            return None

        return Source(
            kind=kind,
            licence=licence,
            pack=str(pack) if pack else None,
            url=str(raw["url"]) if raw.get("url") else None,
            licence_file=str(raw["licence_file"]) if raw.get("licence_file") else None,
            attribution=str(raw["attribution"]) if raw.get("attribution") else None,
        )

    def read_item(self, category: str, index: int, raw: object) -> Item | None:
        where = f"{category}.yaml item {index}"
        if not isinstance(raw, dict):
            self.complain(where, "each item must be a mapping")
            return None

        if isinstance(raw.get("id"), str):
            where = f"{category}.yaml item {index} ({raw['id']})"

        unknown = set(raw) - set(REQUIRED) - set(OPTIONAL)
        if unknown:
            self.complain(where, f"unknown field(s): {', '.join(sorted(unknown))}")
        missing = [name for name in REQUIRED if name not in raw]
        if missing:
            self.complain(where, f"missing: {', '.join(missing)}")
            return None

        identifier = raw["id"]
        if not isinstance(identifier, str) or not ID.match(identifier):
            self.complain(where, "id must be lowercase dot-separated, e.g. tree.oak.broad")
            return None

        name = raw["name"]
        if not isinstance(name, str) or not name.strip():
            self.complain(where, "name must be the label a person sees")

        kind = raw["kind"]
        allowed = KINDS[category]
        if kind not in allowed:
            self.complain(where, f"kind must be one of {', '.join(allowed)} — got {kind!r}")

        collider = raw["collider"]
        if collider not in COLLIDERS:
            self.complain(where, f"collider must be one of {', '.join(COLLIDERS)} — got {collider!r}")

        for which in ("asset", "facts", "thumbnail"):
            value = raw[which]
            if not isinstance(value, str) or not value.strip():
                self.complain(where, f"{which} must be a path relative to content/")
            elif value.startswith("/") or ".." in Path(value).parts:
                self.complain(where, f"{which} must stay inside content/ — got {value!r}")

        source = self.read_source(where, raw["source"])
        if source is None:
            return None

        palette_set, variants = None, ()
        if "palette" in raw:
            palette = raw["palette"]
            if not isinstance(palette, dict):
                self.complain(where, "palette must be a mapping of set and variants")
            else:
                palette_set = palette.get("set")
                raw_variants = palette.get("variants", [])
                if not isinstance(palette_set, str) or not palette_set:
                    self.complain(where, "palette.set must name a palette set")
                if not isinstance(raw_variants, list) or not raw_variants:
                    self.complain(where, "palette.variants must list at least one variant")
                else:
                    variants = tuple(str(variant) for variant in raw_variants)
                    if len(set(variants)) != len(variants):
                        self.complain(where, "palette.variants repeats a variant")

        sockets = ()
        if "sockets" in raw:
            raw_sockets = raw["sockets"]
            if not isinstance(raw_sockets, list):
                self.complain(where, "sockets must be a list of names")
            else:
                sockets = tuple(str(socket) for socket in raw_sockets)
                if len(set(sockets)) != len(sockets):
                    self.complain(where, "sockets repeats a name")

        if any(problem.startswith(where) for problem in self.problems):
            return None

        return Item(
            id=identifier, name=name, category=category, kind=kind,
            asset=raw["asset"], facts=raw["facts"], thumbnail=raw["thumbnail"],
            collider=collider, source=source,
            palette_set=palette_set, palette_variants=variants, sockets=sockets,
        )


def load(catalogue_dir: Path | None = None) -> Catalogue:
    """Read every category file. Reports every fault it finds rather than the first."""
    directory = catalogue_dir or CATALOGUE
    reader = _Reader()
    items: list[Item] = []

    if not directory.is_dir():
        return Catalogue(problems=(f"{directory}: no catalogue directory",))

    stray = sorted(
        path.name for path in directory.glob("*.yaml") if path.stem not in CATEGORIES
    )
    for name in stray:
        reader.complain(name, f"not a known category — expected one of {', '.join(CATEGORIES)}")

    for category in CATEGORIES:
        path = directory / f"{category}.yaml"
        if not path.is_file():
            reader.complain(f"{category}.yaml", "missing — every category needs a file, even empty")
            continue

        try:
            document = yaml.safe_load(path.read_text(encoding="utf-8"))
        except yaml.YAMLError as error:
            reader.complain(f"{category}.yaml", f"will not parse: {error}")
            continue

        if document is None or not isinstance(document, dict) or "items" not in document:
            reader.complain(f"{category}.yaml", "must be a mapping with an items key")
            continue
        if set(document) != {"items"}:
            extra = ", ".join(sorted(set(document) - {"items"}))
            reader.complain(f"{category}.yaml", f"has unexpected top-level key(s): {extra}")
        if document["items"] is None:
            continue
        if not isinstance(document["items"], list):
            reader.complain(f"{category}.yaml", "items must be a list")
            continue

        for index, raw in enumerate(document["items"], start=1):
            item = reader.read_item(category, index, raw)
            if item is not None:
                items.append(item)

    items.sort(key=lambda item: item.id)
    reader.problems.extend(_id_problems(items))
    return Catalogue(items=tuple(items), problems=tuple(reader.problems))


def _id_problems(items: list[Item]) -> list[str]:
    """Ids must be unique as strings, unique as numbers, and never zero."""
    problems: list[str] = []
    seen: dict[str, Item] = {}
    numbers: dict[int, Item] = {}
    symbols: dict[str, Item] = {}

    for item in items:
        if item.id in seen:
            problems.append(
                f"{item.category}.yaml ({item.id}): id already used in "
                f"{seen[item.id].category}.yaml"
            )
            continue
        seen[item.id] = item

        if item.numeric_id == 0:
            # Zero means "nothing" wherever a numeric id is stored, so an item that hashes to it
            # would be indistinguishable from an empty slot in a saved island.
            problems.append(f"{item.id}: hashes to zero, which is reserved — rename it")
        elif item.numeric_id in numbers:
            problems.append(
                f"{item.id}: CRC-32 collides with {numbers[item.numeric_id].id} "
                f"({item.numeric_id:#010x}) — rename one of them"
            )
        else:
            numbers[item.numeric_id] = item

        if item.symbol in symbols:
            problems.append(
                f"{item.id}: generates the same C# name as {symbols[item.symbol].id} "
                f"({item.symbol})"
            )
        else:
            symbols[item.symbol] = item

    return problems
