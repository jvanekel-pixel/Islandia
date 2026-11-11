# 00 — Start here (for the owner)

Everything a human has to do, in one file. The rest of `docs/plan/` is written for Claude.

## 1. What you are starting

An autonomous build of **Islandia**. A scheduled Claude Code cloud session — the *driver* — wakes
every two hours, takes the next work package from an ordered backlog, builds it, proves it in
GitHub Actions, merges it and records what it did. About 113 packages remain, through
fourteen milestones. Each milestone publishes an installable build and a report.

You are needed at five *human gates* after orientation, where something only a person with a
headset can judge. Three of those five are short and optional.

**Read `01-AUDIT.md` §1–2 before you start.** The original blueprint needed real changes and you
should know what they are.

## 2. HG-0 — Orientation (15–30 minutes)

### Step 1 — Create the repository · 2 min

On GitHub: **New repository** → name it (suggested `islandia`) → **Public** (decision D2) → leave
it completely empty (no README, no licence, no .gitignore).

### Step 2 — Give Claude access · 2 min

Open <https://claude.ai/connect-github> and make sure the **Claude GitHub App** is installed on the
new repository. (It is already installed on your account; you may only need to add this
repository to its list.)

### Step 3 — Unity credentials · 5 min

Unity runs in GitHub Actions and needs your account. You do **not** need to install the Unity
Editor, and — despite what almost every guide says — you do **not** need a licence file.

1. Create a Unity ID at <https://id.unity.com> if you do not have one. Check that you qualify for
   Unity Personal under Unity's current terms.
2. **Set a password.** If you sign in with Google you do not have one yet: use the password reset
   at <https://login.unity.com> with that address. It does not unlink Google.
3. Add two **repository secrets** (repository → Settings → Secrets and variables → Actions →
   New repository secret):

   | Secret name | Value |
   | :-- | :-- |
   | `UNITY_EMAIL` | your Unity ID email |
   | `UNITY_PASSWORD` | the password from step 2 |

**Never paste these into a chat.** Secrets in a public repository are not visible to anyone and
are not given to workflows from forks.

> **Why no licence file?** The common advice is to activate Personal in Unity Hub and paste the
> resulting `.ulf` into a `UNITY_LICENSE` secret. That cannot work on a cloud runner, and we
> proved both halves of why rather than guessing: a `.ulf` from the Hub is bound to the machine
> that made it, so the editor rejects it with *"Machine bindings don't match"*; and Unity no
> longer supports manual activation of Personal licences at all, so the older `.alf` exchange
> that would have produced a container-bound file is closed too. CI instead asks Unity's
> licensing service for a seat using the account's own credentials, and gives it back when the
> job ends.

### Step 4 — Repository settings · 3 min

Repository → **Settings → Actions → General**:

- **Fork pull request workflows from outside collaborators:** *Require approval for all external
  contributors.*
- **Workflow permissions:** *Read and write permissions*, and tick *Allow GitHub Actions to create
  and approve pull requests*.

Do not add a self-hosted runner to this repository, ever: on a public repository that would let a
stranger's pull request run code on your PC.

### Step 5 — Start · 2 min

**If the planning session that produced this is still open:** reply there with the repository name
(`owner/repo`). It will attach the repository, push this bundle and start the first session.

**Otherwise:** extract the bundle zip, then in PowerShell from the extracted folder:

```powershell
git init -b main
```
```powershell
git add -A
```
```powershell
git commit -m "Planning bundle"
```
```powershell
git remote add origin https://github.com/OWNER/REPO.git
```
```powershell
git push -u origin main
```

Then start a new session at <https://claude.ai/code> on that repository, choose **Opus** as the
model and **Auto** as the permission mode, and send the contents of `docs/plan/prompts/kickoff.md`.

### Step 6 — Approve what the kickoff session asks for · 2 min

It will ask you to approve a dedicated **cloud environment** for this repository (a setup script
and a short list of network hosts), and it will create the **scheduled task** that runs the
driver. If it cannot create the scheduled task itself it will tell you to create one at
<https://claude.ai/code/routines> with the prompt in `docs/plan/prompts/driver.md`, every two
hours, new session each run, model Opus.

That is the end of orientation. Milestone M1 needs nothing more from you.

## 3. Decisions

Yours, made on 2026-11-10 and 2026-11-11:

