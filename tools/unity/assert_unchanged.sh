#!/usr/bin/env bash
# Fails if an editor run left any committed project file changed, or any new untracked file in the
# project folders (F-07, ADR-06).
#
# Usage: tools/unity/assert_unchanged.sh "<what ran>"
#
# Covers Assets, Packages and ProjectSettings: settings, .meta files, the XR assets, the boot scene
# and the package lock. Library/, Temp/ and Builds/ are git-ignored and so never count. An import
# that writes a .meta or a setting nobody committed means the next machine would import something
# different from this one — the drift ADR-06 exists to stop.
set -uo pipefail

what="${1:-the editor}"
watched=(Assets Packages ProjectSettings)

changed="$(git status --porcelain --untracked-files=all -- "${watched[@]}")"
if [ -z "$changed" ]; then
  echo "Nothing committed was changed by ${what}, and nothing new was left in ${watched[*]}."
  exit 0
fi

# The whole patch, for the workflow to keep as an artefact: the log below is cut short, and a
# 900-line settings diff is exactly the case where the cut-off part is the interesting one.
patch="${PROJECT_CHANGES_PATCH:-/tmp/project-changes.patch}"
{ git --no-pager diff -- "${watched[@]}"; git ls-files --others --exclude-standard -- "${watched[@]}"; } > "$patch" 2>/dev/null || true

echo "${what} left these changed or new (whole patch: $patch):"
echo "$changed"
echo
git --no-pager diff --stat -- "${watched[@]}" || true
git --no-pager diff -- "${watched[@]}" | head -300 || true
echo "::error title=Project changed by ${what}::${what} changed or added files under ${watched[*]}. Commit them through unity-configure, or stop the editor writing them."
exit 1
