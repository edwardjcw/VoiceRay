# Articulatory model (sagittal vocal tract)

> Pedagogical pose library for the layered SVG rig. Source art: [`assets/vocal-tract/reference.png`](../assets/vocal-tract/reference.png). Runtime SVG: [`client/public/vocal-tract.svg`](../client/public/vocal-tract.svg). Backend map: `src/VoiceRay.Core/PoseMap.fs`.

VoiceRay does **not** simulate biomechanics. Each IPA segment maps to a **discrete target pose** (transform + optional tongue path `d`) so learners see where articulators move for common American English phones.

## SVG layer legend

Layers are top-level `<g id="...">` groups in `vocal-tract.svg`. Paint order (back → front): cavity → palate → outline → jaw → teeth → lips → tongue → velum → glottis hint.

| Layer ID | Animates | Role |
| -------- | -------- | ---- |
| `cavity` | No | Oral/nasal/pharyngeal airspace (black fill) |
| `palate` | No | Hard palate landmark |
| `outline` | No | Skull, nose profile, neck frame |
| `jaw` | Yes | Mandible open/close; moves lower teeth group |
| `teeth_upper` | With jaw | Upper dental row (landmark) |
| `teeth_lower` | With jaw | Lower dental row |
| `lips_upper` | Yes | Lip spreading, rounding, closure |
| `lips_lower` | Yes | Lower lip + jaw coupling |
| `tongue` | Yes | Tip/blade/root via path `d` morph |
| `velum` | Yes | Nasal vs oral (translate down ≈ nasal coupling) |
| `glottis_hint` | Optional | Simple voiced/voiceless hint (low MVP usage) |

**Neutral pose:** empty layer overrides — traced reference resting articulation (`SagittalPlayer.NEUTRAL_POSE`).

**Highlights:** backend `highlight` string array (e.g. `tongue_tip`, `alveolar`) drives UI emphasis; not separate SVG layers.

## JSON pose shape

Matches `VoiceRay.Core.LayerPose` / API keyframes:

```json
{
  "ipa": "t",
  "startMs": 120,
  "endMs": 180,
  "layers": {
    "lips_upper": { "transform": "translate(0,0)" },
    "tongue": { "d": "M 46 96 Q ... Z" }
  },
  "highlight": ["tongue_tip", "alveolar"]
}
```

- `transform` — SVG/CSS transform on the layer group.
- `d` — replacement path data for `tongue` (primary morph target).

Frontend test poses in `client/src/animation/SagittalPlayer.js` (`TEST_POSES`) mirror backend presets for local verification.

## IPA pose families (`en-US`)

`PoseMap.fs` groups phones into shared templates:

| Template | IPA symbols | Articulatory focus | Highlight tags |
| -------- | ----------- | ------------------ | -------------- |
| Bilabial stop | `p`, `b` | Lip compression | `bilabial` |
| Alveolar stop | `t`, `d` | Tongue tip at alveolar ridge | `tongue_tip`, `alveolar` |
| Velar stop | `k`, `ɡ`, `g` | Tongue body at velum | `velar` |
| Open vowel | `æ`, `ɑ` | Jaw open, tongue low | (vowel) |
| High front vowel | `ɛ`, `ɪ` | Spread lips, front tongue | (vowel) |
| Rounded back vowel | `ʊ`, `ɔ` | Lip rounding, back tongue | (vowel) |
| Interdental | `θ` | Tongue between teeth | `interdental` |
| Post-alveolar | `ʃ` | Tongue blade retracted | `post_alveolar` |
| Rhotic | `ɹ` | Bunched/retroflex approximant | `rhotic` |
| Nasal velar | `ŋ` | Velum lowered, back tongue | `velar`, `nasal` |

Unknown IPA for a segment still emits a keyframe with **empty** `layers` (no morph) — timeline remains visible.

## Demo word set (`en-US`)

Canonical list: [`status.md`](status.md) **Demo word set**. G2P stub (`G2pStub.fs`) IPA breakdown:

| Word | IPA (display) | Teaching focus |
| ---- | ------------- | -------------- |
| pat | pæt | Voiceless bilabial stop + æ |
| pet | pɛt | Voicing + ɛ |
| pit | pɪt | High front ɪ |
| pot | pɑt | Back rounded ɑ |
| put | pʊt | High back ʊ |
| cat | kæt | Velar stop k |
| dog | dɔɡ | Voiced velar ɡ |
| think | θɪŋ | Interdental θ |
| red | ɹɛd | Rhotic /ɹ/ |
| ship | ʃɪp | Post-alveolar ʃ |

### Validation checklist

After pose or SVG changes:

1. `POST /api/v1/reference` with each demo word — keyframes non-empty for consonants/vowels with poses.
2. `npm run dev` — scrub reference audio; lips/tongue/velum move on the sagittal diagram.
3. Compare ghost overlay (W7) — reference keyframes at reduced opacity on user replay.

## Compare overlay semantics

- **Reference track:** green-tinted ghost keyframes from `/reference`.
- **User track:** amber keyframes from `/analyze`.
- Same rig IDs — no second SVG asset.

Coaching `highlightLayers` names should match layer IDs where possible (`lips_upper`, `tongue`, `velum`).

## Extending locales (Phase 4)

1. Add locale pack: lexicon + IPA inventory.
2. Copy `en-US` pose templates; adjust vowel/consonant targets per locale phonology.
3. Register map branch in `PoseMap.tryGetPose` (or locale loader).
4. Document new phones in this file (pose legend table).

See locale matrix in [`architecture.md`](architecture.md).
