#!/usr/bin/env python3
"""Tests for the catalogue validator, the licence ledger and the honest count.

Run with: make tools-test

Every one of these builds a complete, valid item on disk — catalogue entry, asset, facts file,
thumbnail, archived licence text — and then breaks exactly one thing. That is the only way to know
a check works: a validator that has never refused anything is indistinguishable from a validator
that cannot.

The catalogue in the repository is empty, so none of this is exercised by the real content yet. It
is exercised here, which is the point of doing it before `C-02` downloads a few hundred meshes.
"""

from __future__ import annotations

import contextlib
import io
import json
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "content"))

import catalogue as cat  # noqa: E402
import ledger  # noqa: E402
import report  # noqa: E402
import validate  # noqa: E402

BUDGETS = """\
lod_reduction: 0.6
foliage:
  tree:   { triangles: 1800, lods: 3, texture: 512, bounds: [8, 14, 8] }
  flower: { triangles: 150,  lods: 1, texture: 256, bounds: [1, 1.5, 1] }
architecture:
  module: { triangles: 900, lods: 2, texture: 1024, bounds: [4, 4, 4] }
"""

LICENCES = """\
decision: "A decision that exists"
allowed:
  - { id: CC0-1.0,    name: CC Zero,       attribution: false, non_commercial: false }
  - { id: CC-BY-4.0,  name: CC Attribution, attribution: true,  non_commercial: false }
  - { id: CC-BY-NC-4.0, name: CC BY-NC,    attribution: true,  non_commercial: true }
  - { id: generated,  name: Generated,     attribution: false, non_commercial: false }
"""

DECISIONS = "# Decisions\n\n## A decision that exists\n\nBecause of reasons.\n"


class Fixture:
    """A content/ directory holding one valid item, which a test then breaks."""

    def __init__(self, root: Path) -> None:
        self.root = root
        (root / "catalogue").mkdir(parents=True, exist_ok=True)
        (root / "assets").mkdir(exist_ok=True)
        (root / "facts").mkdir(exist_ok=True)
        (root / "thumbnails").mkdir(exist_ok=True)
        (root / "licences").mkdir(exist_ok=True)
        (root / "budgets.yaml").write_text(BUDGETS, encoding="utf-8")
        (root / "licences.yaml").write_text(LICENCES, encoding="utf-8")
        (root / "DECISIONS.md").write_text(DECISIONS, encoding="utf-8")
        (root / "licences" / "pack.txt").write_text("CC0, honestly.", encoding="utf-8")
        for category in cat.CATEGORIES:
            (root / "catalogue" / f"{category}.yaml").write_text("items: []", encoding="utf-8")

    def add(self, identifier: str = "tree.oak.broad", *, category: str = "foliage",
            kind: str = "tree", collider: str = "box", licence: str = "CC0-1.0",
            source_kind: str = "pack", attribution: str | None = None, url: str | None = None,
            licence_file: str | None = "licences/pack.txt",
            variants: list[str] | None = None, asset_bytes: bytes | None = None,
            facts: dict | None = None, thumbnail: bool = True,
            facts_file: bool = True) -> None:
        """Write a complete item. Every argument has a value that passes every check."""
        asset_path = f"assets/{identifier}.glb"
        body = asset_bytes if asset_bytes is not None else f"glb:{identifier}".encode()
        (self.root / asset_path).write_bytes(body)
        checksum = f"{zlib.crc32(body) & 0xFFFFFFFF:08x}"

        if thumbnail:
            (self.root / f"thumbnails/{identifier}.png").write_bytes(b"png")

        if facts_file:
            document = facts if facts is not None else self.facts(identifier, asset_path, checksum,
                                                                 collider=collider)
            (self.root / f"facts/{identifier}.json").write_text(
                json.dumps(document), encoding="utf-8")

        entry = {
            "id": identifier, "name": identifier.replace(".", " ").title(), "kind": kind,
            "asset": asset_path, "facts": f"facts/{identifier}.json",
            "thumbnail": f"thumbnails/{identifier}.png", "collider": collider,
            "source": {"kind": source_kind, "licence": licence},
        }
        if source_kind == "pack":
            entry["source"]["pack"] = "a-pack"
            entry["source"]["url"] = url or "https://example.invalid/pack"
        elif url:
            entry["source"]["url"] = url
        if attribution:
            entry["source"]["attribution"] = attribution
        if licence_file and source_kind == "pack":
            entry["source"]["licence_file"] = licence_file
        if variants:
            entry["palette"] = {"set": "a-set", "variants": variants}

        path = self.root / "catalogue" / f"{category}.yaml"
        import yaml
        document = yaml.safe_load(path.read_text(encoding="utf-8")) or {"items": []}
        (document["items"] or []).append(entry) if document["items"] else document.update(
            items=[entry])
        path.write_text(yaml.safe_dump(document, sort_keys=False), encoding="utf-8")

    @staticmethod
    def facts(identifier: str, asset_path: str, checksum: str, *, collider: str = "box") -> dict:
        return {
            "id": identifier,
            "generator": "a test",
            "asset": {"path": asset_path, "crc32": checksum, "bytes": 16},
            "lods": [
                {"level": 0, "triangles": 1200, "vertices": 700},
                {"level": 1, "triangles": 600, "vertices": 350},
                {"level": 2, "triangles": 200, "vertices": 120},
            ],
            "bounds": {"size": [2.4, 5.0, 2.4]},
            "topology": {"boundaryEdges": 0, "nonManifoldEdges": 0},
            "textures": [{"path": "a.png", "width": 512, "height": 512}],
            "collider": {"type": collider, "triangles": 12},
            "thumbnail": {"path": f"thumbnails/{identifier}.png", "width": 256, "height": 256},
        }

    def load(self) -> cat.Catalogue:
        return cat.load(self.root / "catalogue")

    def validate(self) -> list[str]:
        budgets, reduction, problems = validate.load_budgets(self.root / "budgets.yaml")
        return problems + validate.validate(self.load().items, budgets, reduction)

    def licence_problems(self) -> list[str]:
        licences, problems = ledger.load_licences(self.root / "licences.yaml",
                                                  self.root / "DECISIONS.md")
        return problems + ledger.check(self.load().items, licences)


