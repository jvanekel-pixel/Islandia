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
`C-02b`'s pipeline is deterministic; a mismatch means the asset changed without the pipeline
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

## 2026-11-11 — The StreamBox client takes its transport by injection, and its address never enters the core

**`SB-01c`.** Two reasons, and the second is the one that decided it.

The obvious one is testability: every state the gateway can put the client in — pending for three
polls then approved, a denial, a 401 in the middle of browsing — is two lines against a fake
transport, and would otherwise need a server that could be told to misbehave on command.

The one that settles it is that **this repository is public**. The gateway's address is given to
the transport, at the edge, by the Unity layer reading device configuration. `Island.Core` never
sees it, so no amount of carelessness inside the core can commit it. `07-STREAMBOX.md` §1 requires
that the address never appear here; making it structurally absent is better than remembering.

The device token is handled on the same principle. It lives in the client and in the
`Authorization` header of the request it belongs to — never as a default header on a shared
`HttpClient`, where a call made after signing out could still carry it, and never in a URL, a log
line or an exception message. A test asserts that an error raised by a failing call does not
contain the token.

**A 401 is the one status with a required consequence.** The device session is revocable from
StreamBox, so a 401 means the token is gone for good. The client drops it and falls back to
`Unpaired` *before* the exception leaves, because an app that treats it as an ordinary error retries
forever with a credential that will never work again.

**Pairing polls at the server's interval, not ours.** The gateway rate-limits the pairing endpoints,
and polling faster than asked is how a device flow earns a denial for the pairing it was trying to
complete. The one liberty taken is a floor of one second, so a server answering `intervalSeconds: 0`
does not produce a busy loop.

**`SB-01c` is split.** It is now the HTTP half; the Connect socket is `SB-01d`. Request and response
is everything the island needs to browse and to start playing; the socket is what remote control
needs, and it is a different transport with its own failure modes — close codes that must not be
retried, a reconnect policy, a state feed. One package each.

**Four music routes are deliberately missing.** The contract snapshot declares `/music/search`,
`/music/artists`, `/music/playlists` and `/music/favorites`; the mock gateway implements none of
them, so a client method for any of them could only guess at a response shape and would be proven
against nothing. They arrive when the mock does. This is also a gap in `SB-01a`, whose contract
tests are supposed to check the mock against the snapshot and did not notice four declared paths
were unserved — filed as an issue.

**The end-to-end tests start the mock themselves**, on a port the operating system picks, and run
the real pairing walk over a real socket. They are marked `EndToEnd` and excluded from
`make core-test`, which exists to be a one-second suite; `make streambox-e2e` runs them. They wait
in real time, because the thing being proven is the real thing.

## 2026-11-11 — Connect reports state on change, and the position is not a change

**`SB-01d`.** `07-STREAMBOX.md` §3.6 asks for state "on every change and every few seconds while
playing". Taken literally that is a message every frame, because the playback position changes
seventy-two times a second — which would be seventy-two messages a second to tell the phone the
obvious.

So a change is any field a remote would *draw differently*: the item, playing, buffering, muted,
has-next, volume, duration, and the audio and subtitle selections. Position rides along on whatever
message goes out, and the periodic re-send every five seconds is what keeps the phone's scrubber
honest. `EveryFieldARemoteDrawsCountsAsAChange` pins each field individually, through the public
method rather than by reaching for the throttle.

**There is no background timer.** `ReportAsync` is called by the app as often as it likes and
decides for itself whether to send. The alternative — a second loop pushing state on a schedule —
would mean a test either racing it or waiting on it in real time, and concurrency that only
misbehaves on a headset is the kind of defect this project cannot afford to find at a device gate.

**The socket transport is pull-shaped**, not event-shaped: `ReceiveAsync` returns the next message
or null once closed. An event-based transport would hand the session callbacks on whatever thread
the socket library chose, which on a headset is a source of races nobody would find until HG-2.

**Reconnect policy.** 4401 and 4409 are never retried — 4401 because the token is gone and
reconnecting fails forever with a credential that will never work again, 4409 because the owner has
directed that playback be one-at-a-time with the newest device winning, so reconnecting would be
this island fighting the phone for the stream. Everything else doubles from one second to a cap of
thirty. A gateway that is down comes back on its own schedule.

**Two bugs the tests found, both mine.** The no-token path set `Done` inside the connection attempt
and then had it overwritten by the loop's `Offline`, so it retried forever and the test died of an
out-of-memory error after ninety seconds of accumulating backoff delays. And `RunAsync` returned
quietly for an already-cancelled token while throwing for one cancelled mid-flight; it now throws in
both cases, because inconsistent cancellation is worse than either choice.

The no-token path also no longer claims a close code. Setting `FinalCloseCode` to 4401 when the
gateway never sent one puts a fiction in front of whoever reads it to find out why Connect stopped.

## 2026-11-11 — The import gatekeeper enforces glTF's node-tree rule

**`I-01`.** ADR-12 makes the headset the trust boundary: the browser converter checks a file too,
but it runs in the player's own browser and can be bypassed by dropping a file straight into the
imports folder. So the gatekeeper believes nothing a file says about itself, validates every length
against the bytes actually present before using it, and never allocates to a size the file asked
for.

**The limits are a shared rule file** (`contract/import/limits.v1.json`), read by both the
gatekeeper and the converter (`I-04`). Two copies would drift, and the symptom would be a player
watching the converter say "ready" and the headset say "no". `allowExternalUris` is checked and
refused on load rather than merely defaulted to false: honouring a file's URIs would let a dropped
model make the headset fetch whatever it named, from inside the player's home network.

**Image headers are read, not file sizes.** A 16,384 × 16,384 PNG is a few hundred kilobytes on
disk and about a gigabyte decoded, so size is no guide at all to cost. The declared `mimeType` is
not trusted either — a decoder acts on the magic bytes, so the magic bytes are what is read, and a
label that disagrees with the content is itself a refusal.

**The real finding was a dead guard.** The node walk pushed every node as a root, so each was
visited at depth zero and the depth limit could never fire. A rule that cannot fire is worse than no
rule, because it reads as handled — the same fault the budget work turned up in the device-tier
table, and the test that was meant to prove the limit worked is what caught it.

