# The twelve

Anthropomorphic dog avatars, all playable, with clothing that swaps between them like a doll's.
Architecture and the trade that forces: `../plan/02-ARCHITECTURE.md` ADR-24.

Grouped by build, because build is what decides which garment set an avatar wears. Breed identity
is carried by head, ears, tail, fur pattern and colour — not by a wholly different body, which is
what makes shared clothing possible at all.

Each has three states. They are *saved outfits*, not modes: any garment can be worn with any
other, and these are simply the combinations worth having a button for.

## Lean

| | Breed | Look | Character |
| :-- | :-- | :-- | :-- |
| **Sable** | Border Collie | Black and white, white blaze, one ear that never quite stands up | Attentive, playful, mildly bossy |
| **Vesper** | Doberman | Black-and-tan, long clean lines | Dry, precise, quietly amused by you |
| **Dune** | Pharaoh Hound | Warm tan, amber eyes, enormous upright ears that catch the light | Sun-warm, watchful, deadpan |
| **Rook** | Dalmatian | Stark white, bold spots, athletic | Sharp-witted, a shameless flirt |

- **Sable** — *dressed:* charcoal waistcoat over rolled shirtsleeves · *easy:* just the shirt, open · *less:* swim shorts, towel round the neck
- **Vesper** — *dressed:* dark tailored suit, no tie · *easy:* jacket gone, shirt half-undone · *less:* fitted shorts
- **Dune** — *dressed:* loose desert robe and sash · *easy:* robe open, sash low · *less:* a linen wrap
- **Rook** — *dressed:* fitted racing jacket, slim trousers · *easy:* jacket unzipped over nothing · *less:* running shorts

## Broad

| | Breed | Look | Character |
| :-- | :-- | :-- | :-- |
| **Marlow** | Golden Retriever | Honey-gold, broad chest, permanently pleased | Sunny, affectionate, a bit of a goof |
| **Ridge** | Husky | Grey-and-white, thick ruff, pale blue eyes | Confident, loud laugh, warm hands |
| **Bodhi** | Newfoundland | Huge, shaggy, black as a wet rock | Calm, enveloping, impossible to fluster |

- **Marlow** — *dressed:* linen shirt open at the collar, braces · *easy:* shirt untucked and hanging open · *less:* linen trousers rolled to the knee
- **Ridge** — *dressed:* heavy cable knit, cuffed trousers · *easy:* knit off, sleeveless underneath · *less:* trunks, fur still wet
- **Bodhi** — *dressed:* fisherman's jumper under an oilskin · *easy:* jumper alone, sleeves shoved up · *less:* a towel, having just hauled himself out of the sea

## Tall

| | Breed | Look | Character |
| :-- | :-- | :-- | :-- |
| **Atlas** | Great Dane | Blue-grey, long-limbed, has to duck through doorways | Gentle, slow-spoken, immovable |
| **Beau** | Afghan Hound | Cream silk coat that moves like water | Glamorous, theatrical, languid |
| **Saffi** | Saluki | Pale gold, very lean, feathered ears, slightly unreal | Quiet, observant, elegant |

- **Atlas** — *dressed:* oversized cardigan, soft trousers · *easy:* cardigan open over bare chest · *less:* lounge shorts
- **Beau** — *dressed:* long open coat, loose silk trousers · *easy:* the coat and nothing under it · *less:* draped silk, nothing structured
- **Saffi** — *dressed:* layered gauze, long vertical lines · *easy:* one layer · *less:* a minimal drape

## Compact

| | Breed | Look | Character |
| :-- | :-- | :-- | :-- |
| **Juno** | Shiba Inu | Red-gold and cream, curled tail, permanently smug | Particular about everything, secretly soft |
| **Pip** | Corgi | Red-and-white, round, low to the ground | Bouncy, mischievous, no sense of personal space |

- **Juno** — *dressed:* short kimono-style wrap, wide belt · *easy:* wrap loosened · *less:* the belt and shorts
- **Pip** — *dressed:* dungarees over a striped tee · *easy:* one strap down, tee gone · *less:* swim shorts and an inflatable ring

## The one that will fight us

**Pip.** A compact, low-slung body sharing a garment set with Atlas is asking a great deal of a
few blend shapes. Decided when it is built rather than guessed at now: either Pip becomes a
scale-and-proportion variant wearing the lean set with some stretching, or the compact build gets
its own garment set and the clothing work goes up by a third. Both are acceptable. `AV-02` makes
the call from how it actually looks.

## Still open

- Voice, or none at all.
- Whether the avatar is visible to the player when they look down, or only in a mirror. Looking
  down at a body that does not match where your hands are is a known way to make people
  uncomfortable, so this is a comfort question before it is an art one.
- Whether any of the twelve should be able to wear another's signature piece as a joke. Probably.