@contextlib.contextmanager
def fixture():
    """A fixture with catalogue.CONTENT and ledger.CONTENT pointed at it."""
    with tempfile.TemporaryDirectory() as raw:
        root = Path(raw)
        original_cat, original_ledger = cat.CONTENT, ledger.CONTENT
        cat.CONTENT, ledger.CONTENT = root, root
        try:
            yield Fixture(root)
        finally:
            cat.CONTENT, ledger.CONTENT = original_cat, original_ledger


class AValidItemPasses(unittest.TestCase):
    def test_nothing_is_wrong_with_the_fixture(self) -> None:
        # If this fails every other test in the file is meaningless, because they all work by
        # breaking one thing about this item.
        with fixture() as f:
            f.add()
            self.assertEqual(f.load().problems, ())
            self.assertEqual(f.validate(), [])
            self.assertEqual(f.licence_problems(), [])

    def test_an_empty_catalogue_passes(self) -> None:
        with fixture() as f:
            self.assertEqual(f.validate(), [])
            self.assertEqual(f.licence_problems(), [])


class TheValidatorRefuses(unittest.TestCase):
    def assert_refused(self, build, fragment: str) -> None:
        with fixture() as f:
            build(f)
            problems = f.validate()
        self.assertTrue(any(fragment in problem for problem in problems),
                        f"expected a problem mentioning {fragment!r}, got {problems}")

    def test_a_missing_facts_file(self) -> None:
        self.assert_refused(lambda f: f.add(facts_file=False), "no facts file")

    def test_facts_that_will_not_parse(self) -> None:
        def build(f):
            f.add()
            (f.root / "facts/tree.oak.broad.json").write_text("{not json", encoding="utf-8")
        self.assert_refused(build, "will not parse")

    def test_facts_describing_a_different_item(self) -> None:
        def build(f):
            f.add()
            path = f.root / "facts/tree.oak.broad.json"
            document = json.loads(path.read_text())
            document["id"] = "bush.azalea"
            path.write_text(json.dumps(document), encoding="utf-8")
        self.assert_refused(build, "not this item")

    def test_facts_missing_a_section(self) -> None:
        def build(f):
            f.add()
            path = f.root / "facts/tree.oak.broad.json"
            document = json.loads(path.read_text())
            del document["topology"]
            path.write_text(json.dumps(document), encoding="utf-8")
        self.assert_refused(build, "is missing: topology")

    def test_facts_whose_checksum_does_not_match_the_asset(self) -> None:
        # The check that makes every other number in the facts trustworthy. Without it, facts
        # could describe any file at all.
        def build(f):
            f.add()
            (f.root / "assets/tree.oak.broad.glb").write_bytes(b"a different mesh entirely")
        self.assert_refused(build, "facts are for a different asset")

    def test_a_missing_asset(self) -> None:
        def build(f):
            f.add()
            (f.root / "assets/tree.oak.broad.glb").unlink()
        self.assert_refused(build, "no asset at")

    def test_a_kind_with_no_budget(self) -> None:
        self.assert_refused(lambda f: f.add("bush.azalea", kind="bush"), "no budget for")

    def test_too_many_triangles_at_lod0(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d["lods"].__setitem__(
                0, {"level": 0, "triangles": 5000, "vertices": 3000}))
        self.assert_refused(build, "over the 1,800 triangle budget")

    def test_an_lod_chain_that_does_not_reduce(self) -> None:
        # The blueprint's "LODs" were the same mesh three times: all the memory, none of the
        # saving.
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("lods", [
                {"level": 0, "triangles": 1200}, {"level": 1, "triangles": 1200},
                {"level": 2, "triangles": 1200}]))
        self.assert_refused(build, "it is not a LOD")

    def test_the_wrong_number_of_lods(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("lods", [{"level": 0, "triangles": 1200}]))
        self.assert_refused(build, "the budget for this kind is exactly 3")

    def test_lod_levels_out_of_order(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("lods", [
                {"level": 1, "triangles": 1200}, {"level": 0, "triangles": 600},
                {"level": 2, "triangles": 200}]))
        self.assert_refused(build, "LOD levels must run")

    def test_a_texture_over_budget(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "textures", [{"path": "a.png", "width": 2048, "height": 2048}]))
        self.assert_refused(build, "over the 512 budget")

    def test_a_texture_that_is_not_a_power_of_two(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "textures", [{"path": "a.png", "width": 500, "height": 512}]))
        self.assert_refused(build, "not a power of two")

    def test_something_too_big_to_place(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("bounds", {"size": [2.4, 40.0, 2.4]}))
        self.assert_refused(build, "over the 14 m budget")

    def test_something_with_no_size(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("bounds", {"size": [2.4, 0, 2.4]}))
        self.assert_refused(build, "has no size on y")

    def test_non_manifold_geometry(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "topology", {"boundaryEdges": 0, "nonManifoldEdges": 4}))
        self.assert_refused(build, "non-manifold edge")

    def test_open_geometry_that_is_not_declared_double_sided(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "topology", {"boundaryEdges": 12, "nonManifoldEdges": 0}))
        self.assert_refused(build, "walk behind it and see through it")

    def test_open_geometry_that_is_declared_is_allowed(self) -> None:
        # A grass blade and a petal are flat cards seen from both sides. That is legitimate, and
        # the difference between legitimate and broken is that someone said so.
        with fixture() as f:
            f.add()
            self.bend(f, lambda d: d.__setitem__("topology", {
                "boundaryEdges": 12, "nonManifoldEdges": 0, "intentionallyDoubleSided": True}))
            self.assertEqual(f.validate(), [])

    def test_a_collider_the_catalogue_disagrees_with(self) -> None:
        def build(f):
            f.add(collider="box")
            self.bend(f, lambda d: d.__setitem__("collider", {"type": "mesh", "triangles": 400}))
        self.assert_refused(build, "the catalogue says box")

    def test_a_collider_with_no_geometry(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__("collider", {"type": "box", "triangles": 0}))
        self.assert_refused(build, "collider with no geometry")

    def test_a_missing_thumbnail(self) -> None:
        def build(f):
            f.add()
            (f.root / "thumbnails/tree.oak.broad.png").unlink()
        self.assert_refused(build, "the inventory browser has nothing to show")

    def test_a_thumbnail_too_small_to_read(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "thumbnail", {"path": "t.png", "width": 32, "height": 32}))
        self.assert_refused(build, "smaller than 128")

    def test_a_thumbnail_that_is_not_square(self) -> None:
        def build(f):
            f.add()
            self.bend(f, lambda d: d.__setitem__(
                "thumbnail", {"path": "t.png", "width": 256, "height": 180}))
        self.assert_refused(build, "lays out squares")

    @staticmethod
    def bend(f: Fixture, change) -> None:
        """Apply a change to the fixture item's facts file."""
        path = f.root / "facts/tree.oak.broad.json"
        document = json.loads(path.read_text())
        change(document)
        path.write_text(json.dumps(document), encoding="utf-8")


