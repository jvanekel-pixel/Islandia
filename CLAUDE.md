# Islandia — operating contract for Claude Code sessions

You are building **Islandia**, a Meta Quest 2 (standalone Android VR) island sandbox in Unity, plus
a small web companion, with the owner's StreamBox media service playing natively in the world. **No human is watching this session and nobody will ever open the Unity Editor
GUI.** The owner checks in only at the human gates listed in `docs/plan/00-START-HERE.md`.

## Read first, every session

1. `docs/plan/STATE.md` — where the project is right now, what is in flight, what is blocked.
2. `docs/plan/05-OPERATIONS.md` — the loop you run (pick → build → verify → merge → record).
3. The backlog entry for the work package you pick, in `docs/plan/BACKLOG.yaml`, and only the
   blueprint/draft files it references. Do not load the whole blueprint; it is 12k lines of code.

## Order of authority

When two sources disagree, the higher one wins. If you must contradict a higher one, change it in
the same PR and log why in `docs/plan/DECISIONS.md`.

1. `docs/plan/02-ARCHITECTURE.md` — locked decisions, budgets, constants.
2. `docs/plan/BACKLOG.yaml` — scope and acceptance criteria per work package.
3. `docs/plan/04-VERIFICATION.md` — what "done" means and how it is proven.
4. `docs/blueprint/` — the original design document. **Reference only.** It has known errors
   (`docs/plan/01-AUDIT.md`).
5. `reference/drafts/` — code extracted from the blueprint. **Never compiled, never run.** Treat it
   as a detailed sketch by a colleague who could not test anything: mine it for structure and
   intent, port it file by file behind tests, and expect invented APIs.

## Hard rules

- **Headless only.** Every project, scene, asset, setting and build step is produced by code run
  with `-batchmode -executeMethod` or by a script. If a step needs a click, it is not done.
  Never write instructions that tell someone to use a menu or the Inspector.
- **Never hand-write Unity YAML** (`.unity`, `.prefab`, `.asset`, `ProjectSettings/*.asset`).
  Generate it with editor code in CI. Hand-written `.meta` files for scripts and folders are fine
  and expected (`tools/gen_meta.py`), so GUIDs stay stable.
- **Verify, then claim.** A work package is done when its acceptance tests pass in CI on the merge
  commit — not when the code is written. Report failures as failures.
- **Never fake a device result.** Anything that needs a headset is recorded as `needs-device` and
  goes into the next human gate. Do not mark an on-device gate passed from a simulation.
- **Budgets are law.** Draw-call, triangle, memory and per-frame-allocation budgets live in
  `02-ARCHITECTURE.md` and are enforced by tests. A change that breaks a budget is a failing change.
- **Do not weaken a gate to get green.** If a test or threshold is wrong, fix it in its own commit
  and record the reason in `DECISIONS.md`. Deleting or skipping a failing test is never the fix.
- **One work package per pull request.** Branch `claude/wp-<ID>-<slug>`. Small, reviewable,
  squash-merged. Use the session's GitHub tools or `gh api`; `gh pr` and `gh issue` do not work here.
- **Secrets** live in GitHub Actions secrets or the cloud environment only. Never print, commit or
  echo them. Never put keys in environment variables through tooling; the owner enters them.
- **Nothing may cost money.** No paid asset, service or dependency. Account-bound things (hosting,
  store) are gated behind feature flags and the project builds and passes CI without them.
- **The Quest 2 is the target.** Budgets in `02-ARCHITECTURE.md` ADR-10 are Quest 2 budgets.
  Nothing may depend on a newer headset.
- **This repository is public.** Never commit a server address, account name, token, keystore, or
  anything from the owner's media library. Never attach a self-hosted runner.
- **StreamBox is a separate project** (`docs/plan/07-STREAMBOX.md`). Never push to it, open pull
  requests on it, or copy from it beyond the client contract. Changes it would need go to the
  owner as a `human-gate` issue.
- **Stop the line** after three failed attempts at the same work package: write the diagnosis to an
  issue labelled `blocked`, set the package to `blocked` in the backlog, and take the next
  unblocked package. If nothing is unblocked, open a `human-gate` issue and stop.
- **Leave the trail.** Before ending a session: push, update `STATE.md`, and make sure the next
  session could continue from the repository alone.

## Conventions

- C# namespaces `Island.*`. Engine-free logic in `Packages/com.island.core` (no `UnityEngine`
  reference, tested with `dotnet test`); Unity-facing code in `Assets/Island/`.
- Runtime is composed in code from a single boot scene. No scene-serialised references to wire.
- Every feature ships with: save/load support, undo support where it edits the world, a budget
  ledger entry, and tests at the lowest verification level that can prove it.
- Commit messages: `<WP-ID>: imperative summary`. PR body lists acceptance criteria with evidence.

## Commands

The canonical commands are defined by `Makefile` once milestone M1 lands (`make help`). Until then
see `docs/plan/BACKLOG.yaml` → work packages `F-*`.
