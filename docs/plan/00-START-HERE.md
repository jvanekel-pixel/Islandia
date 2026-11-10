# 00 — Start here (for the owner)

Everything a human has to do, in one file. The rest of `docs/plan/` is written for Claude.

## 1. What you are starting

An autonomous build of **Islandia**. A scheduled Claude Code cloud session — the *driver* — wakes
every two hours, takes the next work package from an ordered backlog, builds it, proves it in
GitHub Actions, merges it and records what it did. About 108 packages lead through twelve
milestones. Each milestone publishes an installable build and a report.

You are needed six times: once now, then at five *human gates* where something only a person with
a headset can judge. Two of those five are short and optional.

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

### Step 3 — Unity licence · 10 min

Unity runs in GitHub Actions and needs your free Personal licence. This needs your PC once. You do
**not** need to install the Unity Editor.

1. Create a Unity ID at <https://id.unity.com> if you do not have one. Check that you qualify for
   Unity Personal under Unity's current terms.
2. Install **Unity Hub** and sign in.
3. In Unity Hub: **Settings (gear) → Licenses → Add → Get a free personal license**.
4. The licence file is now at `C:\ProgramData\Unity\Unity_lic.ulf`.
5. Add three **repository secrets** (repository → Settings → Secrets and variables → Actions →
   New repository secret):

   | Secret name | Value |
   | :-- | :-- |
   | `UNITY_LICENSE` | The entire contents of `Unity_lic.ulf` |
   | `UNITY_EMAIL` | Your Unity ID email |
   | `UNITY_PASSWORD` | Your Unity ID password |

   With the GitHub CLI signed in, PowerShell does it without copy-paste (replace `OWNER/REPO`; the
   last two prompt you for the value):

   ```powershell
   Get-Content "C:\ProgramData\Unity\Unity_lic.ulf" -Raw | gh secret set UNITY_LICENSE --repo OWNER/REPO
   ```
   ```powershell
   gh secret set UNITY_EMAIL --repo OWNER/REPO
   ```
   ```powershell
   gh secret set UNITY_PASSWORD --repo OWNER/REPO
   ```

**Never paste these into a chat.** Secrets in a public repository are not visible to anyone and
are not given to workflows from forks. If activation fails (for example because of two-factor
sign-in), the first CI run says why and the driver opens an issue with exact steps. Licence files
expire; when yours does you will get the same kind of issue, and the fix is steps 3–5 again.

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

Yours, made on 2026-11-10:

| # | Decision | What was decided |
| :-- | :-- | :-- |
| **D1** | Target headset | **Quest 2.** It is the design, budget and test target. Everything must fit it |
| **D2** | Repository and CI | **Public repository, GitHub-hosted runners. No cost.** |
| **D4** | Media | **No Spotify, no in-world web browser. Your StreamBox must work natively in-world** (`07-STREAMBOX.md`) |

Defaults chosen for you — change any of them by saying so:

| # | Decision | Default | Change it if… |
| :-- | :-- | :-- | :-- |
| **D3** | Name and package id | *Islandia*, `dev.islandia.app` (placeholder) | You want a different package id. It must be final before the first upload to Meta and cannot change after |
| **D5** | Content targets | Alpha ≥ 120 distinct models; Beta ≥ 250 distinct models and ≥ 400 catalogue entries; 631 is a stretch | You want fewer, more coherent items. The blueprint's 631 were about six shapes; these are real models from CC0 packs and generators |
| **D6** | Model relay hosting | Built and tested locally; deployed to a scale-to-zero free tier only if you create an account | You want no hosted service at all. The imports folder works without one |
| **D7** | Other cuts | No `.blend` upload, no hosted Blender converter, SpaceWarp off, internet radio not built unless you ask | See `01-AUDIT.md` |
| **D8** | Release intent | Planned as if for the Meta store, decided for real at HG-5 | You only ever want it on your own headset. Then M11 shrinks to a signed build |

