#!/usr/bin/env python3
"""Gathers the readable sources of the Unity packages the project resolved.

Why this exists: `05-OPERATIONS.md` §5 step 3 says that any Unity, URP, OpenXR or glTFast API a
session has not seen compile in this repository is unverified, and should be checked against the
package source. A session cannot compile Unity code, so every wrong guess costs a ten-minute CI
round trip against a cap of four per work package — reading the signature first is the difference
between one run and four.

It cannot be replaced by downloading from Unity's public registry: that registry's newest URP is
10.10.1 and this project resolves 17.3.0, a Unity 6 package which is not published there. The
resolved cache in CI is the only place those sources exist.

What it keeps: `.cs`, `.shader`, `.hlsl`, `.compute`, `.cginc` and each package's `package.json`.
What it drops: binaries, textures, samples, tests and documentation — none of which answer "what is
this method called", and all of which would make the artefact large enough to be annoying to
download.

Usage:  tools/unity/collect_package_sources.py <PackageCache dir> <output dir>
"""

from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

#: Extensions worth reading to answer an API question.
KEEP = (".cs", ".shader", ".hlsl", ".compute", ".cginc")

#: Directories that carry no API surface worth the bytes.
SKIP_DIRS = {"Tests", "Samples", "Samples~", "Documentation", "Documentation~", ".git"}

#: Packages whose sources a session is likely to need. Everything else is still indexed, so the
#: index tells you a package exists even when its sources were not kept.
INTERESTING = (
    "com.unity.render-pipelines",
    "com.unity.shadergraph",
    "com.unity.xr",
    "com.unity.inputsystem",
    "com.unity.cloud.gltfast",
    "com.unity.cloud.ktx",
    "com.unity.burst",
    "com.unity.collections",
    "com.unity.mathematics",
    "com.unity.ai.navigation",
    "com.unity.test-framework",
)


def version_of(package: Path) -> str:
    manifest = package / "package.json"
    if not manifest.is_file():
        return "?"
    try:
        return json.loads(manifest.read_text(encoding="utf-8")).get("version", "?")
    except (json.JSONDecodeError, OSError):
        return "?"


def name_of(package: Path) -> str:
    manifest = package / "package.json"
    if manifest.is_file():
        try:
            declared = json.loads(manifest.read_text(encoding="utf-8")).get("name")
            if declared:
                return declared
        except (json.JSONDecodeError, OSError):
            pass
    # Cache directories are named `com.unity.thing@hash`; the part before @ is the package.
    return package.name.split("@")[0]


def wanted(name: str) -> bool:
    return any(name.startswith(prefix) for prefix in INTERESTING)


def copy_sources(package: Path, into: Path) -> tuple[int, int]:
    """Copies the readable files. Returns (files, bytes)."""
    files = 0
    size = 0

    for source in package.rglob("*"):
        if not source.is_file() or source.suffix not in KEEP:
            continue
        if any(part in SKIP_DIRS for part in source.relative_to(package).parts):
            continue

        target = into / source.relative_to(package)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        files += 1
        size += source.stat().st_size

    manifest = package / "package.json"
    if manifest.is_file():
        shutil.copy2(manifest, into / "package.json")

    return files, size


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("collect_package_sources: give me the PackageCache directory and an output directory")
        return 2

    cache = Path(argv[0])
    out = Path(argv[1])

    if not cache.is_dir():
        print(f"collect_package_sources: {cache} is not a directory")
        return 1

    out.mkdir(parents=True, exist_ok=True)
    rows: list[tuple[str, str, int, int, bool]] = []

    for package in sorted(p for p in cache.iterdir() if p.is_dir()):
        name = name_of(package)
        version = version_of(package)

        if wanted(name):
            files, size = copy_sources(package, out / f"{name}@{version}")
            rows.append((name, version, files, size, True))
        else:
            rows.append((name, version, 0, 0, False))

    kept = [row for row in rows if row[4]]
    total_files = sum(row[2] for row in rows)
    total_bytes = sum(row[3] for row in rows)

    index = [
        "### Resolved packages",
        "",
        f"{len(rows)} packages resolved; sources kept for {len(kept)} of them "
        f"({total_files:,} files, {total_bytes / (1024 * 1024):.1f} MB).",
        "",
        "Sources are kept only for the packages a session is likely to need an API from. The rest "
        "are listed so you can see they exist — ask for them if one turns out to matter.",
        "",
        "| Package | Version | Files kept |",
        "| :-- | :-- | --: |",
    ]
    for name, version, files, _size, was_kept in rows:
        index.append(f"| `{name}` | {version} | {files if was_kept else '—'} |")

    (out / "INDEX.md").write_text("\n".join(index) + "\n", encoding="utf-8")

    print(f"collect_package_sources: {len(rows)} packages, kept sources for {len(kept)}, "
          f"{total_files:,} files, {total_bytes / (1024 * 1024):.1f} MB")

    if not kept:
        # Every package being uninteresting means the cache was not what we thought it was, and an
        # empty artefact that uploads happily is worse than a failure.
        print("collect_package_sources: nothing was kept, which cannot be right")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
