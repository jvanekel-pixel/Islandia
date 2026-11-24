# Releases

One file per milestone, added in the pull request that closes it (`docs/plan/05-OPERATIONS.md` §9):

```yaml
# releases/M1.yaml
milestone: M1          # must match the file name
title: Factory online
draft: false           # optional; forced to true whenever a private-use pack is admitted (ADR-11)
```

The same pull request adds the report `docs/reports/M<n>-<slug>.md` — exactly one per milestone.
`make lint` refuses a release file without it.

Once merged, the `release` workflow (`.github/workflows/release.yml`) tags the commit `M<n>` and
publishes a GitHub Release titled `M<n> — <title>`, with the report as its notes and two files
attached: `Islandia-M<n>.apk` and the report. Auto-merge dispatches it; it can also be run by hand,
and does nothing for a milestone that already has a release.
