---
name: "Blocked work package"
about: "Stop the line: a work package failed three times (or four red Unity runs). 05-OPERATIONS.md §8."
title: "<WP-ID> blocked: <one-line diagnosis>"
labels: ["blocked"]
---

<!-- Opened by a session that stops the line (05-OPERATIONS.md §8). The owner does not need to act
     on this; it is picked up at the next retro. Then set the package to `blocked` in
     docs/plan/BACKLOG.yaml, leave the draft pull request open with the `blocked` label, and
     rename its branch prefix to `claude/parked-`. -->

**Work package:** `<WP-ID>` — <title>
**Draft pull request:** <link>

## What was tried

1. <attempt 1 — what changed, and the run or command that judged it>
2. <attempt 2>
3. <attempt 3>

## The exact failure

```text
<the failing output, 30 lines or fewer>
```

## Best diagnosis

<why it fails, and how sure you are>

## Cheapest way forward

<the fallback — a pre-agreed one from docs/plan/06-RISKS.md if one exists, and whether it was
taken as a new work package>
