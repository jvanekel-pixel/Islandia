#!/usr/bin/env python3
"""Enforce the assembly layering in 02-ARCHITECTURE.md (ADR-07).

The one rule that carries real weight is that `Island.Core` references nothing from Unity. That
is what lets the logic be tested with `dotnet test` in about a second instead of waiting twenty
minutes for a Unity run, and it is a rule that decays silently: one `using UnityEngine;` added for
a `Vector3` and the fast tests stop building, usually noticed only in CI.

Checked here rather than left to the compiler because the Unity build would still succeed — it is
the plain .NET build, and so the fast feedback loop, that would break.
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

    if problems:
        for problem in problems:
            print(f"layering: {problem}")
        print(f"\nlayering: {len(problems)} problem(s)")
        return 1

    print(f"layering: {checked} source files checked, Island.Core is engine-free")
    return 0


if __name__ == "__main__":
    sys.exit(main())