Fixing it properly meant enforcing what glTF actually says: **nodes form a disjoint union of trees,
so a node has at most one parent.** That is not pedantry. A shared child is drawn once per path to
it, so a file can double the number of paths at every level — forty levels is a trillion draw paths
described in a few kilobytes, and counting them *is* the denial of service. Enforcing the tree
property refuses that outright, kills cycles, makes each node visited exactly once so the triangle
and draw counts are exact, and makes the depth limit mean something.

**Refusals say what to do.** "This model has 92,000 triangles and the limit is 40,000. The converter
can simplify it" rather than "validation failed" — `RT-09` is that people cannot tell why a file was
rejected, and the person reading this is the one who will be wearing the headset.

**Nothing escapes as an exception.** A hostile file is a verdict. The caller is an import screen,
and a throw there is someone watching their world disappear. Covered by 2,000 randomised inputs,
500 bit-flipped copies of a valid file, and truncation at every length from zero to the whole file —
each of which must return a refusal with a reason, not throw.

## 2026-11-11 — Blender is pinned to 4.5.16 LTS, and it can do the work

**`C-02a`.** The content track's last unknown. `C-02b` and `C-03` both need headless Blender, nothing
had ever run it here, and four separate things were open questions — three of which could only be
answered by running it in a container with no display and no GPU. All four now pass, in CI:

| What | Result |
| :-- | :-- |
| Python scripting with `--background --factory-startup` | a cube is 8 verts, 6 faces |
| Topology counts the catalogue validator reads | closed cube: 12 triangles, 0 boundary, 0 non-manifold; one face removed: 4 boundary |
| GLB export, the format `I-01` reads and glTFast loads | 69,684 bytes, header `glTF` |
| A thumbnail with **no GPU and no display** | a real 128px PNG, via Cycles on CPU |

The render was the one in doubt. EEVEE wants OpenGL, which a CI container does not have, so
thumbnails go through Cycles on the CPU. Slower, and it works, which beats faster and absent.

**Pinned exactly, verified by checksum.** `4.5.16` and its published SHA-256 live in
`tools/blender/install.sh`, and an archive that does not match is not unpacked — the script runs as
root in CI and extracts a few gigabytes of executables, so "probably the right file" is not good
enough. Not from apt, either: the distribution's version moves with the runner image and nothing
announces it. `C-02b` requires that a rerun produce byte-identical files, and Blender's mesh
operators, glTF exporter and renderer all change between releases, so "whatever Blender is
available" is not a pipeline but a lottery whose result gets committed.

**Its own workflow, path-filtered.** Installing Blender is a 360 MB download. Putting that on every
push to pay for a probe that only matters when the content tooling changes would be a poor trade, so
`content.yml` runs on the paths that can affect it. The archive is cached with the key derived from
`install.sh` itself, so bumping the pin invalidates the cache by construction rather than by someone
remembering to.

## 2026-11-11 — The Unity project, and the versions the resolver chose

**`F-03`.** The project exists, generated by the "Create Unity project" workflow and committed by
it. Editor pinned to `6000.3.28f1` with revision `8f082d2fe476`. Nothing was created by hand:
ADR-06 forbids writing Unity's YAML by hand, because a hand-made settings file opens fine and is
subtly wrong in ways that only appear on a device.

**Packages were added by name with no version.** These are what the resolver chose for this editor,
recorded here because the criterion asks for them and because they are the answer to "what was it
built against" a year from now. 67 packages are locked in total; 16 of them came
in as dependencies of these 51.

| `com.unity.ai.navigation` | 2.0.16 |
| `com.unity.burst` | 1.8.31 |
| `com.unity.cloud.draco` | 5.6.0 |
| `com.unity.cloud.gltfast` | 6.20.0 |
| `com.unity.cloud.ktx` | 3.8.0 |
| `com.unity.collections` | 2.7.1 |
| `com.unity.inputsystem` | 1.20.1 |
| `com.unity.mathematics` | 1.3.3 |
| `com.unity.modules.accessibility` | 1.0.0 |
| `com.unity.modules.adaptiveperformance` | 1.0.0 |
| `com.unity.modules.ai` | 1.0.0 |
| `com.unity.modules.androidjni` | 1.0.0 |
| `com.unity.modules.animation` | 1.0.0 |
| `com.unity.modules.assetbundle` | 1.0.0 |
| `com.unity.modules.audio` | 1.0.0 |
| `com.unity.modules.cloth` | 1.0.0 |
| `com.unity.modules.director` | 1.0.0 |
| `com.unity.modules.imageconversion` | 1.0.0 |
| `com.unity.modules.imgui` | 1.0.0 |
| `com.unity.modules.jsonserialize` | 1.0.0 |
| `com.unity.modules.particlesystem` | 1.0.0 |
| `com.unity.modules.physics` | 1.0.0 |
| `com.unity.modules.physics2d` | 1.0.0 |
| `com.unity.modules.screencapture` | 1.0.0 |
| `com.unity.modules.terrain` | 1.0.0 |
| `com.unity.modules.terrainphysics` | 1.0.0 |
| `com.unity.modules.tilemap` | 1.0.0 |
| `com.unity.modules.ui` | 1.0.0 |
| `com.unity.modules.uielements` | 1.0.0 |
| `com.unity.modules.umbra` | 1.0.0 |
| `com.unity.modules.unityanalytics` | 1.0.0 |
| `com.unity.modules.unitywebrequest` | 1.0.0 |
| `com.unity.modules.unitywebrequestassetbundle` | 1.0.0 |
| `com.unity.modules.unitywebrequestaudio` | 1.0.0 |
| `com.unity.modules.unitywebrequesttexture` | 1.0.0 |
| `com.unity.modules.unitywebrequestwww` | 1.0.0 |
| `com.unity.modules.vectorgraphics` | 1.0.0 |
| `com.unity.modules.vehicles` | 1.0.0 |
| `com.unity.modules.video` | 1.0.0 |
| `com.unity.modules.vr` | 1.0.0 |
| `com.unity.modules.wind` | 1.0.0 |
| `com.unity.modules.xr` | 1.0.0 |
| `com.unity.multiplayer.center` | 1.0.1 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |
| `com.unity.render-pipelines.universal` | 17.3.0 |
| `com.unity.test-framework` | 1.6.0 |
| `com.unity.xr.hands` | 1.10.0 |
| `com.unity.xr.interaction.toolkit` | 3.7.0 |
| `com.unity.xr.management` | 4.7.0 |
| `com.unity.xr.meta-openxr` | 2.7.0 |
| `com.unity.xr.openxr` | 1.19.0 |

