#!/usr/bin/env python3
"""Enforce the assembly layering in 02-ARCHITECTURE.md (ADR-07).

The one rule that carries real weight is that `Island.Core` references nothing from Unity. That
is what lets the logic be tested with `dotnet test` in about a second instead of waiting twenty
minutes for a Unity run, and it is a rule that decays silently: one `using UnityEngine;` added for
a `Vector3` and the fast tests stop building, usually noticed only in CI.

Checked here rather than left to the compiler because the Unity build would still succeed — it is
the plain .NET build, and so the fast feedback loop, that would break.

Since F-06 it also enforces the rest of ADR-07's table: every assembly definition in the project is
one the table names, each references only what its row allows, the test assemblies reference the
assembly under test, and `Island.Runtime` uses no XR, Input System or editor types — the rule that
lets the whole game run in PlayMode tests with no XR loader.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

#: assembly directory -> namespaces its sources may not touch
FORBIDDEN = {
    "Packages/com.island.core": ("UnityEngine", "UnityEditor", "Unity.Collections",
                                 "Unity.Burst", "Unity.Mathematics"),
}

USING = re.compile(r"^\s*using\s+(?:static\s+)?([A-Za-z0-9_.]+)", re.M)
QUALIFIED = re.compile(r"(?<![A-Za-z0-9_.])(UnityEngine|UnityEditor)\s*\.")


#: The test runner's own assemblies, which any test assembly may reference.
TEST_RUNNER = ("UnityEngine.TestRunner", "UnityEditor.TestRunner")

#: ADR-07, as data. `allow` entries ending in `.*` match a prefix; `*` alone allows anything.
#: `tests` names the assembly under test, which a test assembly must reference.
ASSEMBLIES: dict[str, dict] = {
    "Island.Core": {
        "path": "Packages/com.island.core/Runtime/Island.Core.asmdef",
        "allow": [],
    },
    "Island.Runtime": {
        "path": "Assets/Island/Runtime/Island.Runtime.asmdef",
        "allow": ["Island.Core", "Unity.RenderPipelines.*", "Unity.Burst", "Unity.Collections",
                  "Unity.Mathematics"],
        "sources_may_not_use": ("UnityEditor", "UnityEngine.XR", "Unity.XR", "UnityEngine.InputSystem"),
    },
    "Island.XR": {
        "path": "Assets/Island/XR/Island.XR.asmdef",
        "allow": ["Island.Runtime", "Island.Core", "Unity.XR.*", "Unity.InputSystem", "Unity.Mathematics"],
        "sources_may_not_use": ("UnityEditor",),
    },
    "Island.Editor": {
        "path": "Assets/Island/Editor/Island.Editor.asmdef",
        "allow": ["*"],
        "editor_only": True,
    },
    "Island.Core.Tests": {
        "path": "Packages/com.island.core/Tests/Island.Core.Tests.asmdef",
        "tests": "Island.Core",
    },
    "Island.Runtime.Tests": {
        "path": "Assets/Island/Tests/Runtime/Island.Runtime.Tests.asmdef",
        "tests": "Island.Runtime",
    },
    "Island.XR.Tests": {
        "path": "Assets/Island/Tests/XR/Island.XR.Tests.asmdef",
        "tests": "Island.XR",
    },
    "Island.Editor.Tests": {
        "path": "Assets/Island/Tests/Editor/Island.Editor.Tests.asmdef",
        "tests": "Island.Editor",
    },
}

#: Where the project's own assembly definitions may live. Anything found here must be in the table.
ASMDEF_ROOTS = ("Assets", "Packages/com.island.core")


def _allowed(reference: str, allow: list[str]) -> bool:
    for pattern in allow:
        if pattern == "*" or pattern == reference:
            return True
        if pattern.endswith(".*") and reference.startswith(pattern[:-1]):
            return True
    return False


def _allow_list(name: str) -> list[str]:
    """What an assembly may reference: its own row, or for tests, its subject and the subject's row."""
    rule = ASSEMBLIES[name]
    if "tests" in rule:
        subject = rule["tests"]
        return [subject, *ASSEMBLIES[subject]["allow"], *TEST_RUNNER]
    return list(rule["allow"])