Three things you should know about your own decisions:

- **D1 — the Quest 2 is the tightest target.** Roughly half the draw calls and triangles of a
  Quest 3, one video screen at a time, four active pets. The plan budgets for that from the start.
  Meta has also said it will stop accepting *new* Quest 2 store submissions after December 2027.
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

### HG-2 — StreamBox in-world (after M4) · 20–30 min

Also non-blocking, and the one you will want to do. Install the M4 build, enter your server's
address once, approve the pairing on your phone, then: watch ten minutes of a film, play an album
through two speakers, and drive it from the StreamBox phone app. Note anything that felt wrong.

### HG-3 — Alpha device pass (after M9) · 60–90 min

Run the script again. Play every feature against the checklist in the issue. Decide whether to
deploy the model relay.

### HG-4 — Beta review (after M10) · 60–90 min, plus an unattended hour

Judge the art, comfort and sound. Start the soak run (the headset sits on a stand for an hour).

### HG-5 — Release (after M11) · 1–2 hours

Create the signing keystore and add it as a secret, create the app in the Meta dashboard and add
the upload credential, approve the privacy policy draft, capture store media from the shot-list,
submit. Or, if this is only ever for you, just install the signed build.

## 6. What this costs

| Item | Cost |
| :-- | :-- |
| GitHub repository and CI | Free (public repository) |
| Unity | Free (Personal), subject to Unity's eligibility terms |
| 3D content | Free (CC0 packs and generators) |
| StreamBox integration | Free; adds nothing to StreamBox's running cost |
| Model relay hosting | Free tier if you want it; nothing if you do not |
| Meta developer account | No fee expected; verify when you create it |
| Claude | Your existing plan's usage. This is the one real cost: long Opus sessions, many of them |

Nothing in the plan requires a purchase. If the driver ever concludes one would help, it asks in a
`human-gate` issue and carries on without it.

## 7. Controls

| You want to… | Do this |
| :-- | :-- |
| Pause the build | Disable the *Island build driver* task at <https://claude.ai/code/routines> |
| Resume | Enable it |
| Go slower or use less of your plan | Change its schedule |
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

## 9. One optional file that makes the build fully hands-off

Measured on 2026-11-10: a session's permission classifier refuses to let a session merge its own
pull request ("Merge Without Review"), and refuses to let a session write its own permission file.
Both refusals are correct. GitHub's auto-merge is not a way round the first — enabling it is a
GraphQL call, and GraphQL is blocked in cloud sessions.

So you have a choice, and either is fine:

**A. Leave it as is.** The driver builds each work package, proves it, opens a green pull request
with the evidence in the body, labels it `awaiting-merge`, and moves on. You merge them whenever
you like — one click each, and they batch up happily. Nothing stalls while they wait, and you get
a look at everything before it lands.

**B. Let the driver merge.** Create `.claude/settings.json` in the repository with the content
below and commit it. The driver then merges its own pull requests once their checks are green, and
you are not in the loop at all until the first human gate. The gates that matter are still
enforced: required status checks, the acceptance criteria, and the stop-the-line rule.

A session cannot create this file for you — that is the point of the refusal — so it is yours to
make or skip.

```json
{
  "permissions": {
    "allow": [
      "Bash(make:*)",
      "Bash(python3:*)",
      "Bash(dotnet:*)",
      "Bash(npm:*)",
      "Bash(npx:*)",
      "Bash(node:*)",
      "Bash(git:*)",
      "Bash(gh api:*)"
    ],
    "deny": [
      "Bash(git push --force:*)",
      "Bash(git push -f:*)",
      "Read(./**/*.ulf)",
      "Read(./**/*.keystore)",
      "Read(./**/.env*)"
    ]
  }
}
```

If you take option B and later regret it, delete the file: the driver falls back to option A by
itself, because `05-OPERATIONS.md` §7 describes both.
