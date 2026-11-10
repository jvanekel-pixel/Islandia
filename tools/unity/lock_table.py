#!/usr/bin/env python3
"""Prints the resolved package versions as a markdown table.

Lives here rather than inline in the workflow: a Python heredoc nested inside a YAML block
scalar inside a shell script is three levels of quoting, and it broke the workflow file the
first time it was written.
"""

import json
import sys
from pathlib import Path

lock = Path(sys.argv[1] if len(sys.argv) > 1 else "Packages/packages-lock.json")
if not lock.exists():
    print("_No lock file: the packages step did not get that far._")
    raise SystemExit(0)

dependencies = json.loads(lock.read_text(encoding="utf-8")).get("dependencies", {})
chosen = {name: info for name, info in dependencies.items() if info.get("source") != "builtin"}

if not chosen:
    print("_Nothing but built-in modules._")
    raise SystemExit(0)

print("| Package | Version |")
print("| :-- | :-- |")
for name, info in sorted(chosen.items()):
    print(f"| `{name}` | {info.get('version', '?')} |")
