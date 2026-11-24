---
name: "Infrastructure"
about: "CI failed for a reason that is not the code: licence, runner disk, registry limits, a broken mock."
title: "infra: <what broke>"
labels: ["infra"]
---

<!-- Opened after one retry has also failed (05-OPERATIONS.md §6). Work carries on with packages
     that do not depend on it. Never paste a secret, a server address or a licence file here. -->

**Workflow and run:** <link to the run>
**First seen:** <date> · **Retried:** <link to the retry>

## What failed

```text
<the failing output, 30 lines or fewer>
```

## Why it is infrastructure, not code

<the evidence: same commit passed before, the error is in licence activation, the disk, the network…>

## What is waiting on it

<work packages that cannot proceed, or "nothing">
