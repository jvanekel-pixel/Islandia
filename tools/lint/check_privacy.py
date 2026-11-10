#!/usr/bin/env python3
"""Refuse to let anything private into a public repository.

This repository is public and it describes a client for the owner's private
media service. The address of that service, any token, and anything from the
library itself must stay out. So must the usual credential files.

The patterns are deliberately blunt. A false positive costs a minute; a real
leak costs a rotation and a history rewrite. If a match is legitimate, make the
text not match rather than adding an exception here.
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

#: (name, pattern, why). Searched against every tracked text file.
PATTERNS: list[tuple[str, str, str]] = [
    ("private IPv4", r"\b(?:10\.\d{1,3}|192\.168|172\.(?:1[6-9]|2\d|3[01]))\.\d{1,3}\.\d{1,3}\b",
     "a machine address on the owner's network"),
    ("private key", r"-----BEGIN (?:RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----", "a private key"),
    ("AWS key id", r"\bAKIA[0-9A-Z]{16}\b", "an AWS access key"),
    ("GitHub token", r"\bgh[pousr]_[A-Za-z0-9]{36,}\b", "a GitHub token"),
    ("Anthropic key", r"\bsk-ant-[A-Za-z0-9_-]{20,}\b", "an Anthropic API key"),
    ("Slack token", r"\bxox[abprs]-[A-Za-z0-9-]{10,}\b", "a Slack token"),
    ("bearer literal", r"(?i)\bauthorization\s*[:=]\s*[\"']?bearer\s+[A-Za-z0-9._-]{16,}", "a real bearer token"),
    ("assigned secret", r"(?i)\b(api[_-]?key|client[_-]?secret|password|passwd|token)\s*[:=]\s*[\"'][A-Za-z0-9._\-/+]{16,}[\"']",
     "a secret assigned in code"),
    ("Unity licence", r"<License id=|DeveloperData Value=", "the contents of a Unity licence file"),
]

#: Credential files that must never be tracked, whatever they contain.
FORBIDDEN_NAMES = (".ulf", ".keystore", ".jks", ".p12", ".pfx", ".pem", ".mobileprovision")
FORBIDDEN_EXACT = (".env", ".env.local", ".env.production", "id_rsa", "id_ed25519")

#: Checked for its own text, so it cannot match itself.
SELF = "tools/lint/check_privacy.py"

SKIP_SUFFIXES = {".png", ".jpg", ".jpeg", ".tga", ".psd", ".fbx", ".glb", ".wav",
                 ".ogg", ".mp4", ".ttf", ".otf", ".dll", ".so", ".aar", ".apk", ".zip"}


def tracked_files() -> list[str]:
    out = subprocess.run(["git", "-C", str(REPO), "ls-files"], capture_output=True, text=True, check=True)
    return [line for line in out.stdout.splitlines() if line]


def main() -> int:
    problems: list[str] = []
    compiled = [(name, re.compile(pattern), why) for name, pattern, why in PATTERNS]
    scanned = 0

    for relative in tracked_files():
        path = REPO / relative
        name = Path(relative).name
        if name in FORBIDDEN_EXACT or any(name.endswith(suffix) for suffix in FORBIDDEN_NAMES):
            problems.append(f"{relative}: a credential file is tracked; remove it and add it to .gitignore")
            continue
        if Path(relative).suffix.lower() in SKIP_SUFFIXES or relative == SELF:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, FileNotFoundError):
            continue
        scanned += 1
        for label, pattern, why in compiled:
            match = pattern.search(text)
            if match:
                line = text[: match.start()].count("\n") + 1
                problems.append(f"{relative}:{line}: looks like {why} ({label})")

    if problems:
        for problem in problems:
            print(f"privacy: {problem}")
        print(f"\nprivacy: {len(problems)} problem(s). This repository is public — do not commit this.")
        return 1

    print(f"privacy: {scanned} tracked text files scanned, nothing private found")
    return 0


if __name__ == "__main__":
    sys.exit(main())