class TheBudgetTableItself(unittest.TestCase):
    def test_a_missing_table(self) -> None:
        _, _, problems = validate.load_budgets(Path("/nonexistent/budgets.yaml"))
        self.assertTrue(any("nothing to check against" in problem for problem in problems))

    def test_an_lod_reduction_that_does_not_reduce(self) -> None:
        with fixture() as f:
            (f.root / "budgets.yaml").write_text(
                BUDGETS.replace("lod_reduction: 0.6", "lod_reduction: 1.5"), encoding="utf-8")
            _, _, problems = validate.load_budgets(f.root / "budgets.yaml")
        self.assertTrue(any("between 0 and 1" in problem for problem in problems), problems)

    def test_a_budget_missing_a_field(self) -> None:
        with fixture() as f:
            (f.root / "budgets.yaml").write_text(
                "lod_reduction: 0.6\nfoliage:\n  tree: { triangles: 100, lods: 1 }\n",
                encoding="utf-8")
            _, _, problems = validate.load_budgets(f.root / "budgets.yaml")
        self.assertTrue(any("is missing: texture, bounds" in problem for problem in problems),
                        problems)

    def test_the_real_table_in_this_repository_loads(self) -> None:
        budgets, reduction, problems = validate.load_budgets()
        self.assertEqual(problems, [])
        self.assertGreater(len(budgets), 20)
        self.assertLess(reduction, 1.0)