| # | Decision | What was decided |
| :-- | :-- | :-- |
| **D1** | Target headset | **Quest 2.** It is the design, budget and test target. Everything must fit it |
| **D2** | Repository and CI | **Public repository, GitHub-hosted runners. No cost.** |
| **D4** | Media | **No Spotify, no in-world web browser. Your StreamBox must work natively in-world** (`07-STREAMBOX.md`) |
| **D5** | Content | **Premade free asset packs only, chosen by you.** No fixed count; coherence per island theme. You upload each wave of archives to a draft release (below). Was ≈ 120 models plus generators, changed 2026-11-11 |
| **D6** | Model relay | **Deployed.** Importing your own models and painting them is a major feature. Free tier only, preferably one that needs no payment card. Changed 2026-11-11 |
| **D9** | Trains and roller coasters | **The heart of the gift.** Endless small islands, a station on each, a train between them; build roller coasters and ride them (ADR-24, ADR-25). 2026-11-11 |
| **D10** | Avatar | **A generic premade avatar** from your packs. No character creation; better later, maybe (ADR-27). 2026-11-11 |
| **D11** | Pets | **One stray cat per island.** Feed it and it gets a collar, and a food and water dish appear (ADR-16). Replaces adoptable pets. 2026-11-11 |
| **D12** | Painting | **Paint anything**, with a fill that respects edges and corners so it paints one part, not the whole object (ADR-26). 2026-11-11 |
| **D13** | StreamBox profile | **A dedicated profile with no restricted libraries**; the contract description stays public (`07-STREAMBOX.md` §9). 2026-11-11 |

Defaults chosen for you — change any of them by saying so:

| # | Decision | Default | Change it if… |
| :-- | :-- | :-- | :-- |
| **D3** | Name and package id | *Islandia*, `dev.islandia.app` — **final** (2026-11-11) | The display name can change any time; the package id never can |
| **D7** | Other cuts | No `.blend` upload, no hosted Blender converter, SpaceWarp off, internet radio not built unless you ask | See `01-AUDIT.md` |
| **D8** | Who it is for | **A gift for your partner.** Never published, never sold | Settled 2026-11-10. The store track is cut; M14 is a signed build you install over USB |

Three things you should know about your own decisions:

- **D1 — the Quest 2 is the tightest target.** Roughly half the draw calls and triangles of a
  Quest 3, one video screen at a time, four active pets. The plan budgets for that from the start.
- **D2 — public means public.** The source is visible to anyone. It will include a description of
  how StreamBox's client API works (`07-STREAMBOX.md` §3) — no address, no secrets, nothing from
  your library, but a public description of a private service. If you would rather not publish
  that, the alternative is a private repository with a build runner on your PC; say so before
  Step 1.
- **D4 — StreamBox stays a separate project.** Nothing in the Islandia build touches the StreamBox
  repository. On present evidence it needs no changes.

## 4. What you will see

- **Pull requests** opening and merging on their own, each for one work package.
- **`docs/plan/STATE.md`** — the one file to read to know where things stand.
- **GitHub Releases** named `M1`, `M2`, … each with an APK and a report.
- **Issues labelled `human-gate`** assigned to you when you are needed. Turn on notifications for
  the repository so these reach you.
- **Issues labelled `blocked`** when the driver gave up on something after three attempts and
  moved on. You do not need to act on these; they are picked up at the next retro.

## 5. The later human gates

Each gate arrives as an issue with exact steps and a checklist. This is what to expect.

### HG-1 — First headset check (after M2) · 30–45 min the first time

Recommended, not required: the build carries on either way. It is the first proof that the app
renders in stereo with tracking, and it measures **which way of playing video works on your
headset**, which the whole media milestone depends on.

One-time setup:

1. Create a developer organisation at <https://developers.meta.com> (account verification may be
   required).
2. In the Meta Horizon phone app: your headset → Headset settings → **Developer Mode** → on.
3. On your PC install `adb` (you already have the GitHub CLI if you used it above):

   ```powershell
   winget install Google.PlatformTools
   ```
   ```powershell
   winget install GitHub.cli
   ```
4. Connect the headset by USB and accept the debugging prompt inside the headset.

Then, each time: clone or update the repository and run the one command in the issue. The script
installs the build, runs the benchmark and the video probe, collects the results and uploads them.
Put the headset on, look around, try sculpting, and write two or three sentences in the issue.

### HG-R — Ride comfort (after M5) · 20–30 min

