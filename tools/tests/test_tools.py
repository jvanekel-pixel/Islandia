#!/usr/bin/env python3
"""Tests for the repository's own tooling.

Run with: make tools-test   (or: python3 -m unittest discover -s tools/tests)

The point of most of these is to prove the checks can FAIL. A linter that only
ever passes tells you nothing, and this project's whole premise is that nobody
is watching the output.

Note that the secret-shaped strings below are assembled at runtime from pieces.
Written as literals they would be real matches, and `check_privacy` scans this
file like any other.
"""

from __future__ import annotations

import contextlib
import io
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools"))
sys.path.insert(0, str(REPO / "tools" / "lint"))

import gen_meta  # noqa: E402
import check_privacy  # noqa: E402


class GuidTests(unittest.TestCase):
    def test_is_32_lowercase_hex(self):
        guid = gen_meta.guid_for("Assets/Island/Runtime/Boot.cs")
        self.assertRegex(guid, r"^[0-9a-f]{32}$")

    def test_same_path_gives_same_guid(self):
        self.assertEqual(
            gen_meta.guid_for("Assets/Island/Runtime/Boot.cs"),
            gen_meta.guid_for("Assets/Island/Runtime/Boot.cs"),
        )

    def test_case_does_not_change_the_guid(self):
        # Unity treats asset paths case-insensitively on Windows and macOS; two
        # machines must not disagree about a file's identity.
        self.assertEqual(
            gen_meta.guid_for("Assets/Island/Boot.cs"),
            gen_meta.guid_for("assets/island/boot.cs"),
        )

    def test_backslashes_are_normalised(self):
        self.assertEqual(
            gen_meta.guid_for("Assets/Island/Boot.cs"),
            gen_meta.guid_for("Assets\\Island\\Boot.cs"),
        )

    def test_different_paths_give_different_guids(self):
        paths = [f"Assets/Island/File{n}.cs" for n in range(500)]
        self.assertEqual(len({gen_meta.guid_for(p) for p in paths}), len(paths))


class MetaBodyTests(unittest.TestCase):
    def test_folder_meta_is_marked_as_a_folder(self):
        body = gen_meta.meta_body("Assets/Island", True)
        self.assertIn("folderAsset: yes", body)
        self.assertIn("DefaultImporter:", body)

    def test_each_handled_type_uses_its_own_importer(self):
        for path, importer in [
            ("Assets/a.cs", "MonoImporter:"),
            ("Assets/a.shader", "ShaderImporter:"),
            ("Assets/a.hlsl", "ShaderIncludeImporter:"),
            ("Assets/a.asmdef", "AssemblyDefinitionImporter:"),
            ("Assets/a.asmref", "AssemblyDefinitionReferenceImporter:"),
            ("Assets/a.json", "TextScriptImporter:"),
        ]:
            with self.subTest(path=path):
                self.assertIn(importer, gen_meta.meta_body(path, False))

    def test_every_meta_carries_the_path_s_guid(self):
        path = "Assets/Island/Runtime/Boot.cs"
        self.assertIn(f"guid: {gen_meta.guid_for(path)}", gen_meta.meta_body(path, False))

    def test_unhandled_type_is_left_for_unity(self):
        # We only write importers we can write correctly. A .png must get its
        # TextureImporter from Unity, with the settings the project wants.
        self.assertIsNone(gen_meta.meta_body("Assets/a.png", False))

    def test_body_starts_with_the_format_version(self):
        self.assertTrue(gen_meta.meta_body("Assets/a.cs", False).startswith("fileFormatVersion: 2\n"))


class GenMetaRunTests(unittest.TestCase):
    """The --check mode has to notice a missing or stale file, not just pass."""

    @staticmethod
    def _run(root, check_only):
        """Call gen_meta.run without letting its progress output into the test log."""
        with contextlib.redirect_stdout(io.StringIO()):
            return gen_meta.run(root, ["Assets"], check_only=check_only)

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        (self.root / "Assets" / "Island").mkdir(parents=True)
        self.script = self.root / "Assets" / "Island" / "Boot.cs"
        self.script.write_text("// test\n", encoding="utf-8")
        self.addCleanup(self._tmp.cleanup)

    def test_check_fails_when_a_meta_is_missing(self):
        self.assertEqual(self._run(self.root, check_only=True), 1)

    def test_write_then_check_passes(self):
        self.assertEqual(self._run(self.root, check_only=False), 0)
        self.assertTrue((self.script.parent / "Boot.cs.meta").exists())
        self.assertEqual(self._run(self.root, check_only=True), 0)

    def test_writing_twice_changes_nothing(self):
        self._run(self.root, check_only=False)
        before = (self.script.parent / "Boot.cs.meta").read_text(encoding="utf-8")
        self._run(self.root, check_only=False)
        self.assertEqual(before, (self.script.parent / "Boot.cs.meta").read_text(encoding="utf-8"))

    def test_check_fails_when_a_meta_is_stale(self):
        self._run(self.root, check_only=False)
        (self.script.parent / "Boot.cs.meta").write_text("guid: wrong\n", encoding="utf-8")
        self.assertEqual(self._run(self.root, check_only=True), 1)

    def test_a_folder_gets_its_own_meta(self):
        self._run(self.root, check_only=False)
        self.assertTrue((self.root / "Assets" / "Island.meta").exists())


