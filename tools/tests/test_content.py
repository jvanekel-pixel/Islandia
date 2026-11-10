#!/usr/bin/env python3
"""Tests for the content catalogue loader and the id generator.

Run with: make tools-test

Most of these prove the checks can FAIL. The catalogue is the file that decides what exists in the
game, it will be edited by sessions nobody is watching, and the failure mode it is defending
against already happened once: the blueprint's catalogue claimed 631 items and had about six
distinct geometries. A loader that accepts anything would let that back in.
"""

from __future__ import annotations

import contextlib
import io
import sys
import tempfile
import unittest
import zlib
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "content"))

import catalogue as cat  # noqa: E402
import generate_ids  # noqa: E402

VALID = """\
items:
  - id: tree.oak.broad
    name: Broad Oak
    kind: tree
    asset: assets/foliage/tree.oak.broad.glb
    facts: facts/foliage/tree.oak.broad.json
    thumbnail: thumbnails/foliage/tree.oak.broad.png
    collider: box
    source:
      kind: pack
      pack: quaternius-ultimate-nature
      url: https://example.invalid/nature
      licence: CC0-1.0
      licence_file: licences/quaternius-ultimate-nature.txt
    palette:
      set: broadleaf
      variants: [spring, summer, autumn]
"""


def write_catalogue(directory: Path, **files: str) -> Path:
    """Lay out a catalogue directory, defaulting every category not given to empty."""
    for category in cat.CATEGORIES:
        (directory / f"{category}.yaml").write_text(files.get(category, "items: []"), encoding="utf-8")
    for name, text in files.items():
        if name not in cat.CATEGORIES:
            (directory / name).write_text(text, encoding="utf-8")
    return directory


@contextlib.contextmanager
def temporary_catalogue(**files: str):
    with tempfile.TemporaryDirectory() as raw:
        yield write_catalogue(Path(raw), **files)


def problems_for(**files: str) -> list[str]:
    with temporary_catalogue(**files) as directory:
        return list(cat.load(directory).problems)


def only_item(**files: str) -> cat.Item:
    with temporary_catalogue(**files) as directory:
        loaded = cat.load(directory)
    assert loaded.ok, loaded.problems
    assert len(loaded.items) == 1, loaded.items
    return loaded.items[0]


def replacing(line: str, replacement: str) -> str:
    assert line in VALID, f"{line!r} is not in the fixture any more"
    return VALID.replace(line, replacement)


class LoadingAValidEntry(unittest.TestCase):
    def test_every_field_arrives(self) -> None:
        item = only_item(foliage=VALID)
        self.assertEqual(item.id, "tree.oak.broad")
        self.assertEqual(item.name, "Broad Oak")
        self.assertEqual(item.category, "foliage")
        self.assertEqual(item.kind, "tree")
        self.assertEqual(item.collider, "box")
        self.assertEqual(item.source.licence, "CC0-1.0")
        self.assertEqual(item.source.pack, "quaternius-ultimate-nature")
        self.assertEqual(item.palette_set, "broadleaf")
        self.assertEqual(item.palette_variants, ("spring", "summer", "autumn"))
        self.assertEqual(item.sockets, ())

    def test_the_real_catalogue_in_this_repository_loads(self) -> None:
        loaded = cat.load()
        self.assertEqual(loaded.problems, ())

    def test_paths_resolve_under_content(self) -> None:
        item = only_item(foliage=VALID)
        self.assertEqual(item.path("asset"), cat.CONTENT / "assets/foliage/tree.oak.broad.glb")

    def test_palette_and_sockets_are_optional(self) -> None:
        without = VALID.split("    palette:")[0]
        item = only_item(foliage=without)
        self.assertIsNone(item.palette_set)
        self.assertEqual(item.palette_variants, ())


