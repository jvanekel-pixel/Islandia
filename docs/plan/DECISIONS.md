# DECISIONS

Append-only. One entry per decision or plan change: date, who decided, what, why, evidence.
Architecture decisions themselves live in `02-ARCHITECTURE.md`; this log records when and why
they were made or changed.

---

## 2026-11-10 — Planning baseline

**Decided by:** planning session, from the audit in `01-AUDIT.md`. Owner to confirm at HG-0.

- Adopted `02-ARCHITECTURE.md` ADR-01 … ADR-22 as the baseline, superseding the blueprint.
- Initial defaults were proposed for target devices, repository, name, cuts, content targets and
  relay hosting. Several were replaced the same day by the owner; see the next entry. Still
  standing as defaults: content targets (Alpha ≥ 120 distinct meshes; Beta ≥ 250 distinct meshes
  and ≥ 400 entries) and relay hosting deferred to a human gate.
- Lifecycle: risk-first stage-gate with continuous flow (`03-ROADMAP.md` §1).

**Evidence:** `docs/plan/audit/*.md`.

## 2026-11-10 — Owner decisions

**Decided by:** the owner, in the planning conversation.

1. **Quest 2 is the target.** The owner has a Quest 2 and does not plan to upgrade. ADR-02 and
   ADR-10 rewritten for it: 72 Hz, ≤ 140 draws and ≤ 711K triangles in a busy scene, idle terrain
   merged to 4 × 4 regions, 4 active pets, 1 video screen. Other tiers are data only, unverified.
2. **No Spotify, no in-world web browser.** Both removed outright (the web-browser package is
   deleted from the backlog; the internet radio tuner is kept only as an unrequested option).
3. **StreamBox must work natively in-world.** New ADR-23, new `07-STREAMBOX.md`, new track `SB`.
   Media moved up from the seventh milestone to the fourth, and a media probe added to M2 so the
   first headset check decides the video backend. New non-blocking gate HG-2 for it.
4. **No additional spend.** Public repository with GitHub-hosted runners (ADR-19). The owner would
   also accept a runner on their PC; not chosen, because that PC is the media server and a public
   repository must never have a self-hosted runner.
5. **Name:** the owner calls the app *Islandia*. Adopted (ADR-22).

**Evidence:** the owner's message; a read of the StreamBox repository at commit `9ebfdd6`
(device pairing, stream broker, Connect, music routes), summarised in `07-STREAMBOX.md` §3.

**Consequences logged:** milestones renumbered (media is M4; garden, builder and import are M5–M7);
human gates renumbered (alpha, beta and release are HG-3 to HG-5).

## 2026-11-10 — Unity editor pinned to 6000.3.28f1

**Decided by:** work package `F-02`.

ADR-01 says to pin a 6000.3 LTS patch for which a GameCI Android image exists. Of the 6000.3 tags
published for `unityci/editor`, exactly two carry the Android module: `ubuntu-6000.3.27f2-android-3`
and `ubuntu-6000.3.28f1-android-3`. 28f1 is the newer, so:

- **Editor:** `6000.3.28f1`
- **Licence check image:** `unityci/editor:ubuntu-6000.3.28f1-base-3` (no Android module needed)
- **Build and test image:** `unityci/editor:ubuntu-6000.3.28f1-android-3`

`F-03` writes this into `ProjectSettings/ProjectVersion.txt` and every Unity workflow uses the same
version. Changing it later is a decision record plus a full device gate, never incidental.

**Evidence:** the published tag list for `unityci/editor`, queried 2026-11-10.

## Open questions for the owner

- Does the "Remediation Plan" cited by the blueprint still exist? Five of its items are referenced
  but never described. If so, add it to `docs/blueprint/`.
- Is this intended as a store release, or only for your own headset? (Decides how much of M11 is needed.)
- The three questions in `07-STREAMBOX.md` §9.
