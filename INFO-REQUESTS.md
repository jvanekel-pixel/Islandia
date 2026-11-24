# INFO-REQUESTS — what only the owner can supply

Missing accounts, credentials and human decisions. Secret values never appear here or anywhere in
this public repository — names only. Formal blockers are also raised as `human-gate` issues; the
owner's full list of gates is in `00-START-HERE.md`.

## Open now (none blocks the next work package)

- [ ] On the PC, sign in yourself where a session cannot: `gh auth login`, and — only if you want local Unity — Unity Hub with editor `6000.3.28f1` and its Android module (`docs/plan/LOCAL-SETUP.md`, `F-10`, `S-01`)
- [ ] Generate `.mcp.json` from Command Center's Setup tab on the PC (git-ignored)
- [ ] Optional: a scheduled driver — a local scheduled task on the PC, or the cloud routine on the `islandia` environment. Sessions started by hand are fine (`F-11`)
- [ ] A rigged cat model in a later asset wave — no pack in wave 1 has one (`CAT-01`)
- [ ] Does the "Remediation Plan" the blueprint cites still exist? If so, add it to `docs/blueprint/`

## Coming up

- [ ] HG-1 first headset check, after Phase 2 — includes the video probe that decides how media is played
- [ ] HG-R ride comfort check, after Phase 5
- [ ] HG-2 StreamBox in-world check, after Phase 6 — needs the dedicated StreamBox profile
- [ ] A free-tier hosting account for the relay, preferably one needing no payment card (`I-07`, Phase 9)
- [ ] HG-3 full device pass, after Phase 12 — blocks the device-driven work in Phase 13
- [ ] HG-4 art, comfort and soak review, after Phase 13 — blocks Phase 14

## Already supplied (per `docs/plan/STATE.md`; a session cannot read secrets to confirm)

- [x] GitHub Actions secrets `UNITY_EMAIL` and `UNITY_PASSWORD` — Unity licence activation works in CI
- [x] GitHub Actions secrets `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` — the signing key
- [x] Asset wave 1 uploaded to draft release `assets-wave-1` (23 archives, all CC0)
- [x] Cloud environment `islandia` created and approved
- [x] Headset developer setup — the headset is ready for test builds over USB (2026-11-12)
- [x] Decision: Unity credentials stay out of the cloud environment; Unity runs in CI only (2026-11-12) — reopened for the PC on 2026-11-24
- [x] Decision: work moves from cloud sessions to Claude Code on the owner's PC (2026-11-24)
- [x] Decision: the Command Center onboarding is merged (pull request 46, 2026-11-15)
- [x] Decision: pull request 42 (`RC-01b`) is brought up to date and landed (2026-11-15)
- [x] Decision: Opus plans and writes the code; `project.json` routing now says so (2026-11-15)
- [x] `.env.example` added through the GitHub API with the owner's say-so, names and empty placeholders only (2026-11-15)
