#!/usr/bin/env python3
"""Measure the owner's coaster pack and write the track facts `RC-01` builds on.

Reads Kenney's Coaster Kit (CC0) from a directory the owner's archive was unpacked into, and writes
`content/facts/rides/coaster-kit.json`: for each track shape, the frame at each open end, a
centreline sampled from those frames, and how far the six styles' geometry reaches from it. The
pack itself is never committed. Run it again when the pack changes; `RC-01b`'s data checks and
`coaster_pieces.py` read only the facts file.

How the ends are found (DECISIONS.md, 2026-11-12, "The coaster pieces come from the pack"): the
rails' cross-section at the open end of `coaster-steel-straight` is a ring of 24 vertices. Every
other piece is searched for that ring under each heading, pitch and bank the kit could use
(quarter turns of heading, eighth turns of pitch and bank), and a match counts as an open end
only when the track within a unit of it lies wholly behind it (an exit) or wholly in front (an
entry). The reference point of a frame is the underside of the spine, which every piece rolls about, so
banked ends stay on the grid.

The centreline between the ends is a line, a cubic Bézier (Kenney's curves are Béziers with
handles half the forward run long, which the spine's vertices show), a bump or a loop, chosen per
shape below. It is checked rather than trusted: the facts record, for every style, the furthest any
vertex lies from it (`reach`), and a wrong curve shows up as a reach much larger than a
straight's.

Usage (run untrusted input with an isolated interpreter):
    python3 -I tools/content/measure_coaster_kit.py <unpacked-pack-dir>
"""

from __future__ import annotations

import json
import math
import struct
import sys
import zlib
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
FACTS = REPO / "content/facts/rides/coaster-kit.json"

SUB = 64  # sub-units per pack unit: every frame and sample is an integer in these
STYLES = ("steel", "wood", "mouse", "hanging", "monorail", "flume")
PIVOT = (0.0, -1.0, 0.0)  # the spine's underside, in model coordinates, at a model's origin

# shape -> (model stem, centreline form). The stem is the model name after `coaster-<style>-`;
# `track` is the one-unit straight that a station is made of.
SHAPES = {
    "straight": ("straight", "line"),
    "straight-short": ("track", "line"),
    "corner-small": ("corner-small", "bezier"),
    "corner-large": ("corner-large", "bezier"),
    "corner-small-ramp": ("corner-small-ramp", "bezier"),
    "corner-large-ramp": ("corner-large-ramp", "bezier"),
    "curve": ("curve", "bezier"),
    "hill": ("straight-hill-complete", "bezier"),
    "hill-half": ("straight-hill-complete-half", "bezier"),
    "hill-beginning": ("straight-hill-beginning", "bezier"),
    "hill-end": ("straight-hill-end", "bezier"),
    "bump-up": ("straight-bump-up", "bump"),
    "bump-down": ("straight-bump-down", "bump"),
    "skew-left": ("straight-skew-left", "line"),
    "skew-right": ("straight-skew-right", "line"),
    "skew-left-side": ("straight-skew-left-side", "line"),
    "skew-right-side": ("straight-skew-right-side", "line"),
    "looping": ("looping", "loop"),
}

# ---------------------------------------------------------------------------------------- reading


def read_glb(path: Path) -> tuple[list[tuple[float, float, float]], int]:
    """World-space vertex positions, converted to Unity's handedness (x negated), and CRC-32."""
    data = path.read_bytes()
    _, _, length = struct.unpack_from("<4sII", data, 0)
    offset, doc, blob = 12, None, b""
    while offset < length:
        size, kind = struct.unpack_from("<II", data, offset)
        chunk = data[offset + 8: offset + 8 + size]
        offset += 8 + size
        if kind == 0x4E4F534A:
            doc = json.loads(chunk)
        else:
            blob = chunk
    out: list[tuple[float, float, float]] = []

    def walk(index: int, parent: list[list[float]]) -> None:
        node = doc["nodes"][index]
        m = _mul(parent, _local(node))
        if "mesh" in node:
            for prim in doc["meshes"][node["mesh"]]["primitives"]:
                acc = doc["accessors"][prim["attributes"]["POSITION"]]
                view = doc["bufferViews"][acc["bufferView"]]
                base = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
                stride = view.get("byteStride", 12)
                for i in range(acc["count"]):
                    p = struct.unpack_from("<fff", blob, base + i * stride)
                    w = [sum(m[r][c] * p[c] for c in range(3)) + m[r][3] for r in range(3)]
                    out.append((-w[0], w[1], w[2]))
        for child in node.get("children", []):
            walk(child, m)

    for root in doc["scenes"][doc.get("scene", 0)]["nodes"]:
        walk(root, [[1.0 if r == c else 0.0 for c in range(4)] for r in range(4)])
    return out, zlib.crc32(data) & 0xFFFFFFFF


def _local(node: dict) -> list[list[float]]:
    if "matrix" in node:
        m = node["matrix"]
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    t = node.get("translation", [0, 0, 0])
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    s = node.get("scale", [1, 1, 1])
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[i][j] * s[j] for j in range(3)] + [t[i]] for i in range(3)] + [[0, 0, 0, 1]]


