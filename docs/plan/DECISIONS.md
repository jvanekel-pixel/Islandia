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

## 2026-11-10 — Nothing was running in CI, and F-07 is split so that it does

**Found while starting the content track.** `main` carried three workflows — activate a Unity seat,
create the Unity project, merge labelled pull requests — and not one of them ran a linter or a test.
Every package merged so far was proven by a session running `make` in its own container and pasting
the output into a pull request. That is not what `CLAUDE.md` means by *"a work package is done when
its acceptance tests pass in CI on the merge commit"*, and it means the evidence in those pull
request bodies rests on my word rather than on a run anyone can click.

Worse, `F-13`'s auto-merge waits for all checks to complete and pass. With no checks configured that
is a wait for nothing: on landing, the first thing it would have done is merge whatever carried the
label, unverified. `F-07a` lands the checks before that can happen.

**`F-07` is split.** Its L0 and L1 half needs no Unity project and exists now (`F-07a`: the linters,
the tooling tests, `Island.Core`, the netstandard2.1 compile, the mock gateway's contract tests, on
every push, concurrency-cancelled). `F-07` keeps EditMode, PlayMode, the Android build, the Library
cache and the package-source artefact, all of which need the project that is still in PR #10.
Waiting for that had meant nothing was checked at all.

**It failed on its first run, which is the whole argument for it.** Two of the mock gateway's
contract tests pass in a session container and fail on a GitHub runner: the mock builds its HLS
stream with ffmpeg and the runner image has none. The workflow now installs it, which also un-skips
two tests that had been skipping silently — including the one asserting a segment really is
MPEG-TS. That stream is what `V-14` will try to decode on the headset, and it was being generated
nowhere in CI. A related defect in the mock's no-ffmpeg fallback is issue #15.

The run summary prints test counts per suite. `RK-07` — a session weakening a gate to get green — is
the risk this project is least able to notice on its own, and a count that drops between two runs is
the cheapest possible signal.

## 2026-11-10 — Catalogue ids are hashes, not sequence numbers

**`C-01a`.** An item's numeric id is CRC-32 of its id string. The blueprint used sequential integers
(398 of them), which cannot survive the catalogue growing: inserting an item renumbers everything
after it, and since a saved island stores these numbers, every island anyone had built would quietly
come back with the wrong contents. A hash is stable across additions by construction.

It is also derivable by both toolchains — Python at build time for the generated constants, C# at
runtime when a save file hands over an id string — and a test asserts the two agree against values
computed from neither. The cost is that collisions are possible rather than impossible; the
validator fails the build on one, on a zero hash (zero means "no item"), and on two ids that would
generate the same C# name.

**The honest-count rule.** The blueprint's catalogue claimed 631 items and resolved to about six
distinct geometries: 95 architecture modules all pointed at one wall box. So a mesh is identified by
**the checksum of its asset file**, not by the catalogue entry that references it, and the report
counts distinct meshes and palette variants separately (`C-01b`). Two entries sharing a mesh are one
mesh and two variants, and claiming otherwise is not possible rather than merely discouraged.

**Brought forward, and split.** The catalogue package was one entry parked behind `M3-RETRO`. It
needs no Unity, no GPU and no spending decision, and `05-OPERATIONS.md` §4 allows taking
later-milestone work while the current milestone waits on a human gate — which M1 is doing, on
PR #10. It is also the real prerequisite for any asset work at all: until the catalogue, the
validator and the ingest pipeline exist, generated or downloaded meshes have nowhere to go. It is
now `C-01a` (schema, loader, ids — done) and `C-01b` (validator, licence ledger, honest count), and
the packages that depended on the whole of it depend on `C-01b`.

## 2026-11-10 — The catalogue's facts are bound to the asset by checksum

**`C-01b`.** The validator needs to know a mesh's triangle counts, bounds, texture sizes and edge
counts. It does not re-derive them from the GLB: Blender has exact topology at the moment it
exports, and reconstructing it afterwards is both lossy and a second implementation to keep
correct. So the ingest pipeline writes a small JSON **facts file** per item and the validator reads
that.

That trades a real risk — facts can say anything — for a cheap one. Every facts file carries the
CRC-32 of its asset and the validator recomputes it from the file on disk, so facts and asset are
bound together or the item is refused. There is no legitimate way for them to disagree, because
`C-02`'s pipeline is deterministic; a mismatch means the asset changed without the pipeline
re-running, or someone edited the facts by hand.

**Four checks are there because the blueprint failed them.** An LOD chain whose levels do not
actually reduce is refused (its "LODs" were the same mesh three times: all the memory, none of the
saving). Open geometry must be *declared* double-sided rather than assumed — legitimate for a grass
blade or a petal, fatal for anything the player can walk behind, and in VR that is everything.
Non-manifold geometry is never allowed, because it breaks collider generation, normals and welding.
And one mesh shared across two different `kind`s fails: sharing geometry between a spring and an
autumn oak is the palette system working, sharing it between a stair and a roof is what the
blueprint did ninety-five times.