`Packages/packages-lock.json` is the pin, not this table — a version typed into a document is a
guess that ages. This is a record.

**It is idempotent**, which was the criterion worth having: the workflow opens the project a second
time and reports *"Stable: opening the project again changed nothing."* A project that rewrites its
own settings on every open would make every later pull request noise, and nobody could tell a real
change from a shuffle.

**Four runs to get there, and every failure was worth having.** Run 1: packages were resolved in the
repository, whose code needs `Newtonsoft.Json`, which is one of the packages being added — the
compile error aborted batchmode before the resolver ran. Behind it, `Island.Core.asmdef` referenced
Newtonsoft nowhere at all, so run 2 would have failed identically after the resolver had done
everything right. Run 2 then got Unity fully green and failed on two things outside it: git refusing
the checkout as "dubious ownership", because the container runs as root over a checkout owned by the
runner, and `diff <(…) <(…)` in a step GitHub runs under `sh` rather than bash. Run 3 was green with
`commit=false`; run 5 committed.

None of that was discoverable by reading. It is the argument for dispatching the thing rather than
reasoning about it.

## 2026-11-11 — The test runner comes before the work it tests

**`F-07b`.** The plan had a circle in it. `F-04`'s criteria are all `[L2]` — EditMode tests proven
in CI — and the workflow that runs EditMode tests belonged to `F-07`, which sits behind `F-06`,
`F-05` and `F-04` itself. So the first package that needed the harness could not have been proven
with it, and the only ways out were to write tests nothing ran or to call a criterion met on a local
run.

This is the same mistake as building auto-merge before the checks it waits for, recorded yesterday:
**the thing that verifies the work has to exist before the work it verifies.** Twice in two days
suggests it is worth stating as a rule rather than noticing again.

`F-07b` is the EditMode/PlayMode workflow, depending only on `F-03`, placed ahead of `F-04`. `F-07`
keeps the Android build, the Library cache, the package-source artefact and the APK artefact — all
of which need a scene and a build target that do not exist yet.

**An empty run fails.** `tools/unity/test_summary.py` refuses a result file reporting zero tests,
and refuses a missing file too. Both are what a test assembly that did not compile looks like from
outside the editor, and both would otherwise read as a pass — `Island.Core.Tests` is behind
`UNITY_INCLUDE_TESTS` and an `Editor` platform filter, either of which could silently match nothing.

**PlayMode is wired but not yet a gate**, because no PlayMode assembly exists until `F-06`'s boot
tests. The workflow says so in the step rather than skipping quietly, and the moment a PlayMode test
exists the empty-run refusal applies to it as well.

**The seat is shared.** All three Unity workflows use `concurrency: unity-licence`, because there is
one Personal seat: two runs at once would race for it and one would fail for a reason unrelated to
the code. The seat is returned in an `if: always()` step, so a failing test run does not strand it.

## 2026-11-11 — Auto-merge's trigger list is now checked, not remembered

**The same fault twice in two days.** Yesterday auto-merge relied on `check_suite: completed`,
which GitHub never fires for our own workflows because their check suites are created with
`GITHUB_TOKEN`; the fix was `workflow_run` naming `checks`. Today `unity-tests` was added, nothing
named it, and `F-07b`'s own pull request skipped at *"1 still running"* and sat green waiting for an
event that could not come. `content` was missing too.

The mechanism was right both times. What was wrong is that **the list has to be remembered**, and
nothing about adding a workflow hints that a different workflow needs editing. So the invariant is
now a check: `tools/lint/check_workflows.py` fails if any workflow triggered by `push` or
`pull_request` is absent from `workflow_run.workflows`, and also fails if that list names a workflow
that does not exist — because a name matching nothing waits forever, just as silently.

It is in `make lint`, so it runs on every commit, and it has eight tests of its own including the
exact 2026-11-11 mistake. It found both omissions the moment it was written, which is the only
evidence worth having for a linter.

**Why this keeps happening.** Both faults share a shape with the two before them: the thing that
verifies the work arriving after the work, and a guard that could never fire. The common cause is
that automation failures are *quiet* — a pull request sitting green looks like nothing happening,
not like something broken. Every fix in this entry is therefore a check rather than a habit.

## 2026-11-11 — The resolved package sources, and why the registry cannot replace them

**`F-07c`**, split out of `F-07` and brought forward for the third time this week, because `F-04` is
the first package that needs it. `05-OPERATIONS.md` §5 step 3 already told sessions to check any
unfamiliar Unity, URP, OpenXR or glTFast API against the real package source — and the thing it
pointed at did not exist.

This is not a convenience. A session cannot compile Unity code here, so each wrong API guess costs a
ten-minute CI round trip against a cap of four per work package. `F-04` has to set roughly twenty
ADR-03 properties on a `UniversalRenderPipelineAsset`, several of which have **internal setters** and
must go through `SerializedObject` by their serialised field names. Guessing those names is three or
four runs; reading them is one.

**I tried to avoid the CI run and could not.** Unity's public registry serves package tarballs, so
fetching URP 17.3.0 directly looked like a way to skip the whole workflow. It is not: that registry's
newest URP is **10.10.1**, and 17.3.0 is a Unity 6 package which is not published there at all. The
resolved cache in a Unity container is the only place those sources exist. Checking that took two
minutes and saved building the wrong thing.

**What is kept and dropped.** `.cs`, `.shader`, `.hlsl`, `.compute`, `.cginc` and each
`package.json`, for the packages a session is plausibly stuck on. Tests, samples, documentation and
binaries are dropped — none answers "what is this property called", and all of them would make the
artefact large enough that nobody downloads it. Every resolved package is listed in the index with
its version even when its sources were not kept, so the index never implies a package is absent.

**Keeping nothing fails.** An empty artefact uploads perfectly happily, and the next session would
download it, find nothing, and have no way to tell whether that is the truth.

## 2026-11-11 — ADR-03 in code, and four things the real URP sources corrected

**`F-04`.** The configurator sets every ADR-03 item and `ProjectConfigurationTests` asserts each one
by loading the committed assets from disk — not by checking the objects the configurator returned.
ADR-06 notes batchmode builds have historically reset generated settings, so what matters is what a
build will load. **334 EditMode tests pass in CI**, 312 from `Island.Core` and 22 of these.

