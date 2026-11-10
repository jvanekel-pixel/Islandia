# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 0 — Orientation |
| **Current milestone** | M0 Keys handed over → next M1 Factory online |
| **In flight** | — |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | Scheduled: *Islandia build driver*, every 2 hours, new session per run, Opus |
| **Last updated** | 2026-11-10 (planning bundle, revised for the owner's decisions) |
| **Target** | Quest 2 · public repository · StreamBox native in-world |

## Open human gates

| Gate | What is needed | Issue |
| :-- | :-- | :-- |
| HG-0 | Done 2026-11-10, except the Unity licence secrets (owner was adding them) | — |

If `F-02` fails because `UNITY_LICENSE`, `UNITY_EMAIL` or `UNITY_PASSWORD` is missing, that is the
tail of HG-0, not a new problem: open a `human-gate` issue pointing at `00-START-HERE.md` §2 step 4
and carry on with packages that do not need Unity (`F-01`, `F-08`, `SB-01`, `I-01`, `I-04`).

## Blocked

*(none)*

## Waiting on a device (`needs-device`) or a person (`needs-human`)

Collected here as packages merge; they become the checklist for the next human gate.

*(none yet)*

## Next up

`F-01` → `F-02` → `F-08` (F-08 does not need Unity and can proceed if F-02 is waiting on secrets).

**Note on `F-10` (cloud environment):** the kickoff session did not propose one. Its own container
had a second, unrelated repository attached, and an environment proposed from there would have
covered that project too. Propose it from a session that has only Islandia. The Default environment
has unrestricted network access and no setup script, so nothing is blocked meanwhile — the only
cost is reinstalling the .NET SDK and the companion toolchain each session.

## Recently merged

- `c30cdda` Planning bundle (2026-11-10, kickoff session)

## Measured facts

Filled in as they are measured; planning numbers elsewhere are hypotheses until they appear here.

| Fact | Value | Source |
| :-- | :-- | :-- |
| Unity editor version pinned | — | F-03 |
| CI: warm test run / Android build | — | S-05 |
| Runner minutes per Unity package | — | S-05 |
| Device strings and tier | — | HG-1 |
| Frame time, empty island | — | HG-1 |
| Video backend that works on the headset | — | HG-1 (V-14) |
| StreamBox commit the contract snapshot matches | 9ebfdd6 (read during planning) | SB-01 |