Non-blocking, and worth doing early: the train and the coasters are the heart of the gift, and
whether a ride feels good in a headset cannot be measured any other way. Install the M5 build, ride
the train between two islands, build a small coaster and ride it at each intensity, try watching
from outside, and note anything that felt wrong — or wonderful.

### HG-2 — StreamBox in-world (after M6) · 20–30 min

Also non-blocking. Install the M6 build, enter your server's
address once, approve the pairing on your phone choosing the headset's own profile (§3, D13), then: watch ten minutes of a film, play an album
through two speakers, and drive it from the StreamBox phone app. Note anything that felt wrong.

### HG-3 — Alpha device pass (after M12) · 60–90 min

Run the script again. Play every feature against the checklist in the issue, including sending a
model from the companion and painting it.

### HG-4 — The last gate (after M13) · 60–90 min, plus an unattended hour

Judge the art, comfort and sound — is this a nice thing to be given? Start the soak run (the
headset sits on a stand for an hour).

After this there is no submission and no review board. Milestone 14 is a signed build, a page of
notes written for whoever is going to play it, and handing it over.

## 6. What this costs

| Item | Cost |
| :-- | :-- |
| GitHub repository and CI | Free (public repository) |
| Unity | Free (Personal), subject to Unity's eligibility terms |
| 3D content | Free (packs of free assets you download) |
| StreamBox integration | Free; adds nothing to StreamBox's running cost |
| Model relay hosting | Free tier, preferably one that needs no payment card |
| Meta developer account | No fee. Needed only for developer mode on the headset, not for publishing |
| Claude | Your existing plan's usage. This is the one real cost: long Opus sessions, many of them |

Nothing in the plan requires a purchase. If the driver ever concludes one would help, it asks in a
`human-gate` issue and carries on without it.

## 7. Controls

| You want to… | Do this |
| :-- | :-- |
| Pause the build | Disable the *Island build driver* task at <https://claude.ai/code/routines> |
| Resume | Enable it |
| Go slower or use less of your plan | Change its schedule |
| Add 3D assets | Download the packs, then upload the `.zip` archives to a **draft** release at <https://github.com/jvanekel-pixel/Islandia/releases/new>, tag `assets-wave-<n>` (the next number), with each pack's download page listed in the description. Save it as a draft — never publish it. The next ingestion run picks it up |
| Change direction | Open an issue describing the change and label it `human-gate`, or edit `docs/plan/` and commit. The driver reads the plan fresh every session |
| Ask what is going on | Read `docs/plan/STATE.md`, or open any session on the repository and ask |
| Undo something | Revert the merge commit on GitHub. The next session adapts |
| Stop for good | Disable the task. Everything is in the repository |

## 8. If something goes wrong

- **Nothing has happened for a day.** Check the scheduled task is enabled and look at its last run.
  Then check for an open `human-gate` or `infra` issue.
- **CI is red on `main`.** The driver treats that as its first job next session. If it persists,
  there will be a `blocked` issue explaining why.
- **You disagree with something it built.** Say so in an issue. It is a normal input, not a failure.
- **You want a human-readable summary.** Every milestone report in `docs/reports/` is written for
  you, not for Claude.

## 9. Merging happens by itself

Since 2026-11-10 the repository merges finished work on its own
(`.github/workflows/auto-merge.yml`). You asked for this, and it is worth being clear about what
you traded: **nobody looks at a change before it reaches `main` any more.** That was a real safety
net.

What still has to be true before anything merges:

- every test has passed, and none is still running;
- the pull request is marked finished (the `awaiting-merge` label, which only a completed work
  package gets);
- the branch is in your repository, **never a fork** — anyone can open a pull request on a public
  repository, and this is what stops a stranger landing code on `main`;
- you are the author; it is not a draft; and it carries no stop label.

### If you want to look at something before it lands

Add the label **`do-not-merge`** to the pull request. It will sit there until you take the label
off. That is the escape hatch.

### If you want this turned off

Delete `.github/workflows/auto-merge.yml`, or rename it to end in `.disabled`. Everything falls
back to merging by hand, and the prompt below still works.

### Merging by hand, if you ever need to

```
Go to https://github.com/jvanekel-pixel/Islandia/pulls

For every open pull request in that list, starting with the lowest number:
open it, scroll to the bottom, click the green "Merge pull request" button,
then "Confirm merge", and wait for the purple "Merged" label before the next one.

If one shows a conflict warning instead of a green button, skip it and tell me
its number. When the list is empty, tell me which numbers you merged.
```