def _planted() -> dict[str, str]:
    """Secret-shaped strings, assembled so no literal appears in this file."""
    return {
        "private IPv4": "host = 192." + "168.0.150",
        "private key": "-----BEGIN " + "PRIVATE KEY-----",
        "AWS key id": "AK" + "IA" + "ABCDEFGHIJKLMNOP",
        "GitHub token": "gh" + "p_" + "A" * 40,
        "Anthropic key": "sk-" + "ant-" + "B" * 32,
        "Slack token": "xo" + "xb-" + "1234567890-abcdef",
        "bearer literal": 'Authorization: "Bearer ' + "C" * 24 + '"',
        "assigned secret": 'api_key = "' + "D" * 24 + '"',
        # Split so the literal does not appear here: check_privacy scans this file.
        "Unity licence": "<Lic" + "ense id=" + '"x"',
    }


class PrivacyPatternTests(unittest.TestCase):
    """Every pattern must catch its own kind of leak."""

    def test_each_pattern_catches_a_planted_example(self):
        planted = _planted()
        self.assertEqual(
            sorted(planted), sorted(name for name, _, _ in check_privacy.PATTERNS),
            "a pattern was added or renamed without a test for it",
        )
        for name, pattern, _ in check_privacy.PATTERNS:
            with self.subTest(pattern=name):
                self.assertIsNotNone(re.compile(pattern).search(planted[name]))

    def test_ordinary_prose_does_not_match(self):
        innocent = (
            "The gateway returns a bearer token the device stores in the keystore.\n"
            "Set the server address once; it is never committed. Version 1.2.3.4 of nothing.\n"
            "password: <the one you chose>  # a placeholder, not a secret\n"
        )
        for name, pattern, _ in check_privacy.PATTERNS:
            with self.subTest(pattern=name):
                self.assertIsNone(re.compile(pattern).search(innocent))

    def test_credential_file_names_are_listed(self):
        for suffix in (".ulf", ".keystore", ".jks"):
            self.assertIn(suffix, check_privacy.FORBIDDEN_NAMES)


class LayeringCheckTests(unittest.TestCase):
    """Island.Core must stay engine-free, and the check must notice when it does not."""

    def _run(self) -> subprocess.CompletedProcess:
        return subprocess.run([sys.executable, "tools/lint/check_layering.py"],
                              cwd=REPO, capture_output=True, text=True)

    def test_passes_on_the_repository_as_it_stands(self):
        self.assertEqual(self._run().returncode, 0, self._run().stdout)

    def test_fails_when_a_core_source_reaches_for_the_engine(self):
        probe = REPO / "Packages" / "com.island.core" / "Runtime" / "_layering_probe.cs"
        if not probe.parent.is_dir():
            self.skipTest("Island.Core is not present")
        probe.write_text("using UnityEngine;\n", encoding="utf-8")
        try:
            result = self._run()
            self.assertEqual(result.returncode, 1, "a `using UnityEngine` must fail the check")
            self.assertIn("engine-free", result.stdout)
        finally:
            probe.unlink()


class ScriptsRunCleanTests(unittest.TestCase):
    """Each check passes on the repository as it stands right now."""

    def _run(self, *args: str) -> subprocess.CompletedProcess:
        return subprocess.run([sys.executable, *args], cwd=REPO, capture_output=True, text=True)

    def test_plan_check_passes(self):
        result = self._run("tools/lint/check_plan.py")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_privacy_check_passes(self):
        result = self._run("tools/lint/check_privacy.py")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_markdown_check_passes(self):
        result = self._run("tools/lint/check_markdown.py")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_layering_check_passes(self):
        result = self._run("tools/lint/check_layering.py")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_meta_check_passes(self):
        result = self._run("tools/gen_meta.py", "--check")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