**Per-item budgets are data** (`content/budgets.yaml`), derived by dividing ADR-10's subsystem
allowance by roughly the instance count that subsystem expects. They are guesses and say so, in the
file. Recalibrating them from device readings is then an edit to a table rather than a change to a
program.

**The licence allowlist cites its own authority.** `content/licences.yaml` names the `DECISIONS.md`
record that admitted attribution licences, and the ledger tool checks that heading really exists. A
future session cannot widen what the project may use by editing a data file; widening it means
writing down why, where the owner can read it. Attribution is enforced rather than encouraged — an
attribution-licensed item with no author or no source url fails the build, because the alternative
is a notices screen that quietly omits people whose work is in the game. Non-commercial licences are
admitted and listed in their own section of the generated notices, so that if this project's purpose
ever changed, the list of what would have to go already exists.

The catalogue is still empty, so none of this is exercised by real content yet. It is exercised by
50 tests that each build a complete valid item and then break exactly one thing — which is the only
way to know a validator works, since one that has never refused anything is indistinguishable from
one that cannot.

## 2026-11-10 — Auto-merge treated "no checks" as "all checks passed"

**It fired before it was noticed, which is the useful part of this entry.** `F-13`'s auto-merge
waits until every check on the head commit has succeeded and none is still running. On a commit
with no check runs at all, both conditions are true of an empty set, so the label alone was
sufficient. Between 22:23 and 22:58 it merged four pull requests — `V-07a`, `F-03`, `C-01a` and
`C-01b` — that no CI had touched.

The merges themselves are sound: every package was proven locally before it was pushed, and the
`checks` workflow later ran green on a commit containing `V-07a` and `C-01a` together. But the
mechanism did exactly what it was warned about earlier the same day, in the entry above this one,
and the warning did not stop it because the fix was sitting in an unmerged pull request.

**The guard now requires at least one check to have succeeded.** An empty set of checks is a
refusal, not a pass, and the skip message names the `checks` workflow as the thing to fix if it is
not running. Nothing else about the merge conditions changed.

**The wider lesson is about ordering.** The automation that lands work was built and merged before
the automation that verifies it. That ordering is backwards and it produced exactly the failure it
implies. `F-07a` exists because of it, and nothing of this kind should be merged again while the
thing that checks it is still in review.

## 2026-11-11 — Undo history is two lists, not a ring buffer

**`V-08`.** A ring buffer is the obvious shape for bounded history and it is where three of the
four defects in the draft lived. Batches could be split when the write head overtook them, so
undoing one left the world in a state it had never been in. Entries that fell off the end were
never released, so a long session leaked every object it had ever deleted — the draft hid deleted
objects with `SetActive(false)` and nothing ever destroyed the ones history forgot. And the redo
branch was "invalidated" by zeroing a counter while the stale entries stayed in the buffer, where a
later walk could reach them again.

Two `List<Entry>` instead. Eviction becomes an explicit operation on the front of a list, which is
the thing that has to be right, and it is now covered by tests that assert a command is released
exactly once however it leaves history.

**History is bounded by memory, not by a count of steps.** A terrain stroke carries a compressed
region delta; a move carries two transforms. Counting steps would either hold a hundred megabytes
of terrain or throw away a hundred cheap moves. Both bounds exist and either trips eviction. The
step limit is **128**, which is what the spec asks for — the draft's constant said 256 while its
own comment said 128, the kind of disagreement that decides nothing until it decides the wrong
thing.

**Two eviction rules, both chosen deliberately.** A whole batch goes or none of it does, because
half a batch undoes into a state that never existed. And the newest step is never evicted, even
when it alone exceeds the memory bound: being unable to take back the thing you just did is worse
than holding one oversized delta, and refusing to record it is the same failure in a different
coat.

**A terrain stroke stores both sides of its dirty rectangle, uncompressed in value and deflated in
storage.** Replaying a brush backwards does not return the ground to where it started — the
exponential approach in `TerrainSculptor` is not symmetric, and nor is the weight blend, which
renormalises — so undo is a write of stored values rather than an inverse operation. The values are
stored as whole floats, not the 16-bit quantisation the save format uses, because undo that returns
the ground to *almost* where it was leaves a visible seam where the restored region meets the ground
around it. Deflate then gets it back: a measured stroke over 3,969 samples stores **8 KB against 63
KB raw, 12%**, so a full 128-step history of sculpting is about a megabyte.

**A stroke is one step, not one per frame.** The sculptor runs every frame a button is held;
capture opens when the stroke starts and closes when it ends. Holding the brush down for three
seconds is one thing to undo, which is what the person did.

**The draft had no terrain command at all** — spawn, delete, transform, scatter, and nothing for
sculpting, which is both the thing people do most and the thing they most need to take back,
because a brush's falloff is not invertible by eye.

## Open questions for the owner

- Does the "Remediation Plan" cited by the blueprint still exist? Five of its items are referenced
  but never described. If so, add it to `docs/blueprint/`.
- Is this intended as a store release, or only for your own headset? (Decides how much of M11 is needed.)
- The three questions in `07-STREAMBOX.md` §9.