def check_assemblies(repo: Path) -> tuple[list[str], int]:
    """ADR-07's reference rules over every assembly definition. Returns (problems, asmdefs read)."""
    problems: list[str] = []
    by_path = {rule["path"]: name for name, rule in ASSEMBLIES.items()}
    read = 0

    found: set[str] = set()
    for root in ASMDEF_ROOTS:
        base = repo / root
        if not base.is_dir():
            continue
        for asmdef in sorted(base.rglob("*.asmdef")):
            relative = asmdef.relative_to(repo).as_posix()
            if any(part in ("Library", "Temp", "PackageCache") for part in asmdef.parts):
                continue
            found.add(relative)
            if relative not in by_path:
                problems.append(f"{relative}: not an assembly ADR-07 names — add its row to "
                                "ASSEMBLIES in tools/lint/check_layering.py and to the ADR")

    for name, rule in ASSEMBLIES.items():
        relative = rule["path"]
        if relative not in found:
            problems.append(f"{relative}: missing — ADR-07 requires {name}")
            continue
        read += 1
        data = json.loads((repo / relative).read_text(encoding="utf-8"))
        if data.get("name") != name:
            problems.append(f"{relative}: is named {data.get('name')!r}, expected {name!r}")

        references = data.get("references", [])
        allow = _allow_list(name)
        for reference in references:
            if reference.startswith("GUID:"):
                problems.append(f"{relative}: references {reference} by GUID; use the assembly name so this check can read it")
            elif reference == name:
                problems.append(f"{relative}: references itself")
            elif not _allowed(reference, allow):
                problems.append(f"{relative}: references {reference}, which ADR-07 does not allow {name}")

        if "tests" in rule and rule["tests"] not in references:
            problems.append(f"{relative}: does not reference {rule['tests']}, the assembly it tests")

        platforms = data.get("includePlatforms", [])
        if rule.get("editor_only") and platforms != ["Editor"]:
            problems.append(f"{relative}: must be editor-only (includePlatforms [\"Editor\"])")
        if name in ("Island.Runtime", "Island.XR") and "Editor" in platforms:
            problems.append(f"{relative}: must build for the device, not only the editor")

    for name, rule in ASSEMBLIES.items():
        forbidden = rule.get("sources_may_not_use")
        if not forbidden:
            continue
        folder = (repo / rule["path"]).parent
        if not folder.is_dir():
            continue
        for source in sorted(folder.rglob("*.cs")):
            relative = source.relative_to(repo).as_posix()
            text = source.read_text(encoding="utf-8")
            for match in USING.finditer(text):
                namespace = match.group(1)
                if any(namespace == f or namespace.startswith(f + ".") for f in forbidden):
                    line = text[: match.start()].count("\n") + 1
                    problems.append(f"{relative}:{line}: `using {namespace}` — {name} may not use it (ADR-07)")
            for match in QUALIFIED_ANY.finditer(text):
                qualified = match.group(1)
                if any(qualified == f or qualified.startswith(f + ".") for f in forbidden):
                    line = text[: match.start()].count("\n") + 1
                    problems.append(f"{relative}:{line}: refers to {qualified} — {name} may not use it (ADR-07)")

    return problems, read


QUALIFIED_ANY = re.compile(r"(?<![A-Za-z0-9_.])((?:UnityEngine|UnityEditor|Unity)(?:\.[A-Z][A-Za-z0-9_]*)+)")


def main() -> int:
    problems: list[str] = []
    checked = 0

    for directory, forbidden in FORBIDDEN.items():
        root = REPO / directory
        if not root.is_dir():
            continue

        for source in sorted(root.rglob("*.cs")):
            relative = source.relative_to(REPO).as_posix()
            text = source.read_text(encoding="utf-8")
            checked += 1
            for match in USING.finditer(text):
                namespace = match.group(1)
                if any(namespace == f or namespace.startswith(f + ".") for f in forbidden):
                    line = text[: match.start()].count("\n") + 1
                    problems.append(f"{relative}:{line}: `using {namespace}` — {directory} must stay engine-free")
            for match in QUALIFIED.finditer(text):
                line = text[: match.start()].count("\n") + 1
                problems.append(f"{relative}:{line}: refers to {match.group(1)} — {directory} must stay engine-free")

        # The assembly definition has to say so too, or Unity links the engine in anyway.
        for asmdef in sorted(root.rglob("*.asmdef")):
            relative = asmdef.relative_to(REPO).as_posix()
            data = json.loads(asmdef.read_text(encoding="utf-8"))
            if "Tests" in asmdef.parts[-1]:
                continue  # Unity's test runner needs the engine; the dotnet build does not use it.
            if data.get("noEngineReferences") is not True:
                problems.append(f"{relative}: needs \"noEngineReferences\": true")
            if data.get("references"):
                problems.append(f"{relative}: references {data['references']}; it must reference nothing")

    assembly_problems, asmdefs = check_assemblies(REPO)
    problems.extend(assembly_problems)

    if problems:
        for problem in problems:
            print(f"layering: {problem}")
        print(f"\nlayering: {len(problems)} problem(s)")
        return 1

    print(f"layering: {checked} source files checked, Island.Core is engine-free; "
          f"{asmdefs} assemblies follow ADR-07")
    return 0


if __name__ == "__main__":
    sys.exit(main())
