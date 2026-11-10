#!/usr/bin/env python3
"""Split the original single-file architecture blueprint into clean per-phase
reference files and extract every embedded code draft to reference/drafts/.

Usage: python3 tools/split_blueprint.py <original.md> <repo_root>

The original export has four inline base64 PNGs, markdown-escape noise
(\\+ \\# \\_ ...), empty list-item artefacts, and one phase (5) that is
backslash-escaped end to end. This script normalises all of that. It never
edits meaning: prose and code are carried over verbatim apart from escapes.
"""
import base64, json, os, re, sys

src, root = sys.argv[1], sys.argv[2]
lines = open(src, encoding="utf-8").read().split("\n")

# 1. pull out inline images ---------------------------------------------------
img_dir = os.path.join(root, "docs/blueprint/images")
os.makedirs(img_dir, exist_ok=True)
kept = []
for ln in lines:
    m = re.match(r"\[(image\d+)\]: <data:image/png;base64,([^>]+)>", ln)
    if m:
        open(os.path.join(img_dir, m.group(1) + ".png"), "wb").write(base64.b64decode(m.group(2)))
        continue
    kept.append(ln)
lines = kept

ESC = re.compile(r"\\([\\`*_{}\[\]()#+\-.!<>=|~&$\"'^:;,/@%?])")
ESC_LIGHT = re.compile(r"\\([_#+\-.!<>=~&\[\]()$])")   # keep \| and \* outside phase 5

def phase_of(i):
    cur = None
    for start, name in bounds:
        if i >= start:
            cur = name
    return cur

heads = [(i, re.search(r"Phase (\d+):", l)) for i, l in enumerate(lines) if l.startswith("### **Phase ")]
heads = [(i, int(m.group(1))) for i, m in heads if m]
state_i = next(i for i, l in enumerate(lines) if l.startswith("## **Session Continuity and Project State Record"))
cited_i = next(i for i, l in enumerate(lines) if "Works cited" in l)
prog_i = next(i for i, l in enumerate(lines) if l.startswith("## **The 12-Phase Full-Stack Build Program"))

bounds = [(0, "00-executive-blueprint"), (prog_i, "00b-build-program-intro")]
for i, n in heads:
    bounds.append((i, "phase-%02d" % n))
bounds.append((state_i, "90-original-state-record"))
bounds.append((cited_i, "91-works-cited"))
bounds.sort()

p5 = next(i for i, n in heads if n == 5)
p6 = next(i for i, n in heads if n == 6)

# 2. normalise ----------------------------------------------------------------
out, in_code = [], False
for i, ln in enumerate(lines):
    if p5 < i < p6:
        ln = ESC.sub(r"\1", ln).rstrip()          # fully escaped phase
        if ln.startswith("```"):
            in_code = not in_code
    else:
        if ln.startswith("```"):
            in_code = not in_code
        elif not in_code:
            ln = ESC_LIGHT.sub(r"\1", ln)
    if not in_code:
        if re.fullmatch(r"\s*(\d+\.|\*|-)\s*", ln):   # empty list-item artefacts
            ln = None
        elif ln.startswith("![]["):
            m = re.match(r"!\[\]\[(image\d+)\]", ln)
            ln = "![%s](images/%s.png)" % (m.group(1), m.group(1))
    out.append(ln)
    if i == p6 - 1:
        in_code = False

# 3. write per-section files --------------------------------------------------
bp = os.path.join(root, "docs/blueprint")
index = []
for k, (start, name) in enumerate(bounds):
    end = bounds[k + 1][0] if k + 1 < len(bounds) else len(out)
    body = [l for l in out[start:end] if l is not None]
    text = re.sub(r"\n{3,}", "\n\n", "\n".join(body)).strip() + "\n"
    open(os.path.join(bp, name + ".md"), "w", encoding="utf-8").write(text)
    index.append((name, len(text.split("\n")), len(text)))

# 4. extract code drafts ------------------------------------------------------
drafts = os.path.join(root, "reference/drafts")
manifest = []
for k, (start, name) in enumerate(bounds):
    end = bounds[k + 1][0] if k + 1 < len(bounds) else len(out)
    seg = [l for l in out[start:end] if l is not None]
    j = 0
    while j < len(seg):
        m = re.search(r"File Path[^:]*:\*\*\s*`?([A-Za-z0-9_./-]+)`?", seg[j])
        if m:
            path = m.group(1)
            a = next((x for x in range(j + 1, min(j + 12, len(seg))) if seg[x].startswith("```")), None)
            if a is not None:
                b = next(x for x in range(a + 1, len(seg)) if seg[x].startswith("```"))
                code = "\n".join(seg[a + 1:b]).strip("\n") + "\n"
                dest = os.path.join(drafts, path)
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                open(dest, "w", encoding="utf-8").write(code)
                manifest.append({"phase": name, "path": path, "lines": code.count("\n")})
                j = b
        j += 1
json.dump(manifest, open(os.path.join(drafts, "MANIFEST.json"), "w"), indent=1)

for name, nl, nb in index:
    print("%-28s %6d lines %8d bytes" % (name, nl, nb))
print("drafts:", len(manifest), "files,", sum(m["lines"] for m in manifest), "lines")
