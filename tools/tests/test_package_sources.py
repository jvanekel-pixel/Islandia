#!/usr/bin/env python3
"""Tests for the Unity package-source collector.

Run with: make tools-test

The collector decides what a later session gets to read when it needs a real API signature. If it
quietly keeps nothing, or keeps everything, the session either guesses or downloads a few hundred
megabytes — so what it keeps and drops is worth pinning.
"""

from __future__ import annotations

import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "unity"))

import collect_package_sources as collector  # noqa: E402


def package(cache: Path, name: str, version: str, files: dict[str, str]) -> Path:
    directory = cache / f"{name}@somehash"
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "package.json").write_text(
        json.dumps({"name": name, "version": version}), encoding="utf-8")
    for relative, body in files.items():
        path = directory / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(body, encoding="utf-8")
    return directory


def run(cache: Path, out: Path) -> tuple[int, str]:
    captured = io.StringIO()
    with contextlib.redirect_stdout(captured):
        code = collector.main([str(cache), str(out)])
    return code, captured.getvalue()


class TheCollector(unittest.TestCase):
    def setUp(self) -> None:
        self.room = tempfile.TemporaryDirectory()
        self.cache = Path(self.room.name) / "PackageCache"
        self.out = Path(self.room.name) / "out"
        self.cache.mkdir()

    def tearDown(self) -> None:
        self.room.cleanup()

    def test_it_keeps_the_sources_of_a_package_worth_reading(self) -> None:
        package(self.cache, "com.unity.render-pipelines.universal", "17.3.0", {
            "Runtime/UniversalRenderPipelineAsset.cs": "public class Asset {}",
            "Shaders/Lit.shader": "Shader x {}",
            "Runtime/Common.hlsl": "// hlsl",
        })
        code, output = run(self.cache, self.out)

        self.assertEqual(code, 0, output)
        kept = self.out / "com.unity.render-pipelines.universal@17.3.0"
        self.assertTrue((kept / "Runtime/UniversalRenderPipelineAsset.cs").is_file())
        self.assertTrue((kept / "Shaders/Lit.shader").is_file())
        self.assertTrue((kept / "Runtime/Common.hlsl").is_file())
        self.assertTrue((kept / "package.json").is_file())

    def test_it_drops_tests_and_samples(self) -> None:
        # They carry no API surface worth the bytes, and URP's test folders are large.
        package(self.cache, "com.unity.xr.openxr", "1.19.0", {
            "Runtime/OpenXRSettings.cs": "public class Settings {}",
            "Tests/Runtime/SettingsTests.cs": "public class T {}",
            "Samples~/Demo/Demo.cs": "public class D {}",
        })
        run(self.cache, self.out)

        kept = self.out / "com.unity.xr.openxr@1.19.0"
        self.assertTrue((kept / "Runtime/OpenXRSettings.cs").is_file())
        self.assertFalse((kept / "Tests/Runtime/SettingsTests.cs").exists())
        self.assertFalse((kept / "Samples~/Demo/Demo.cs").exists())

    def test_it_drops_binaries(self) -> None:
        package(self.cache, "com.unity.burst", "1.8.31", {
            "Runtime/Burst.cs": "public class B {}",
            "Runtime/icon.png": "not really a png",
            "Runtime/thing.dll": "not really a dll",
        })
        run(self.cache, self.out)

        kept = self.out / "com.unity.burst@1.8.31"
        self.assertTrue((kept / "Runtime/Burst.cs").is_file())
        self.assertFalse((kept / "Runtime/icon.png").exists())
        self.assertFalse((kept / "Runtime/thing.dll").exists())

    def test_an_uninteresting_package_is_listed_but_not_copied(self) -> None:
        # Knowing it exists is useful; its sources are not what anyone is stuck on.
        package(self.cache, "com.unity.render-pipelines.universal", "17.3.0",
                {"Runtime/A.cs": "class A {}"})
        package(self.cache, "com.unity.ide.rider", "3.0.0", {"Editor/Rider.cs": "class R {}"})
        run(self.cache, self.out)

        self.assertFalse((self.out / "com.unity.ide.rider@3.0.0").exists())
        index = (self.out / "INDEX.md").read_text()
        self.assertIn("com.unity.ide.rider", index)
        self.assertIn("3.0.0", index)

    def test_it_keeps_assembly_definitions(self) -> None:
        # The name an asmdef must reference is in the asmdef, not in the C#, and cannot be guessed
        # from the namespace. Leaving these out was the first question the artefact could not
        # answer.
        package(self.cache, "com.unity.render-pipelines.universal", "17.3.0", {
            "Runtime/A.cs": "class A {}",
            "Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef":
                '{"name":"Unity.RenderPipelines.Universal.Runtime"}',
        })
        run(self.cache, self.out)

        kept = self.out / "com.unity.render-pipelines.universal@17.3.0"
        self.assertTrue((kept / "Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef").is_file())

    def test_the_index_records_versions(self) -> None:
        package(self.cache, "com.unity.cloud.gltfast", "6.20.0", {"Runtime/G.cs": "class G {}"})
        run(self.cache, self.out)

        index = (self.out / "INDEX.md").read_text()
        self.assertIn("`com.unity.cloud.gltfast`", index)
        self.assertIn("6.20.0", index)
        self.assertIn("| Package | Version | Files kept |", index)

    def test_keeping_nothing_is_a_failure(self) -> None:
        # An empty artefact that uploads happily is worse than a failure: the next session
        # downloads it, finds nothing, and has no idea whether that is the truth.
        package(self.cache, "com.unity.ide.rider", "3.0.0", {"Editor/R.cs": "class R {}"})
        code, output = run(self.cache, self.out)

        self.assertEqual(code, 1)
        self.assertIn("cannot be right", output)

    def test_a_missing_cache_is_a_failure(self) -> None:
        code, output = run(Path("/nonexistent/PackageCache"), self.out)
        self.assertEqual(code, 1)
        self.assertIn("not a directory", output)

    def test_a_package_with_no_manifest_still_gets_a_name(self) -> None:
        # Cache directories are `name@hash`, so the name survives a missing package.json.
        directory = self.cache / "com.unity.xr.hands@deadbeef"
        (directory / "Runtime").mkdir(parents=True)
        (directory / "Runtime/Hands.cs").write_text("class H {}", encoding="utf-8")
        code, output = run(self.cache, self.out)

        self.assertEqual(code, 0, output)
        self.assertTrue((self.out / "com.unity.xr.hands@?" / "Runtime/Hands.cs").is_file())


if __name__ == "__main__":
    unittest.main()
