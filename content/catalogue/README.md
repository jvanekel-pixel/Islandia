# The catalogue

Every placeable thing in Islandia is an entry in a YAML file in this directory. **The catalogue is
the source of truth** (ADR-11): the C# ids, the licence ledger, the validator's budgets and the
inventory browser all derive from it. Nothing is placeable because a prefab exists; it is placeable
because it is in here and passes the validator.

One file per category, named after it. A file holds a single `items:` list.

## Why it works this way

The blueprint this project replaces shipped a catalogue of 631 items that resolved to about six
distinct geometries: 95 architecture modules all pointed at the same 2 × 3 × 0.25 m box, and 75
props at one lantern box. The count was real and the content was not, and nothing in the build
could tell the difference.

So the counting here is deliberately hard to fake. An item's mesh is identified by the **checksum of
the asset file**, not by its catalogue entry, and the report counts distinct meshes and palette
variants separately. Two entries sharing a mesh are one mesh and two variants, and saying otherwise
is not possible — not discouraged, not reviewed for, not possible.

## An entry

```yaml
items:
  - id: tree.oak.broad          # canonical, permanent — see "Ids" below
    name: Broad Oak             # what a person sees
    kind: tree                  # sub-type within the category; selects the budget
    asset: assets/foliage/tree.oak.broad.glb
    facts: facts/foliage/tree.oak.broad.json
    thumbnail: thumbnails/foliage/tree.oak.broad.png
    collider: box               # box | capsule | mesh | none
    source:
      kind: pack                # pack | generated
      pack: quaternius-ultimate-nature
      url: https://quaternius.com/...
      licence: CC0-1.0
      licence_file: licences/quaternius-ultimate-nature.txt
      attribution: Quaternius   # required by some licences; see licences.yaml
    palette:
      set: broadleaf
      variants: [spring, summer, autumn]
    sockets: []                 # named attachment points; architecture uses these
```

Paths are relative to `content/`. Every field above is required except `palette`, `sockets` and the
`source` fields a licence does not demand; `category` is not a field, because it is the file the
entry lives in — one place for it to be wrong instead of two.

## Ids

`id` is lowercase `a-z0-9`, dot-separated, read coarse to fine: `tree.oak.broad`, not
`broad_oak_tree`. It is **permanent**. Saved islands store the numeric id derived from it, so
renaming an id orphans whatever anyone has already built with it. Renaming is therefore a migration,
not an edit.

The numeric id is `CRC-32` of the id's UTF-8 bytes, which gives three properties the sequential
integers in the blueprint did not have:

- **Stable across additions.** Inserting an item alphabetically before another does not renumber
  anything. The blueprint's 398 sequential ids all shifted whenever the catalogue grew, which would
  have silently rewritten every saved island.
- **Derivable in both directions of the toolchain.** The generator computes them in Python and
  `Island.Core` computes them in C#; both are plain CRC-32, and a test asserts the two agree.
- **Checkable.** A collision, or an id hashing to zero (which means "nothing"), fails the build
  rather than producing two items that are the same item.

`make content` writes `Packages/com.island.core/Runtime/Content/CatalogueIds.g.cs` from the
catalogue. It is generated, committed, and CI fails if it is stale — so runtime code can use
`CatalogueIds.TreeOakBroad` without the catalogue and the code ever drifting apart.

## Adding something

1. Put the entry in the right category file, alphabetically by id.
2. `make content` — regenerates the ids.
3. `make content-check` — schema, budgets, licences, geometry, and the honest count.

Both run in CI. Neither needs Unity.