class NumericIds(unittest.TestCase):
    def test_the_number_is_crc32_of_the_id(self) -> None:
        item = only_item(foliage=VALID)
        self.assertEqual(item.numeric_id, zlib.crc32(b"tree.oak.broad"))
        self.assertEqual(item.numeric_id, 0x44CC33DE)

    def test_the_csharp_name_is_derived_from_the_id(self) -> None:
        self.assertEqual(only_item(foliage=VALID).symbol, "TreeOakBroad")

    def test_adding_an_item_renumbers_nothing(self) -> None:
        # The property the blueprint's sequential ids did not have. Saved islands store these
        # numbers, so a renumbering would silently rewrite what someone had built.
        with temporary_catalogue(foliage=VALID) as directory:
            first = {item.id: item.numeric_id for item in cat.load(directory).items}
            extra = VALID.replace("tree.oak.broad", "bush.azalea").replace(
                "Broad Oak", "Azalea").replace("kind: tree", "kind: bush")
            (directory / "foliage.yaml").write_text(VALID + extra.split("items:\n")[1], encoding="utf-8")
            second = {item.id: item.numeric_id for item in cat.load(directory).items}

        self.assertEqual(len(second), 2)
        for identifier, number in first.items():
            self.assertEqual(second[identifier], number, f"{identifier} was renumbered")

    def test_an_id_used_twice_is_refused(self) -> None:
        both = VALID + VALID.split("items:\n")[1]
        problems = problems_for(foliage=both)
        self.assertTrue(any("id already used" in problem for problem in problems), problems)

    def test_a_collision_is_refused(self) -> None:
        # Two different ids hashing to the same number would be the same item as far as every save
        # file is concerned. A real CRC-32 collision between two readable ids is not something to
        # go looking for, so this proves the guard fires rather than that such a pair exists.
        collision = [_Fake("tree.oak.broad", 0x1234), _Fake("bush.azalea", 0x1234)]
        problems = cat._id_problems(collision)
        self.assertTrue(any("collides" in problem for problem in problems), problems)

    def test_zero_is_reserved(self) -> None:
        problems = cat._id_problems([_Fake("tree.oak.broad", 0)])
        self.assertTrue(any("hashes to zero" in problem for problem in problems), problems)

    def test_two_ids_generating_one_csharp_name_are_refused(self) -> None:
        clash = [_Fake("tree.oak", 1, "TreeOak"), _Fake("treeoak", 2, "TreeOak")]
        problems = cat._id_problems(clash)
        self.assertTrue(any("same C# name" in problem for problem in problems), problems)


@dataclass
class _Fake:
    """Stands in for an Item where the test needs to choose the number itself."""

    id: str
    numeric_id: int
    symbol: str = "Whatever"
    category: str = "foliage"


class RefusingBadEntries(unittest.TestCase):
    def assert_refused(self, text: str, fragment: str) -> None:
        problems = problems_for(foliage=text)
        self.assertTrue(
            any(fragment in problem for problem in problems),
            f"expected a problem mentioning {fragment!r}, got {problems}",
        )

    def test_an_unknown_field(self) -> None:
        self.assert_refused(VALID + "    weldable: true\n", "unknown field(s): weldable")

    def test_a_missing_field(self) -> None:
        self.assert_refused(replacing("    collider: box\n", ""), "missing: collider")

    def test_an_id_that_is_not_dotted_lowercase(self) -> None:
        self.assert_refused(replacing("id: tree.oak.broad", "id: Broad_Oak"), "lowercase dot-separated")

    def test_a_kind_from_another_category(self) -> None:
        self.assert_refused(replacing("kind: tree", "kind: roof"), "kind must be one of")

    def test_an_invented_collider(self) -> None:
        self.assert_refused(replacing("collider: box", "collider: cube"), "collider must be one of")

    def test_a_path_that_escapes_content(self) -> None:
        self.assert_refused(
            replacing("asset: assets/foliage/tree.oak.broad.glb", "asset: ../../etc/passwd"),
            "must stay inside content/",
        )

    def test_an_absolute_path(self) -> None:
        self.assert_refused(
            replacing("asset: assets/foliage/tree.oak.broad.glb", "asset: /tmp/thing.glb"),
            "must stay inside content/",
        )

    def test_a_pack_source_with_no_url(self) -> None:
        self.assert_refused(
            replacing("      url: https://example.invalid/nature\n", ""),
            "must record the url",
        )

    def test_a_pack_source_with_no_pack(self) -> None:
        self.assert_refused(
            replacing("      pack: quaternius-ultimate-nature\n", ""),
            "must name its pack",
        )

    def test_a_generated_source_claiming_a_pack(self) -> None:
        self.assert_refused(replacing("kind: pack", "kind: generated"), "generated source has no pack")

    def test_an_empty_palette_variant_list(self) -> None:
        self.assert_refused(
            replacing("      variants: [spring, summer, autumn]", "      variants: []"),
            "at least one variant",
        )

    def test_a_repeated_palette_variant(self) -> None:
        self.assert_refused(
            replacing("      variants: [spring, summer, autumn]", "      variants: [spring, spring]"),
            "repeats a variant",
        )

    def test_a_palette_with_no_set(self) -> None:
        self.assert_refused(replacing("      set: broadleaf\n", ""), "palette.set must name")