Everything was read out of URP 17.3.0 via `F-07c`'s artefact rather than recalled, and four of those
readings changed what I would otherwise have written:

**Most of those properties have public setters.** I expected several to need `SerializedObject`.
Exactly three do: `mainLightRenderingMode`, `additionalLightsRenderingMode` and
`supportsMainLightShadows` have `internal set`, so they go through `SerializedObject` by their
serialised names. Reflection on the private field would set the value in memory, lose it on the next
write, and keep reading correctly in the same session — which is the fault the criterion exists to
prevent, and why a test compares the serialised value against the property rather than trusting
either.

**Render Graph cannot be set.** In this editor version `enableRenderCompatibilityMode` reads `false`
unconditionally and its setter carries `[Obsolete(..., error: true)]`; assigning it does not compile.
So Render Graph is simply on, and the test asserts it. If a later URP makes it settable, that test is
what will notice the default moved.

**`GPUResidentDrawerMode` lives in a third assembly** — `Unity.RenderPipelines.GPUDriven.Runtime`,
not URP's own or core's. A property on `UniversalRenderPipelineAsset` whose type is in neither was
not a shape I expected, and it cost the one compile error of this package.

**ASTC has two settings and only one of them travels.**
`EditorUserBuildSettings.androidBuildSubtarget` lives in `Library/`, which is not committed, so a
test asserting it would pass on the machine that ran the configurator and fail on every fresh
checkout — including the CI run meant to prove it. `PlayerSettings.Android.textureCompressionFormats`
is the one that is serialised into `ProjectSettings`.

**URP creates assets of its own accord.** The first successful run put
`UniversalRenderPipelineGlobalSettings.asset` and `DefaultVolumeProfile.asset` at the `Assets` root.
The first of those is where `RenderGraphSettings` lives, so committing only
`Assets/Island/Generated` would have left the Render Graph assertion passing locally and failing
everywhere else. They are committed where URP puts them rather than moved into
`Assets/Island/Generated`: `GraphicsSettings.asset` references them by GUID, and relocating them is
a cosmetic gain against a real risk. A declared deviation from ADR-06's folder convention.

## 2026-11-11 — A workflow that commits cannot trigger the workflow that checks it

Found finishing `F-04`. `unity-configure` committed the generated assets and pushed them, and
`unity-tests` did not run — because the push used `GITHUB_TOKEN`, and GitHub does not start a
workflow run from an event created with it. The same rule that made `check_suite` useless for
auto-merge.

So **after running a workflow that commits, the workflow that verifies the commit has to be
dispatched by hand.** That is now in `05-OPERATIONS.md`. It is not worth a deploy key or a bot
token to automate: the generating workflows are dispatch-only and rare, and a session that has just
asked for one can ask for the other.

## 2026-11-11 — The owner's re-plan: what changed, in one place

**Decided by:** the owner, in a new planning conversation, after reviewing where the build stood.
Each decision has its own entry below; this one records the mechanics.

**Milestones renumbered.** Two new milestones — Islands and the train (M4) and Roller coasters
(M5) — come straight after the foundations, and Paint anything (M10) is new. Old → new: M4 media →
**M6**, M5 garden → **M7**, M6 builder → **M8**, M7 import → **M9**, M8 pets → **M11 The island
cat**, M9 alpha → **M12**, M10 beta → **M13**, M11 finished → **M14**. Retro packages were renamed to
match. **Entries above this one use the old numbers.**

**Gates.** New non-blocking `HG-R` (ride comfort) after M5. HG-2 (StreamBox) now follows M6, HG-3
M12, HG-4 M13. HG-4 is still the last gate.

**Moved.** `A-01` (audio core) from the media milestone to M3, because the train and coaster rides
need the mixer before StreamBox does. Content ingestion (`C-01a`–`C-02d`) to M2, because wave 1 —
coaster pieces and avatar included — is the input the ride milestones need.

**New tracks.** `TR` islands and the train, `RC` roller coasters, `AV` the avatar, `PNT` painting,
`CAT` the island cat. `tools/lint/check_plan.py` learned the new prefixes so prose mentions of
them are checked like any other.

**Acceptance lines changed, not deleted** (`CLAUDE.md` order of authority):

- `F-07` — the APK artefact rule gains an exception for builds holding private-use packs.
- `F-09` — releases become drafts when the build holds private-use packs; the gate templates are
  HG-1, HG-R, HG-2, HG-3 and HG-4 (HG-5 went with the store track on 2026-11-10).
- `C-02b` — palette mapping applies to flat-colour packs; textured packs keep their textures.
- `C-02c` — rewritten: draft-release input, licence classification, per-theme admission.
- `W-11` — per-instance tint rather than the generators' vertex masks, which no longer exist.
- `C-04`, `C-10`, `C-12` — counts against the ≈ 120 target became "what the owner's packs provide
  and fits a theme", because the target is lifted.
- `P-10` — island slots became per-island saves through the world index.
- `R-03` — "only if the relay is used" removed; it is used.
- Added lines on `Q-02`, `Q-04`, `Q-05` and `I-07` for the stress world, the opening by train, the
  private-pack check and card-free hosting.

**Cut** (`status: cut`, kept for history): `C-03` and `PET-01` to `PET-06`.

**Also:** the old scheduled driver was paused by the session that took these decisions down — see
`STATE.md`.

## 2026-11-11 — Content is the owner's premade packs, delivered on draft releases

**Decided by:** the owner. "I want to move to completely premade digital assets. I didn't know
there would be so much free stuff."

- **Premade free packs are the only content source** (ADR-11). Parametric generators are dropped
  as a source and `C-03` is cut. The ≈ 120-mesh target of 2026-11-10 (D5) is lifted: the catalogue
  holds what the owner's packs provide and what passes the validator and the eye. Coherence still
  beats count, now judged per island theme — with many islands, packs in different styles can
  live on different islands.
- **How archives arrive — answers issue #25 and `RT-06`.** The owner downloads each pack by hand
  (which honours donation pages; nothing scripts past one) and uploads the archives as assets on a
  **draft** GitHub Release tagged `assets-wave-<n>`. A draft is visible only to people with write
  access, and is not reachable by its tag, so ingestion lists releases with `GITHUB_TOKEN` and
  fetches by asset id. The archive checksum is the reproducibility pin that a download URL was
  supposed to be.
