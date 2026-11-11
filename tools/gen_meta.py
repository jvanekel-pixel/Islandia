#!/usr/bin/env python3
"""Generate Unity .meta files deterministically.

Unity writes a .meta beside every asset, carrying the GUID that every reference
in the project points at. Normally the Editor creates them on import, with a
random GUID. This project never opens the Editor by hand, so instead the GUID is
derived from the asset's project-relative path: the same path always produces the
same GUID, on every machine and in every CI run, and a file that has moved keeps
working as long as nothing referenced it by its old path.

Only files whose importer we can write correctly are handled here. Anything else
is left for Unity to import, which is why `check_meta.py` ignores unknown types
rather than failing on them.

Usage:
    python3 tools/gen_meta.py [--check] [root ...]

    --check   report what is missing or stale and exit 1; write nothing.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import sys
from pathlib import Path

#: Directories whose contents Unity imports. Paths outside these never get a .meta.
ASSET_ROOTS = ("Assets", "Packages")

#: Directories that are never imported, wherever they appear.
SKIP_DIRS = {".git", "Library", "Temp", "obj", "bin", "Logs", "UserSettings", "node_modules", "__pycache__"}

#: Extensions Unity imports but which carry no .meta of their own.
NOT_ASSETS = {".meta"}


#: A .meta carrying one of these is authoritative, whoever wrote it.
GUID_LINE = re.compile(r"^guid: [0-9a-f]{32}$", re.M)


def guid_for(relative_path: str) -> str:
    """A stable 32-character hex GUID for a project-relative path.

    Unity only requires 32 hex characters that are unique within the project, so
    a truncated digest of the path is enough. The path is lower-cased because
    Unity treats asset paths case-insensitively on Windows and macOS, and we do
    not want two machines to disagree.
    """
    normalised = relative_path.replace("\\", "/").lower()
    return hashlib.sha256(normalised.encode("utf-8")).hexdigest()[:32]


def _trailer() -> str:
    # Unity writes a trailing space after `userData:` and friends. Matching it
    # keeps the Editor from rewriting every file the first time it opens.
    return "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def meta_body(relative_path: str, is_dir: bool) -> str | None:
    """The full text of the .meta for this asset, or None if we do not handle it."""
    guid = guid_for(relative_path)
    if is_dir:
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n" + _trailer()
        )

    suffix = Path(relative_path).suffix.lower()
    if suffix == ".cs":
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "MonoImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  defaultReferences: []\n"
            "  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n" + _trailer()
        )
    if suffix == ".shader":
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "ShaderImporter:\n"
            "  externalObjects: {}\n"
            "  defaultTextures: []\n"
            "  nonModifiableTextures: []\n"
            "  preprocessorOverride: 0\n" + _trailer()
        )
    if suffix in (".hlsl", ".cginc", ".glslinc"):
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "ShaderIncludeImporter:\n"
            "  externalObjects: {}\n" + _trailer()
        )
    if suffix == ".asmdef":
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "AssemblyDefinitionImporter:\n"
            "  externalObjects: {}\n" + _trailer()
        )
    if suffix == ".asmref":
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "AssemblyDefinitionReferenceImporter:\n"
            "  externalObjects: {}\n" + _trailer()
        )
    if suffix in (".json", ".txt", ".xml", ".md", ".csv", ".bytes"):
        return (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "TextScriptImporter:\n"
            "  externalObjects: {}\n" + _trailer()
        )
    return None


def assets_under(repo_root: Path, roots: list[str]) -> list[tuple[str, bool]]:
    """Every asset path that should carry a .meta, as (relative path, is_dir)."""
    found: list[tuple[str, bool]] = []
    for root_name in roots:
        root = repo_root / root_name
        if not root.is_dir():
            continue
        for path in sorted(root.rglob("*")):
            if any(part in SKIP_DIRS for part in path.relative_to(repo_root).parts):
                continue
            if path.is_symlink():
                continue
            relative = path.relative_to(repo_root).as_posix()
            if path.is_dir():
                found.append((relative, True))
            elif path.suffix.lower() not in NOT_ASSETS:
                found.append((relative, False))
    return found


def run(repo_root: Path, roots: list[str], check_only: bool) -> int:
    missing: list[str] = []
    stale: list[str] = []
    written = 0

    for relative, is_dir in assets_under(repo_root, roots):
        body = meta_body(relative, is_dir)
        if body is None:
            continue  # An importer we do not write; Unity will create it.
        meta_path = repo_root / (relative + ".meta")
        current = meta_path.read_text(encoding="utf-8") if meta_path.exists() else None

        # An existing .meta that already carries a well-formed GUID is left alone, whoever wrote
        # it. The point of this tool is that a GUID is *stable*, not that it came from here —
        # and rewriting one that is already committed is the opposite of stable. Unity creates
        # .meta files of its own accord for the folders and assets its generators produce
        # (`unity-configure` does exactly that), and before this check those were reported stale
        # every run and then overwritten with a different GUID.
        if current is not None and GUID_LINE.search(current):
            continue

        if current == body:
            continue
        if check_only:
            (missing if current is None else stale).append(relative + ".meta")
            continue
        meta_path.write_text(body, encoding="utf-8", newline="\n")
        written += 1

    if check_only:
        for name in missing:
            print(f"missing: {name}")
        for name in stale:
            print(f"stale:   {name}")
        if missing or stale:
            print(f"\n{len(missing)} missing, {len(stale)} stale. Run: make meta")
            return 1
        print("meta: every generated .meta is present and current")
        return 0

    print(f"meta: wrote {written} file(s)")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="report problems and exit 1; write nothing")
    parser.add_argument("roots", nargs="*", default=None, help=f"asset roots (default: {' '.join(ASSET_ROOTS)})")
    args = parser.parse_args(argv)
    repo_root = Path(__file__).resolve().parent.parent
    return run(repo_root, args.roots or list(ASSET_ROOTS), args.check)


if __name__ == "__main__":
    sys.exit(main())