class RefusingBadFiles(unittest.TestCase):
    def test_a_missing_category_file(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = write_catalogue(Path(raw))
            (directory / "rocks.yaml").unlink()
            problems = cat.load(directory).problems
        self.assertTrue(any("rocks.yaml: missing" in problem for problem in problems), problems)

    def test_a_category_nobody_declared(self) -> None:
        problems = problems_for(**{"vehicles.yaml": "items: []"})
        self.assertTrue(any("not a known category" in problem for problem in problems), problems)

    def test_yaml_that_will_not_parse(self) -> None:
        problems = problems_for(foliage="items: [\n  - id: broken\n")
        self.assertTrue(any("will not parse" in problem for problem in problems), problems)

    def test_a_file_with_no_items_key(self) -> None:
        problems = problems_for(foliage="catalogue: []")
        self.assertTrue(any("items key" in problem for problem in problems), problems)

    def test_items_that_are_not_a_list(self) -> None:
        problems = problems_for(foliage="items: {id: tree.oak.broad}")
        self.assertTrue(any("must be a list" in problem for problem in problems), problems)

    def test_an_unexpected_top_level_key(self) -> None:
        problems = problems_for(foliage="items: []\nversion: 2")
        self.assertTrue(any("unexpected top-level key" in problem for problem in problems), problems)

    def test_no_catalogue_directory_at_all(self) -> None:
        problems = cat.load(Path("/nonexistent/catalogue")).problems
        self.assertTrue(any("no catalogue directory" in problem for problem in problems), problems)

    def test_every_fault_is_reported_not_just_the_first(self) -> None:
        broken = replacing("collider: box", "collider: cube").replace("kind: tree", "kind: roof")
        problems = problems_for(foliage=broken)
        self.assertGreaterEqual(len(problems), 2, problems)


class GeneratingTheIdFile(unittest.TestCase):
    def test_the_committed_file_is_current(self) -> None:
        # The check CI runs. If this fails, someone edited the catalogue without `make content`.
        with contextlib.redirect_stdout(io.StringIO()) as out:
            code = generate_ids.main(["--check"])
        self.assertEqual(code, 0, out.getvalue())

    def test_the_generated_file_holds_every_id(self) -> None:
        with temporary_catalogue(foliage=VALID) as directory:
            loaded = cat.load(directory)
        rendered = generate_ids.render(loaded)
        self.assertIn("public const uint TreeOakBroad = 0x44cc33deu;", rendered)
        self.assertIn("// tree.oak.broad", rendered)
        self.assertIn("0x44cc33deu, // tree.oak.broad", rendered)

    def test_an_empty_catalogue_still_generates_a_valid_file(self) -> None:
        rendered = generate_ids.render(cat.Catalogue())
        self.assertIn("public static readonly uint[] All = new uint[0];", rendered)
        self.assertIn("namespace Island.Core.Content", rendered)

    def test_a_name_with_markup_in_it_is_escaped(self) -> None:
        item = only_item(foliage=replacing("name: Broad Oak", "name: Oak <broad> & tall"))
        rendered = generate_ids.render(cat.Catalogue(items=(item,)))
        self.assertIn("Oak &lt;broad&gt; &amp; tall", rendered)
        self.assertNotIn("<broad>", rendered)

    def test_generation_is_deterministic(self) -> None:
        with temporary_catalogue(foliage=VALID) as directory:
            loaded = cat.load(directory)
        self.assertEqual(generate_ids.render(loaded), generate_ids.render(loaded))


if __name__ == "__main__":
    unittest.main()
