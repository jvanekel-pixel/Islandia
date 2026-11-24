# LOCAL-SETUP — the owner's PC as the workstation

Since 2026-11-24 the driver runs in Claude Code on the owner's PC (`DECISIONS.md`, "The driver
moves to the owner's PC"). This is what such a session needs. `F-10` fills in the *Found* column
from the PC itself; a session never installs a tool that changes the PC outside the clone without
the owner's yes.

Names only. No server address, account name, token or path that identifies the owner goes here —
this repository is public.

| Tool | Needed for | Required | Found on the PC |
| :-- | :-- | :-- | :-- |
| git | everything | yes | — |
| GNU make, a POSIX shell | every `make` target (on Windows: Git Bash or WSL) | yes | — |
| Python 3.11+ | lint, tooling tests (`make deps`) | yes | — |
| Node 22 | mock gateway, companion tests (`make mock-install`) | yes | — |
| .NET 9 SDK | `Island.Core` build and tests | yes | — |
| GitHub CLI `gh`, signed in | pull requests, issues, CI status | yes | — |
| ffmpeg | the StreamBox end-to-end test serves a decodable stream | yes | — |
| Unity Hub + editor `6000.3.28f1` with the Android module | local batchmode runs (`S-01`) | no — CI runs Unity regardless | — |
| Android platform tools (`adb`) | installing a CI-built APK on the headset over USB | no | — |
| Blender 4.5.16 LTS | `make blender` installs the pinned one itself | no | — |
| Ollama | read-only log checks and briefs (`AGENTS.md`) | no | — |
| Command Center `.mcp.json` | `get_project_context` / `log_session` | no | — |

## Rules that do not change on the PC

- Headless only: Unity runs with `-batchmode -executeMethod`; nothing is set up by clicking.
- A package is done when CI is green on the merge commit, whatever ran locally.
- The owner signs in to Unity Hub, `gh` and anything else themselves. A session never asks for,
  reads or prints a password, token or licence file.
- The headset: a session may `adb install` an APK CI built, but what happens in the headset is
  reported by the owner. Nothing is marked passed on a device from a simulation.
