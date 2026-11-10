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

## 2026-11-10 — Island.Core parses JSON with Newtonsoft.Json

**Decided by:** work package `SB-01b`.

`Island.Core` must stay engine-free (ADR-07), so it cannot use Unity's `JsonUtility` — and that
type could not express this contract anyway: it has no notion of an absent field versus a null
one, which the StreamBox contract leans on throughout.

Three options were weighed:

| Option | Why not |
| :-- | :-- |
| Hand-written JSON reader | Keeps Core dependency-free, which is attractive for a package compiled by two toolchains. But a subtly wrong parser is worse than a dependency, and this is a well-known time sink |
| `System.Text.Json` | Not shipped with Unity, and its netstandard2.1 story there is poor |
| **`Newtonsoft.Json`** | **Chosen.** Unity ships it as the official `com.unity.nuget.newtonsoft-json` package, and NuGet has the same library, so one dependency covers both toolchains with identical behaviour |

Newtonsoft is not a Unity reference, so the engine-free rule and `check_layering` are unaffected.

Two constraints on how it is used:

1. **No attribute-driven deserialisation.** Responses are parsed to a token tree and mapped by
   explicit code, so a missing required field is a named error rather than a silent default, and
   every mapping is unit-testable without a server.
2. **Tolerance is the default.** Unknown fields are ignored and unknown enum values fall back to a
   documented case. StreamBox is under active development and will add both; an older Islandia
   build must keep working rather than throw.

`F-03` adds `com.unity.nuget.newtonsoft-json` to the Unity manifest when it creates the project.

## 2026-11-10 — Unity is activated by seat request, not by a licence file

**Decided by:** work package `F-02`, from evidence rather than documentation.

The widely-published way to use Unity Personal in CI — activate in Unity Hub, paste the resulting
`.ulf` into a `UNITY_LICENSE` secret, drop the file into the container — does not work for Unity 6
on a cloud runner. Both halves of why were proven in CI:

| Route | What happened |
| :-- | :-- |
| `.ulf` from Unity Hub, placed on disk | `[Licensing::Client] Error: Code 400 (status: Machine bindings don't match)`, then `Found 0 entitlement groups`, editor exit 198. The file is bound to the Windows machine that activated it |
| `.ulf` via the `.alf` exchange | Closed. Unity no longer supports manual activation of Personal licences, so no container-bound file can be produced |
| Editor flag `-manualLicenseFile` | Loads the file, then the same machine-binding rejection |
| **`Unity.Licensing.Client --activate-all --include-personal --username --password`** | **`Status: [200] ASSIGN_SEAT`, editor exit 0, version 6000.3.28f1, 104 s** |

So: **no licence file is involved.** `UNITY_EMAIL` and `UNITY_PASSWORD` are the only secrets, and
`UNITY_LICENSE` is neither needed nor read.

Two consequences worth remembering:

- **The seat must be returned.** Each activation takes one of the account's few Personal seats and
  the runner is destroyed minutes later. Every Unity workflow therefore ends with
  `--deactivate-all` in a step that runs even on failure or cancellation. Leaked seats would
  eventually fail every run with an error that looks nothing like its cause.
- **Activations are serialised.** The workflow takes a concurrency group so two runs cannot take
  two seats at once, and `cancel-in-progress` is off so a cancelled run still gets to give its
  seat back.

**Evidence:** runs on `claude/wp-F-02b-licence-activation`, 2026-11-10.

## 2026-11-10 — Finished work merges itself

**Decided by:** the owner, explicitly, after merging five pull requests by hand in one day.

A build driven by scheduled sessions stops every few hours waiting for a button to be pressed. A
session cannot press it: the permission classifier refuses a self-merge, and GitHub's own
auto-merge is a GraphQL call, which cloud sessions cannot make. So the merging moves into the
repository itself, as `.github/workflows/auto-merge.yml`.

**What this gives up, stated plainly:** a human no longer sees each change before it reaches
`main`. That is a real safety net and it is now gone. The owner was told so before choosing.

What still has to hold:

- every check on the head commit has passed, and none is still running;
- the pull request carries `awaiting-merge`, which only a finished package gets;
- **the branch is in this repository, never a fork** — this one matters most. The repository is
  public, so anyone may open a pull request; without it, a stranger could land code on `main` by
  adding a label. A fork's workflow gets a read-only token and so could not merge anyway, but a
  control that holds only because of a token scope is a coincidence, not a control;
- the author is the repository owner, it is not a draft, and it carries none of
  `blocked`, `needs-human`, `do-not-merge`, `wip`.

The escape hatch is a label: adding `do-not-merge` to a pull request parks it for a human.

The workflow that introduced this was **not** auto-merged. The change that removes review should
not be the first thing to skip it.

## 2026-11-10 — This is a gift, not a product

**Decided by:** the owner, unprompted, after a progress estimate.

Islandia is being made for the owner's partner, who will be the one wearing the headset. It is
never going to a store and is never for sale. Three things follow, and they are recorded here
because they change judgement calls rather than just paperwork.

**The release track is cut** (ADR-20). No store compliance validator, no listing media, no
submission, no public privacy policy. Seven packages removed; `M12` is gone and `M11` becomes a
signed APK, a page of hand-over notes, and installing it. `HG-5` no longer exists, so `HG-4` is
the last gate. Meta's December 2027 cut-off for new Quest 2 submissions stops mattering.

**Attribution-licensed assets become usable** (ADR-11). The CC0-only rule existed because
commercial distribution demands it; for a personal gift, attribution is fine, which roughly
triples the available art. The licence ledger stays and attribution is honoured in the notices
screen — not treated as optional because nobody would check. Anything forbidding redistribution in
a built application is still refused, and a non-commercial licence is recorded as such, so the
position stays honest if the purpose ever changes.

**Content settles at about 120 distinct models**, final rather than a waypoint, with coherence
explicitly worth more than count.

**Onboarding went up in risk, not down.** The person playing this is not a developer, will not
read instructions, and nobody will be standing next to them explaining it. The first five minutes
now carry the whole thing, so `Q-04` is a high-risk package and its criteria ask for the opening
to be designed rather than inherited.

What does *not* relax: the security review of the import path and the relay. The app talks to a
private media server holding a household's library, and that is true whether or not anything is
published.

**No deadline.** Prioritise on quality and risk, not on a date.

## 2026-11-10 — ADR-10's budgets leave no headroom, and the ledger now says so

**Found while building `V-07a`.** Adding up ADR-10's per-subsystem allowances for a Quest 2 gives
140 draws against a 200 hard cap (70%), 711,072 triangles against 750,000 (95%), and 368 MB
against a memory figure which *is* 368 MB (100%). For Quest 3 the same sums come to 97% and 99%.
So the comfortable-sounding phrase "busy-scene total" describes a scene that is already at the
limit on two of the three measures, and the planning numbers contain no slack to absorb a busy
moment. None of them has been near the hardware.

Two things changed as a result.

**The ledger now measures all three against stated caps.** It had compared draw calls and
triangles against the hard caps but memory against the sum of the allowances, so one reported
number meant two different things depending on which measure was tightest. `DeviceBudget` carries
an explicit `HardBytes`, which for a Quest 2 is ADR-10's 368 MB — the figure from the document, not
an invented one.

**The numbers are pinned by a test that explains itself**
(`TheQuest2AllowancesLeaveNoMeaningfulHeadroom`). It asserts the 70% / 95% / 100% shape, so that
anyone adjusting the tables without a device reading in hand breaks a test that tells them what
they are doing. An island with every subsystem at its allowance reads **red**, which is honest:
a build in that state has nothing left.

Nothing was weakened to get green. The failing test that started this was correct about the
inconsistency and wrong in the reason it gave for it, and both were fixed.

**Consequence for `HG-1`.** The first headset check should measure memory first. It is the measure
with zero slack and the one most likely to bind, and `RK-08` already names budget calibration as
the project's largest technical risk. The numbers in ADR-10 stay as they are until device readings
replace them — guessing a different guess would not be an improvement.

## Open questions for the owner

- Does the "Remediation Plan" cited by the blueprint still exist? Five of its items are referenced
  but never described. If so, add it to `docs/blueprint/`.
- Is this intended as a store release, or only for your own headset? (Decides how much of M11 is needed.)
- The three questions in `07-STREAMBOX.md` §9.