def _mul(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


# ------------------------------------------------------------------------------------- the frames


def rotation(heading: int, pitch: int, bank: int) -> list[list[float]]:
    """Columns right, up, forward for heading in quarter turns, pitch and bank in eighth turns.

    The same convention as `TrackFrame` in Island.Core: heading turns right (+z towards +x),
    pitch raises the nose, bank lowers the right rail.
    """
    h, p, b = heading * math.pi / 2, pitch * math.pi / 4, bank * math.pi / 4
    fwd = (math.sin(h) * math.cos(p), math.sin(p), math.cos(h) * math.cos(p))
    level_right = (math.cos(h), 0.0, -math.sin(h))
    level_up = _cross(fwd, level_right)
    up = tuple(level_up[i] * math.cos(b) + level_right[i] * math.sin(b) for i in range(3))
    right = _cross(up, fwd)
    return [[right[r], up[r], fwd[r]] for r in range(3)]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _apply(m, v):
    return tuple(sum(m[r][c] * v[c] for c in range(3)) for r in range(3))


def find_ends(verts, ring, tol=3e-3):
    """Every pose of `ring` that lands on vertices and sits at an open end of the spine."""
    grid: dict[tuple[int, int, int], list] = {}
    for v in verts:
        grid.setdefault(tuple(int(round(c * 50)) for c in v), []).append(v)

    def present(p) -> bool:
        k = [int(round(c * 50)) for c in p]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for v in grid.get((k[0] + dx, k[1] + dy, k[2] + dz), ()):
                        if all(abs(v[i] - p[i]) < tol for i in range(3)):
                            return True
        return False

    ends = []
    for heading in range(4):
        for pitch in range(-2, 3):
            for bank in range(-3, 5):
                m = rotation(heading, pitch, bank)
                placed = [_apply(m, q) for q in ring]
                for v in verts:
                    t = tuple(v[i] - placed[0][i] for i in range(3))
                    if not all(present(tuple(q[i] + t[i] for i in range(3))) for q in placed[1:]):
                        continue
                    fwd = (m[0][2], m[1][2], m[2][2])
                    up = (m[0][1], m[1][1], m[2][1])
                    # The track around the end, in the end's own frame: up to a unit along it and
                    # half a unit across, which takes in the rails but not a neighbouring track
                    # (the loop's two legs pass 0.65 apart). At an open end it is all on one side.
                    along = []
                    for w in verts:
                        rel = tuple(w[i] - t[i] for i in range(3))
                        x, y, z = (sum(m[r][c] * rel[r] for r in range(3)) for c in range(3))
                        if abs(x) < 0.5 and -0.1 < y < 0.5 and abs(z) < 1.0:
                            along.append(z)
                    kind = ("exit" if max(along) < 0.02 and min(along) < -0.05 else
                            "entry" if min(along) > -0.02 and max(along) > 0.05 else None)
                    if kind:
                        ends.append((kind, tuple(round(c * SUB) for c in t), heading, pitch, bank))
    return sorted(set(ends))


def _dist(a, b) -> float:
    return math.sqrt(sum((a[i] - b[i]) ** 2 for i in range(3)))


# ---------------------------------------------------------------------------------- the centreline


def centreline(form: str, d, heading: int, p0: int, p1: int, rise: float = 0.0, n: int = 240):
    """Points from (0,0,0) to `d` (units, entry-relative), entry facing +z at pitch p0."""
    pts = []
    for i in range(n + 1):
        s = i / n
        if form == "line":
            pts.append(tuple(d[k] * s for k in range(3)))
        elif form == "bezier":
            # Kenney's curves are cubic Béziers. A level end's handle runs half the forward
            # distance (so a quarter turn of radius r bulges 2.7% inside a circle); a 45° end's
            # handle is one unit. Both fitted to the spine's vertices, and checked by `reach`.
            c0 = _along(heading=0, pitch=p0, length=1.0 if p0 else d[2] / 2)
            c1 = tuple(d[k] - v for k, v in enumerate(_along(heading, p1, 1.0 if p1 else d[2] / 2)))
            u = 1 - s
            pts.append(tuple(3 * u * u * s * c0[k] + 3 * u * s * s * c1[k] + s ** 3 * d[k]
                             for k in range(3)))
        elif form == "bump":
            pts.append((0.0, rise * math.sin(math.pi * s) ** 2, d[2] * s))
        elif form == "loop":
            a = 2 * math.pi * s
            pts.append((d[0] * s, 2 - 2 * math.cos(a), 2 * math.sin(a)))
    return pts


def _along(heading: int, pitch: int, length: float):
    m = rotation(heading, pitch, 0)
    return (m[0][2] * length, m[1][2] * length, m[2][2] * length)


def reach(verts, pivot, line) -> float:
    """The furthest any vertex lies from the centreline, in units."""
    worst = 0.0
    for v in verts:
        local = tuple(v[i] - pivot[i] for i in range(3))
        best = min(_seg(local, line[i], line[i + 1]) for i in range(len(line) - 1))
        worst = max(worst, best)
    return worst


def _seg(p, a, b) -> float:
    ab = tuple(b[i] - a[i] for i in range(3))
    denom = sum(c * c for c in ab) or 1.0
    t = max(0.0, min(1.0, sum((p[i] - a[i]) * ab[i] for i in range(3)) / denom))
    return _dist(p, tuple(a[i] + ab[i] * t for i in range(3)))


def resample(line, spacing: float = 0.5):
    """Evenly spaced points no further apart than `spacing`, rounded to sub-units, and length."""
    cumulative = [0.0]
    for i in range(1, len(line)):
        cumulative.append(cumulative[-1] + _dist(line[i - 1], line[i]))
    total = cumulative[-1]
    count = max(1, math.ceil(total / spacing - 1e-9))
    out, j = [], 0
    for k in range(count + 1):
        target = total * k / count
        while j < len(line) - 2 and cumulative[j + 1] < target:
            j += 1
        span = cumulative[j + 1] - cumulative[j] or 1.0
        f = (target - cumulative[j]) / span
        out.append([round((line[j][i] + (line[j + 1][i] - line[j][i]) * f) * SUB) for i in range(3)])
    return out, round(total * SUB)


# --------------------------------------------------------------------------------------- the facts


def measure(pack: Path) -> dict:
    models = pack / "Models" / "GLB format"
    ref, _ = read_glb(models / "coaster-steel-straight.glb")
    ring = sorted({(v[0], v[1] - PIVOT[1], 0.0) for v in ref if abs(v[2]) < 1e-4})
    shapes = {}
    for name, (stem, form) in SHAPES.items():
        verts, _ = read_glb(models / f"coaster-steel-{stem}.glb")
        ends = find_ends(verts, ring)
        # The rails are symmetric, so each end also matches facing backwards, where it reads as
        # the other kind of end. Every model's entry faces +z; its exit is the other end.
        entries = [e for e in ends if e[0] == "entry" and e[2] == 0]
        exits = [e for e in ends if e[0] == "exit" and entries and e[1] != entries[0][1]]
        if len(entries) != 1 or len(exits) != 1:
            raise SystemExit(f"{stem}: expected one entry and one exit, found {ends}")
        _, a, h0, p0, b0 = entries[0]
        _, z, h1, p1, b1 = exits[0]
        d = tuple((z[i] - a[i]) / SUB for i in range(3))
        # A bump rises or dips by however far its rails reach above or below a straight's.
        rise = 0.0
        if form == "bump":
            top, bottom = max(v[1] for v in verts), min(v[1] for v in verts)
            straight_top, straight_bottom = max(v[1] for v in ref), min(v[1] for v in ref)
            rise = round((top - straight_top if top - straight_top > 0.05 else bottom - straight_bottom) * SUB) / SUB
        line = centreline(form, d, h1, p0, p1, rise)
        entry_pivot = tuple(c / SUB for c in a)
        samples, length = resample(line)
        if samples[-1] != [z[i] - a[i] for i in range(3)]:
            raise SystemExit(f"{stem}: the centreline does not end at the exit")
        upright = [i for i, _ in enumerate(samples)
                   if form != "loop" or math.cos(2 * math.pi * i / (len(samples) - 1)) >= math.sqrt(0.5) - 1e-9]
        crcs, worst = {}, 0.0
        for style in STYLES:
            path = models / f"coaster-{style}-{stem}.glb"
            sv, crc = read_glb(path)
            crcs[style] = f"{crc:08x}"
            worst = max(worst, reach(sv, entry_pivot, line))
        shapes[name] = {
            "model": stem,
            "form": form,
            "entry": {"at": list(a), "pitch": p0, "bank": b0},
            "exit": {"at": list(z), "heading": h1, "pitch": p1, "bank": b1},
            "length": length,
            "samples": samples,
            "supports": upright,
            "reach": round(worst * SUB),
            "crc32": crcs,
        }
        print(f"{name:18s} exit {d} heading {h1} pitch {p0}->{p1} bank {b0}->{b1} "
              f"reach {worst:.3f} samples {len(samples)}")
    return {
        "pack": "kenney-coaster-kit",
        "pack_version": "1.0",
        "licence": "CC0-1.0",
        "sub_units_per_unit": SUB,
        "pivot_below_model_origin": round(-PIVOT[1] * SUB),
        "styles": list(STYLES),
        "shapes": shapes,
    }


def main(argv: list[str]) -> int:
    if len(argv) != 1:
        print(__doc__)
        return 2
    facts = measure(Path(argv[0]))
    FACTS.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(facts, indent=1, sort_keys=False)
    FACTS.write_text(_compact(text) + "\n", encoding="utf-8")
    print(f"wrote {FACTS.relative_to(REPO)}")
    return 0


def _compact(text: str) -> str:
    """Keeps each [x, y, z] on one line so the file reads as a table."""
    import re
    return re.sub(r"\[\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\s*\]", r"[\1, \2, \3]", text)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