class TheLicenceLedgerRefuses(unittest.TestCase):
    def assert_refused(self, build, fragment: str) -> None:
        with fixture() as f:
            build(f)
            problems = f.licence_problems()
        self.assertTrue(any(fragment in problem for problem in problems),
                        f"expected a problem mentioning {fragment!r}, got {problems}")

    def test_a_licence_nobody_allowed(self) -> None:
        self.assert_refused(lambda f: f.add(licence="WTFPL"), "is not in licences.yaml")

    def test_attribution_required_and_no_author(self) -> None:
        self.assert_refused(lambda f: f.add(licence="CC-BY-4.0"), "no author is named")

    def test_attribution_required_and_no_source_url(self) -> None:
        self.assert_refused(
            lambda f: f.add(licence="CC-BY-4.0", attribution="Someone", source_kind="generated"),
            "no source url to attribute to")

    def test_attribution_satisfied_is_allowed(self) -> None:
        with fixture() as f:
            f.add(licence="CC-BY-4.0", attribution="Quaternius")
            self.assertEqual(f.licence_problems(), [])

    def test_a_pack_with_no_archived_licence_text(self) -> None:
        self.assert_refused(lambda f: f.add(licence_file=None),
                            "must record where its licence text is archived")

    def test_a_pack_whose_archived_licence_text_is_gone(self) -> None:
        def build(f):
            f.add()
            (f.root / "licences/pack.txt").unlink()
        self.assert_refused(build, "licence text missing at")

    def test_an_allowlist_citing_a_decision_nobody_wrote(self) -> None:
        with fixture() as f:
            (f.root / "licences.yaml").write_text(
                LICENCES.replace("A decision that exists", "A decision nobody wrote"),
                encoding="utf-8")
            problems = f.licence_problems()
        self.assertTrue(any("means writing down why" in problem for problem in problems), problems)

    def test_an_allowlist_citing_nothing(self) -> None:
        with fixture() as f:
            (f.root / "licences.yaml").write_text(
                LICENCES.replace('decision: "A decision that exists"', "decision: ''"),
                encoding="utf-8")
            problems = f.licence_problems()
        self.assertTrue(any("must cite the DECISIONS.md record" in p for p in problems), problems)

    def test_a_licence_listed_twice(self) -> None:
        with fixture() as f:
            (f.root / "licences.yaml").write_text(
                LICENCES + "  - { id: CC0-1.0, name: Again, attribution: false,"
                           " non_commercial: false }\n", encoding="utf-8")
            _, problems = ledger.load_licences(f.root / "licences.yaml", f.root / "DECISIONS.md")
        self.assertTrue(any("listed twice" in problem for problem in problems), problems)

    def test_the_real_allowlist_cites_a_real_decision(self) -> None:
        _, problems = ledger.load_licences()
        self.assertEqual(problems, [])


