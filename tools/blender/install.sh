#!/usr/bin/env bash
#
# Installs the one Blender this project uses, verified against its published checksum.
#
# Pinned exactly, because the content pipeline must be deterministic (work package C-02: "a rerun
# produces byte-identical files"). Blender's mesh operators, its glTF exporter and its renderer all
# change between releases, so "whatever Blender the runner has" is not a pipeline, it is a lottery
# whose result is committed to the repository.
#
# Not installed from apt for the same reason: the distribution's version moves with the runner
# image, and nothing announces it.
#
# Usage:  tools/blender/install.sh [prefix]     (default /opt/blender)
#         BLENDER_PREFIX=... tools/blender/install.sh
#
# Idempotent: if the right version is already there, it does nothing and says so.

set -euo pipefail

# The pin. Changing either line is a decision, and belongs in DECISIONS.md with the reason.
VERSION="4.5.16"
SHA256="23432b064977908beb1ab4b7f32f024e7331105c875c187dce002aff84b10c11"

SERIES="$(echo "$VERSION" | cut -d. -f1-2)"
ARCHIVE="blender-${VERSION}-linux-x64.tar.xz"
URL="https://download.blender.org/release/Blender${SERIES}/${ARCHIVE}"

PREFIX="${1:-${BLENDER_PREFIX:-/opt/blender}}"
BLENDER="${PREFIX}/blender"

if [ -x "$BLENDER" ]; then
  have="$("$BLENDER" --version 2>/dev/null | head -1 | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' || true)"
  if [ "$have" = "$VERSION" ]; then
    echo "blender: $VERSION already at $PREFIX"
    exit 0
  fi
  echo "blender: found $have at $PREFIX, wanted $VERSION — replacing"
  rm -rf "$PREFIX"
fi

# With BLENDER_CACHE set, the archive is kept there between runs — which is what lets CI cache a
# 360 MB download instead of fetching it on every content change. Without it, a temporary
# directory, cleaned up on the way out.
if [ -n "${BLENDER_CACHE:-}" ]; then
  work="$BLENDER_CACHE"
  mkdir -p "$work"
else
  work="$(mktemp -d)"
  trap 'rm -r -f -- "$work"' EXIT
fi

if [ -f "${work}/${ARCHIVE}" ] && echo "${SHA256}  ${work}/${ARCHIVE}" | sha256sum -c - >/dev/null 2>&1; then
  echo "blender: using the cached archive"
else
  echo "blender: fetching $VERSION (about 360 MB)"
  curl -fsSL --retry 3 --retry-delay 5 -o "${work}/${ARCHIVE}" "$URL"
fi

# The point of the exercise. An archive that does not match is not unpacked: this runs as root in
# CI and extracts a few gigabytes of executables, so "probably the right file" is not good enough.
echo "${SHA256}  ${work}/${ARCHIVE}" | sha256sum -c - >/dev/null
echo "blender: checksum verified"

mkdir -p "$PREFIX"
tar -xf "${work}/${ARCHIVE}" -C "$PREFIX" --strip-components=1

installed="$("$BLENDER" --version | head -1)"
echo "blender: installed $installed at $PREFIX"
