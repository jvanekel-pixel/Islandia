# Release rehearsal

This prerelease was made by the `release` workflow to prove, on a pull request that changes how
releases are made, that the route works end to end: a tag is created, a GitHub Release is published
with the APK and a report attached, and both can be read back.

The workflow deletes this release and its tag as soon as it has checked them. If you can still see
it, that clean-up failed; it is safe to delete by hand.