class TheNotices(unittest.TestCase):
    def test_credits_are_grouped_by_licence_and_author(self) -> None:
        with fixture() as f:
            f.add("tree.oak.broad", licence="CC-BY-4.0", attribution="Quaternius")
            f.add("flower.poppy", category="foliage", kind="flower",
                  licence="CC-BY-4.0", attribution="Quaternius")
            f.add("wall.plain", category="architecture", kind="module", licence="CC0-1.0")
            licences, _ = ledger.load_licences(f.root / "licences.yaml", f.root / "DECISIONS.md")
            text = ledger.render(f.load().items, licences)

        self.assertIn("## CC Attribution (CC-BY-4.0)", text)
        self.assertIn("## CC Zero (CC0-1.0)", text)
        self.assertIn("[Quaternius](https://example.invalid/pack)** — 2 item(s)", text)

    def test_non_commercial_items_are_called_out(self) -> None:
        with fixture() as f:
            f.add(licence="CC-BY-NC-4.0", attribution="Someone")
            licences, _ = ledger.load_licences(f.root / "licences.yaml", f.root / "DECISIONS.md")
            text = ledger.render(f.load().items, licences)
        self.assertIn("## Non-commercial licences", text)
        self.assertIn("never sold", text)
        self.assertIn("`tree.oak.broad`", text)

    def test_the_committed_notices_file_is_current(self) -> None:
        with contextlib.redirect_stdout(io.StringIO()) as out:
            code = ledger.main(["--check"])
        self.assertEqual(code, 0, out.getvalue())


class TheHonestCount(unittest.TestCase):
    def test_entries_sharing_a_mesh_count_as_one_mesh(self) -> None:
        # The blueprint's failure, in miniature: two entries, one geometry. The report must say
        # one mesh, not two items.
        with fixture() as f:
            f.add("tree.oak.spring", asset_bytes=b"one mesh", variants=["spring"])
            f.add("tree.oak.autumn", asset_bytes=b"one mesh", variants=["autumn"])
            items = f.load().items
            text = report.render(items)

        self.assertEqual(len({report.mesh_of(item) for item in items}), 1)
        self.assertIn("entries               2", text)
        self.assertIn("distinct meshes       1", text)

    def test_palette_variants_are_counted_separately(self) -> None:
        with fixture() as f:
            f.add(variants=["spring", "summer", "autumn", "winter"])
            text = report.render(f.load().items)
        self.assertIn("distinct meshes       1", text)
        self.assertIn("palette variants      4", text)

    def test_an_entry_with_no_palette_is_one_variant(self) -> None:
        with fixture() as f:
            f.add()
            self.assertEqual(report.variants_of(f.load().items[0]), 1)

    def test_one_mesh_across_two_kinds_is_refused(self) -> None:
        # Sharing geometry between a spring and an autumn oak is the system working. Sharing it
        # between a stair and a roof is what the blueprint did 95 times.
        with fixture() as f:
            f.add("tree.oak.broad", asset_bytes=b"same box")
            f.add("flower.poppy", category="foliage", kind="flower", asset_bytes=b"same box")
            problems = report.problems(f.load().items)
        self.assertTrue(any("shared across kinds" in problem for problem in problems), problems)

    def test_one_mesh_within_one_kind_is_allowed(self) -> None:
        with fixture() as f:
            f.add("tree.oak.spring", asset_bytes=b"same tree", variants=["spring"])
            f.add("tree.oak.autumn", asset_bytes=b"same tree", variants=["autumn"])
            self.assertEqual(report.problems(f.load().items), [])

    def test_an_entry_with_unreadable_facts_is_named_not_silently_dropped(self) -> None:
        with fixture() as f:
            f.add(facts_file=False)
            text = report.render(f.load().items)
        self.assertIn("no readable facts", text)
        self.assertIn("tree.oak.broad", text)

    def test_the_report_shows_progress_against_the_target(self) -> None:
        with fixture() as f:
            f.add()
            text = report.render(f.load().items)
        self.assertIn(f"target of about {report.TARGET_MESHES}", text)

    def test_every_category_appears_even_when_empty(self) -> None:
        with fixture() as f:
            text = report.render(f.load().items)
        for category in cat.CATEGORIES:
            self.assertIn(category, text)


if __name__ == "__main__":
    unittest.main()