- **Licence rule, restated.** Admitted: anything whose licence allows use in a private gift that
  is never distributed. Recorded per pack in the ledger, with the licence text from the archive;
  unreadable licences go to the owner, not guessed. The 2026-11-10 rule refusing anything that
  forbids redistribution in a built application is replaced, because a build installed on one
  headset is not a redistribution.
- **But the repository is public**, and committing a file is publishing it. So each pack is
  classified *redistributable* (CC0, CC-BY and similar) or *private-use-only*, and private-use
  output never enters git, a published release or a workflow artefact (`C-02d`, ADR-19). An APK
  that contains private-use assets goes only to a draft release. Open design point, settled in
  `C-02d`: whether processed private-use output lives on the draft release or in the Actions
  cache. `C-02d` is cut if every pack turns out redistributable.
- The owner has already downloaded a first set of packs, including avatar packs and coaster
  models; wave 1 is the first upload.

**Evidence:** issue #25 (Kenney's download runs through a donation interstitial; no pinnable URL);
GitHub's documented visibility of draft releases.

## 2026-11-11 — Islands and the train, and roller coasters, are the heart of the gift

**Decided by:** the owner. "When I told you this was a gift and you asked what we could
incorporate to make this special for the user — these are both it. He loves trains and roller
coasters, so including those is most important."

- **Endless islands, a station on each, and a train between them** (ADR-24, `TR-01`–`TR-05`). It
  keeps each island small and contained, lets each be its own idea, and suits the Quest 2: only one
  island is resident at a time, and the train ride covers the load. The first launch begins on
  the train, arriving at the first island — the opening of the gift.
- **Build roller coasters and ride them** (ADR-25, `RC-01`–`RC-05`). The owner supplies the coaster
  models; the work is the builder and the ride. Comfort is treated as a requirement, because a
  coaster is the classic way to make someone sick in VR (`RK-12`), and gets its own early gate,
  `HG-R`, so the comfort numbers are measured on a headset rather than guessed.
- **Ordering:** straight after the foundations (M4, M5), ahead of media, garden, building kit and
  import, so they sit at the heart of the alpha and get the beta's polish. The engine-free halves
  can be built now, while Unity work waits on CI.
- **Never cut** (`RK-09`): the train, the coasters, painting, and StreamBox playback.
- Rides draw on the *Architecture and props* budget until measured (ADR-10); no budget number was
  changed, because none has been measured.

## 2026-11-11 — One stray cat per island replaces pets

**Decided by:** the owner. "Instead of there being a pet generator or different pets, I just want a
different stray cat on each island and you can interact, and if you do choose to feed it, it gets
a collar and food and water dish like another placeable object."

ADR-16 rewritten; `PET-01`–`PET-06` cut; `CAT-01`–`CAT-04` in M11. Navigation and behaviour carry
over from the pet design in smaller form. The budget's hard cap of four active pets becomes one
cat per island (ADR-10); `CAT-02` changes the cap in the ledger code and its test, which today pin
four.

## 2026-11-11 — The avatar is generic; character creation is dropped

**Decided by:** the owner. "Drop the anthro character creation and use a generic avatar that I
will upload for now. We will add better later, maybe."

ADR-27; `AV-01` in M4. The avatar is seen only where the player can see themselves from outside:
at tabletop scale and when watching a ride from outside. A character creator is deferred, not
planned.

## 2026-11-11 — The relay is deployed, and painting is a major feature

**Decided by:** the owner (D6 = yes). "I want to be able to import 3D assets into the world, and
those items should be customisable — I should be able to paint them. It would be cool if the game
could identify edges and corners so that the user could fill and it not do the whole object. This
is a major feature that needs a lot of attention during the build."

- **Relay deployed** (ADR-13, `I-07`, `R-03`). Free tier only; preferably a provider that needs no
  payment card, since Cloud Run's free tier — the earlier default — needs a billing account, and
  "nothing may cost money" is safest when nothing *can* (`RX-10`).
- **Painting** gets its own milestone, M10 (ADR-26, `PNT-01`–`PNT-06`), and applies to everything
  placed: pack items, structures, imported models, the cat's dishes, coaster cars and the train.
  The fill respects edges: meshes are segmented into regions at material slots, UV islands and
  sharp edges, with one implementation in `Island.Core` used both at ingestion and on the headset.
  The risk is segmentation quality on arbitrary meshes (`RT-21`); `PNT-06` gives the player a way
  to correct it, and the brush always works regardless.

## 2026-11-11 — StreamBox: public contract, dedicated profile, no restricted libraries

**Decided by:** the owner, who asked for "whatever you think is safest and smartest"; these are
the choices made on that instruction.

1. **The contract description stays public.** It holds no secret or address, and it is already in
   history, so removing it would hide nothing while costing the public repository and its free CI.
2. **The headset pairs to a dedicated profile.** It can be revoked alone, without touching the
   owner's other devices, and keeps its own history.
3. **No restricted libraries on that profile**, so they are not reachable from the headset.
   Islandia needs no change for this; the HG-2 pairing step names the profile.

`07-STREAMBOX.md` §9 and ADR-23 updated.

## 2026-11-11 — The cloud environment, and the hosts it allows

`F-10`. The owner approved a personal environment, `islandia`, proposed from a session that had
only this repository attached. Its setup script installs the .NET 9 SDK system-wide
(`/usr/share/dotnet`, symlinked into `/usr/local/bin` so it works whatever `HOME` is), the Python
tooling requirements, and the mock gateway's npm packages, and restores the core test project. It
is idempotent and every step is non-fatal, because a failing setup script stops a session from
starting. Measured in a session: 25 s cold, 2 s warm; `make check` then passed (7 lint checks,
134 tooling, 312 core, 30 contract, 11 end-to-end).

Network is **limited**. Package registries and MCP servers are allowed by default; beyond them:

| Host | Why |
| :-- | :-- |
| `dot.net` | Serves `dotnet-install.sh` (redirects) |
| `aka.ms` | The installer reads the channel-9.0 link from it to find the latest SDK |
| `builds.dotnet.microsoft.com` | The installer script and the SDK tarball |
| `api.nuget.org` | NuGet restore; the proxy's default registry list does not include NuGet |
| `api.github.com` | `gh api` REST calls the driver loop runs on (`05-OPERATIONS.md` §6) |
| `github.com` | Release-asset downloads and git over HTTPS |

