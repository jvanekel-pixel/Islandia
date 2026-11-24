---
name: "HG-1 — First headset check"
about: "After M2. The first build on the Quest 2: stereo, tracking, a frame time, and which video backend works."
title: "HG-1: first headset check — M2 build"
labels: ["human-gate", "needs-device"]
assignees: ["jvanekel-pixel"]
---

<!-- Opened by a session when M2 is released (package M2-RETRO). Fill every <angle-bracket> field
     before posting. Never put a server address, token or account name in this issue: it is public. -->

**Recommended, not required.** The build carries on either way. About 30–45 minutes the first time.

**Release:** <link to the M2 release>

## What this is for

The first proof that Islandia renders in stereo with tracking on your headset, and the measurement
the media milestone depends on: **which way of playing video works on the Quest 2** (`V-14`).

## Before you start (one time — already done on 2026-11-12)

- [ ] Developer Mode is on for the headset, and Android platform tools (`adb`) are on the PC.
- [ ] A **data** USB cable. A charge-only cable shows up as a connect chime about every 30 s.

## Steps

1. Connect the headset to the PC by USB.
2. Run, from a clone of the repository:

   ```powershell
   <the one command, filled in by the session>
   ```

   The **Allow USB debugging** box appears in the headset only after the PC first runs `adb`, and
   it can sit out of the wearer's line of sight — look around for it. Over Wi-Fi later,
   `adb connect <address>:5555` works too; do not post the address here.
3. The script installs the build, runs the benchmark and the video probe, and uploads the results.
4. Put the headset on, look around, try sculpting.

## Checklist

- [ ] The script finished and uploaded its results
- [ ] Both eyes render, and the world stays put when you move your head
- [ ] Sculpting responds to the controllers
- [ ] Nothing made you feel unwell

## In your own words

<two or three sentences — what felt right, what felt wrong>
