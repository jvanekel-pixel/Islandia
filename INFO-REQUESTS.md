# INFO-REQUESTS — what only the owner can supply

Missing accounts, credentials and human decisions. Secret values never appear here or anywhere in
this public repository — names only. Formal blockers are also raised as `human-gate` issues; the
owner's full list of gates is in `00-START-HERE.md`.

## Open now (none blocks the next work package)

- [ ] Review this onboarding branch and merge it — until then the scheduled driver still sees the old `CLAUDE.md` and none of the Command Center files
- [ ] Point the scheduled driver routine at the `islandia` cloud environment instead of Default (closes `F-10`)
- [ ] Check the scheduled driver is running: `main` has had no commit since 2026-11-12 although it is scheduled daily
- [ ] Decide pull request 42 (`RC-01b`, open and unlabelled since 2026-11-12): label it `work-package` and `awaiting-merge`, or close it
- [ ] Decide the code-writer model: `project.json` routing says Sonnet; the driver was set to Opus on 2026-11-11
- [ ] Create `.env.example` by hand, or narrow the `.env*` rule in `.claude/settings.json`: that rule stops a session writing or reading the file. The six secret names it would list are under "Already supplied" below
- [ ] Generate `.mcp.json` from Command Center's Setup tab on the local machine (git-ignored; cloud sessions cannot reach the server)
- [ ] Headset developer setup: Meta developer organisation, Developer Mode, `adb` on the PC (needed for HG-1)
- [ ] Optional: Unity credentials in the cloud environment, if sessions should run Unity directly (`S-01`)
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