The Default environment was unrestricted; this is narrower on purpose. If a session needs another
host, it says which and why, and the owner approves that one host.

## 2026-11-11 — OpenXR in code: no depth submission, and the Android image for configure and tests

**Decided by:** work package `F-05`.

- **No depth submission.** The blueprint (§1.3C) asks for 16-bit depth submission "for compositor
  layer depth sorting and Application SpaceWarp". Neither exists here: there are no depth-sorted
  composition layers, and SpaceWarp sits behind a no-op interface (audit, phases 1–3, decision 2).
  Submitting depth makes the GPU store the depth buffer out of tile memory every frame, which is
  the cost ADR-03's other choices exist to avoid. Revisit only alongside a package that adds a
  consumer for it, measured at a device gate.
- **Features are chosen by id, and nothing else is on.** Android enables Meta Quest Support,
  Foveated Rendering (SRP foveation API), the XR Hands tracking subsystem, Meta Display Utilities,
  and two interaction profiles: Oculus Touch (the Quest 2's controllers) and Hand Interaction. Every
  other feature is switched off, except Meta's hidden life-cycle helper, which Meta OpenXR manages
  itself and its features need. Multiview render regions are not enabled: they need hardware the
  Quest 2 does not have.
- **`unity-configure` and `unity-tests` run on `ubuntu-6000.3.28f1-android-3`.** OpenXR only
  creates settings for a build target the editor can build for, so on the base image the
  configurator would have found no Android settings to write. This is the build-and-test image
  this log named on 2026-11-10; the two workflows had been using the base image only because
  nothing before F-05 needed the Android module.
- **"A batchmode build resets it" is guarded twice.** `XrConfigurationTests` reads the committed
  assets by path, and `unity-tests` fails any run after which `Assets/XR` or
  `ProjectSettings/EditorBuildSettings.asset` differs from the commit. The Android build itself
  arrives with `F-07`, which must keep the same check after its build step.

**Evidence:** the `com.unity.xr.openxr` 1.19.0, `com.unity.xr.management` 4.7.0,
`com.unity.xr.meta-openxr` 2.7.0 and `com.unity.xr.hands` 1.10.0 sources from the `F-07c`
artefact — `OpenXRPackageSettings.GetSettingsForBuildTargetGroupFromPackageSettings` returns null
for a group whose module is missing, and `FeatureHelpers.RefreshFeatures` is the public route that
creates the settings.

## 2026-11-11 — Editor-only XR files are ignored, not committed

**Decided by:** work package `F-05`, on its first `unity-tests` run.

The new guard in `unity-tests` (no editor run may leave `Assets/XR` changed) failed with every
test green. Nothing committed had changed. Opening the project created three untracked files:
`Assets/XR/Settings/OpenXR Editor Settings.asset` (OpenXR's internal editor-side settings), and
`Assets/XR/UserSimulationSettings/` and `Assets/XR/Resources/`, which hold AR Foundation's per-user
XR Simulation settings and preferences (AR Foundation arrives as a dependency of Meta OpenXR, and
its build processor strips the simulation files from player builds). None of these is read by
a build, and committing per-user files invites churn, so they go in `.gitignore`.

The guard is unchanged and still fails on any modified, deleted or new file under `Assets/XR` or in
`ProjectSettings/EditorBuildSettings.asset` that is not one of these.

**Evidence:** `unity-tests` run 43102051087 — EditMode and PlayMode passed, and the guard listed
exactly those three paths as `??`, with no `M` or `D` lines.

## 2026-11-11 — The player thinks VR is dumb, and that is the brief

From the owner, answering the naming question: the headset is theirs, and the person it is for
thinks VR is dumb. They asked him what game he would want to play, and this is it.

That sharpens what "a nice thing to be given" means. He is not someone who will forgive a rough
first minute because VR is novel. So, wherever a package leaves room for judgement:

- **Comfort beats spectacle.** Seated play works everywhere; snap turn and teleport are the
  defaults; nothing moves the camera that he did not ask to move. Rides (`RC-*`, `TR-*`) open at
  their gentlest settings, with a vignette and a fixed horizon cue on by default, and a "watch
  from outside" option is never more than one button away. One queasy coaster ends the gift.
- **The first five minutes are the product.** No account, no tutorial wall, no menu before the
  island. He arrives by train at a station, sees a coaster he can ride, and finds out by doing
  that he can build one (`Q-04`'s criteria already ask for this; this is why it matters).
- **Controls he does not have to learn.** Few buttons, the same meaning everywhere, and hands
  where they work.

Package id `dev.islandia.app` is final and the display name stays *Islandia* (ADR-22, D3).

## 2026-11-12 — An island from a seed: integers only, a station on the shore, and its ground locked

**Decided by:** the build session for `TR-01a`, inside ADR-08 and ADR-24.

**`TR-01` is split.** All of it came to about 1,400 hand-written lines, so `TR-01a` (seeds,
starting terrain, the station site and its reservation) ships first and `TR-01` keeps the world
index and the per-island files (05-OPERATIONS.md §5). The backlog note on `TR-01` records the
planned file layout.

- **Seeds are SplitMix64, split by purpose.** Not `System.Random`, whose algorithm differs
  between Mono and CoreCLR and has changed between .NET versions. Terrain, station, theme and
  (later) the cat each draw from their own stream, so adding a draw to one never moves another.
  The first three outputs for seed 0 are pinned against the published values.
- **Generation is integer arithmetic,** heights in 1/1024 m, converted to metres once by an exact
  power-of-two division. Floating-point maths is deterministic on one machine but not across
  them — IL2CPP on the headset may fuse a multiply-add that Mono rounds twice — so none is used.
  Two seeds' full fingerprints (every height's bits and every weight) are pinned in a test that
  runs under CoreCLR in `make core-test` and under Mono in Unity's EditMode job.
- **New islands' surfaces** come from height: sand below `GrassLineMetres` (+0.1 m), grass
  above `FullGrassMetres`, both derived in `IslandConstants` from the sand line.
- **The shape:** a landmass near the middle, three to five lobes out towards the coast, up to two
  hills kept clear of the station. Nothing reaches within 2 m of the 64 m extent, so open water
  rings every island; a test checks the edge of 150 seeds.
- **The station faces one of four directions** with a seeded offset of up to 6 m along that side,
  so its footprint and corridor are axis-aligned rectangles. Its seaward edge is where the
  station's full width first meets land walking in from the edge, at least 4 m in; the strip in
  front of it, the station's width out to the edge, is kept as water, so it always faces open
  water and the 4 m corridor runs down its middle. The 12 m × 8 m footprint is levelled to +0.5 m and painted
  as path, ground beside and behind it is eased towards it over 2 m, and nothing seaward of it is
  touched — that is the view down the line. Dimensions are ADR-08 constants.
- **The site is saved, not recomputed.** A `STAN` section carries it, so a later change to
  generation can never move a station out from under the ground built around it.
- **The reservation is a lock on the heightfield,** checked by `TerrainSculptor` itself, so every
  brush and the undo command that replays one obey it without being told. Loading and generation
  still write locked samples. Placement has no system yet, so `StationSite.Permits` is the rule,
  and `V-10` gained an acceptance line that requires it.
- **`IslandGenesis.EnsureStation`** gives a station to ground that has none (a save from before
  islands had stations) through the same survey, changing only the samples it must and
  repainting those by their new height. Reading the `STAN` section locks the ground it is given,
  so no load path can forget to; `Heightfield.ClearLocks` frees a field for another island.
- **Themes are provisional ids** (`meadow`, `woodland`, `village`, `harbour`) until `C-02c`
  admits pack items per theme; the index will store the id, so the list can grow without moving
  any island's.

## 2026-11-12 — The world index is checked against the island files, not trusted

`TR-01`, after an independent review of the world index. The index is rewritten when islands
are made or deleted, not when one is saved, so its backup can be many sessions old. Trusting a
recovered index would have issued a used id again and overwritten that island, then its backup.

So the island files on disk are the truth about which islands exist, and the index is the truth
about what they are called:

- When the index comes back from its backup, or is missing while island files exist, it is
  **reconciled by file name only** (no island is read): islands it lost are listed again as
  "Island <id>", entries whose file is gone are dropped, and every id on disk is reserved.
- A new island **never takes an id whose file is on disk**, whatever the index says.
- A **thumbnail must be a plain file under `thumbnails/`**, checked when set and when read,
  because deleting an island deletes its thumbnail.
- An **unreadable single-island save is renamed `.unreadable` and the world opens new**, instead
  of failing at every launch. The file is kept.
- Names are cut at 40 characters without splitting an emoji.

Each has a test in `IslandWorldTests`.

## 2026-11-12 — The coaster pieces come from the pack, and their frames are integers

**Decided by:** the build session for `RC-01a`, inside ADR-25. **`RC-01` is split** (05-OPERATIONS.md
§5): the measuring tool, the generator and the frame model alone came to about 1,100 hand-written
lines before tests, so `RC-01a` ships the pieces and their frames, `RC-01b` the validation and `RC-01` keeps the save
section and the undo commands.

**What the pack is.** Kenney's Coaster Kit 1.0 (CC0), 183 models. 138 are track: six styles
(steel, wood, mouse, hanging, monorail, flume) of the same 23 shapes. The styles share their
centrelines exactly — the same ends, and every vertex of all six within 0.58–0.61 units of one
centreline — so a style is a skin chosen per coaster, not a different set of pieces. The rest are
seven cars (1.3 units long, 0.7 wide), twelve support parts, the station's platform, gate and
fence, and park dressing.

**How it was measured** (`tools/content/measure_coaster_kit.py`, re-runnable on the pack, never
committing it). The rails' cross-section at the open end of the steel straight is a ring of 24
vertices; every other model is searched for that ring at each quarter turn of heading and eighth
turn of pitch and bank, and a match is an open end only when the track within a unit of it lies
wholly to one side. The frame's point is **the underside of the spine, one unit below each
model's origin**: it is the axis the lean pieces roll about, and with it every end of every piece
lands on a half-unit grid. Between the ends the centreline is a line, a bump, a loop, or a cubic
Bézier — Kenney's curves are Béziers whose handles run half the forward distance (a 45° end's
handle is one unit), which the spine's vertices show: a quarter turn bulges 2.7% inside a circle.
Each fit is checked rather than trusted: the facts record how far the furthest vertex of any style
lies from it, and every shape comes out within 0.03 units of a straight's (the loop is the widest).

**What it found.** The grid unit is the pack's unit, held in Core as 64 sub-units. A straight is
4 long and the station's one-unit track tile 1; quarter turns have radius 2 or 4 and the climbing
ones rise 1; the S-bend shifts 2 sideways over 4; hills rise 1 or ½ over 4, level to level; the
steep pieces rise 1 over 4 into or out of 45°; bumps rise or dip ½; the loop is 4 tall and comes
out one unit to the right of where it went in; the leans roll 45° or 90°; the vertical bends rise
2 or 4 to vertical. Every heading change is a quarter turn and every pitch and bank an eighth.
Facts are bound to the models by CRC-32 per style, as the catalogue binds facts to assets.

**The set: 37 pieces** (`content/rides/coaster-track.yaml`), each a model as-is, reversed (ridden
backwards: a right turn becomes a left, a climb a descent), mirrored, or both. A station; straight
and short straight; tight and wide turns each way; climbing and falling turns in both sizes and
hands; S-bends each way; hills and low hills up and down; steep climb and drop, start and end;
bump and dip; a loop each way; a lean each way; and six lifts. **A lift is ours, not the pack's**
— it has no chain — so it is a role on the climbing shapes (hill, low hill, steep climb, tight
climbing turn). The pack has no brake; if `RC-02` needs one it is a role on a straight, a data
change.

**Left out,** because they do not belong with the rest: the vertical bends (they end pointing
straight up and nothing in the pack continues from there but a loop's own top), the 90° leans
(track on its side with no banked turn to use it), and the end caps and single sleeper, which are
`RC-03`'s dressing, not track. Supports are not pieces: `RC-03` stacks the one-unit columns, feet,
braces and cross-beams under the samples each piece lists as standing.

**Integers, not floats.** A frame is a position in sub-units, a heading in quarter turns and a
pitch and bank in eighths. A piece's exit is stored relative to its entry turned by heading only,
as the models are built, and a piece fits only where the pitch and bank are the ones its entry
expects — except a piece whose whole centreline lies on its roll axis (straights and leans), which
fits at any bank and carries it. Composing is then a quarter-turn swap and an integer add: no
rounding, nothing to drift, identical under CoreCLR, Mono and IL2CPP. General rotation
composition was not an option: with 45° pitches and banks in the set, composing them freely
escapes every exact representation (and builds twisted turns on slopes that nobody wants). A test composes
the same 200-piece circuit as rigid transforms in doubles and agrees with the integer frames at
every piece; the integer circuit closes with an error of exactly zero.

**Scale: two metres to the unit,** a planning figure from the pack's proportions — a two-seat car
1.4 m wide, a tight turn of 4 m radius, an 8 m loop. Track is stored in units, so `RC-03` can
change it against the avatar without touching a saved coaster.

**For comfort** (2026-11-11, "The player thinks VR is dumb"), each piece carries an intensity
class from its geometry alone: wide turns, shallow hills and lifts are *gentle*; tight turns, 45°
slopes, the S-bend and the airtime bumps *lively*; the loop and the leans *thrilling*. It is for
the builder's palette and the gentle setting, which can offer only gentle pieces by default, until
`RC-02` rates whole coasters from their forces. Nothing else was added for rides.

## 2026-11-12 — The boot scene, the composition root, and which generated assets are committed

**`F-06`.** One boot scene, made by `Island.Editor.BootSceneBuilder` from `ProjectConfigurator.Apply`,
holding a `CompositionRoot` and a plain rig (origin, offset, camera) and nothing else. The root picks
the platform once at boot: one a test supplied, else the headset if `Island.XR` finds an XR loader
running, else the idle simulation. The editor and every PlayMode test have no loader (Standalone has
none, F-05), so they boot the simulation without being told to.

**Committed or rebuilt, per generated asset type** — the decision ADR-06 deferred to this package:

| Asset type | Where | Committed? | Why |
| :-- | :-- | :-- | :-- |
| Project and XR settings (`ProjectSettings/*`, `Assets/XR/*`) | as Unity places them | **Committed** | ADR-06: batchmode builds have reset them before; tests assert what is on disk |
| Rendering assets | `Assets/Island/Generated/Rendering` | **Committed** | F-04; tests read them back |
| The boot scene and the build's scene list | `Assets/Island/Generated/Scenes`, `EditorBuildSettings.asset` | **Committed** | A build and the PlayMode tests load it from the checkout; generating it in every job would put a Unity step in front of every test run |
| `.meta` for scripts and folders | beside them | **Committed** | Deterministic GUIDs from `tools/gen_meta.py` |
| Content GLBs, thumbnails, atlases from the packs | CI artefact or draft release | **Rebuilt in CI** | Large, binary, and fully reproducible from the packs and the pipeline (`C-02*`) |
| `Library/`, import caches, the APK | runner | **Rebuilt** (cached by lockfile hash, F-07) | Machine state, never source |

The rule behind the table: commit what a build or test *reads* and is small text; rebuild what is
large, binary, or a cache. A committed generated asset is always backed by a test that its generator
would not change it — for the scene, `BootSceneTests.RunningTheBuilderAgainWritesNothing`, and the
`unity-tests` job fails if any editor run leaves the scene or the XR settings changed.

**The scene is in `Assets/Island/Generated/Scenes`, not `Assets/Island/Scenes`.** The criterion named
the second; ADR-06 says generated assets live under `Generated/`, and the ADR is the higher
authority. The criterion's path was corrected to match.

**Rerunning is a no-op by comparison, not by determinism.** Unity gives a freshly made scene new
file IDs, so regenerating a correct scene would rewrite it for nothing. The builder describes the
scene on disk — names, components, tag, clip planes, any moved transform — and only rebuilds when
that differs from its specification. The same description is the test that the committed scene is
right.

**No tracked-pose component and no `XROrigin` in the rig.** The head is placed by the root from
`IInputSource` every frame, and again just before rendering on a headset — the same path in the
simulation and on the device, which is what ADR-07 asks for. Two findings made this the safer
choice as well: the project's active input handler is the **legacy Input Manager**
(`activeInputHandler: 0`), so an Input System `TrackedPoseDriver` would see no devices; and the
headset input reads `UnityEngine.XR.InputDevices`, which works under either handler and allocates
nothing. `U-02` brings in XR Interaction Toolkit, which needs the Input System — that package must
switch the handler and add `XROrigin`; noted on it.

**Two clocks.** `Island.Runtime.IClock` is the frame clock ADR-07 names: advanced once per frame by
the root, scripted in tests on a fixed 1/72 s step. `Island.Core.StreamBox.IClock` is the StreamBox
client's wall clock with async delays. Different jobs, kept apart by namespace rather than merged.

**"Log is clean" means no warnings either.** The test framework already fails a test on a logged
error; the boot tests also fail on any warning, from anyone. A warning that genuinely has to be
tolerated goes in the test's `Tolerated` list with its reason.

**The PlayMode step is a real gate now.** `unity-tests` excused an empty PlayMode run until a PlayMode
assembly existed; this package adds the first, so a missing or empty PlayMode result fails.

**ADR-07 is checked, not just written.** `tools/lint/check_layering.py` holds the table as data:
every assembly definition in the project must be one it names, each references only what its row
allows, test assemblies must reference what they test, `Island.Editor` is editor-only, and
`Island.Runtime`'s sources may not use XR, Input System or editor types.

## Open questions for the owner

- Does the "Remediation Plan" cited by the blueprint still exist? Five of its items are referenced
  but never described. If so, add it to `docs/blueprint/`.
- Do the owner's packs include a **train and station**, and a **rigged cat**? If not, `TR-03` and
  `CAT-01` will propose specific free packs.

Answered and removed on 2026-11-11: whether this is a store release (no — a gift, 2026-11-10), and
the three questions in `07-STREAMBOX.md` §9.

## Open design points

Not questions for the owner; each is settled inside the package named.

- Where processed private-use output lives — the draft release or the Actions cache (`C-02d`).
- The coaster piece set, read from the owner's coaster pack (`RC-01`).
- Whether rides need their own budget row (`RC-03`).
- Which free host runs the relay without a payment card (`I-07`).
