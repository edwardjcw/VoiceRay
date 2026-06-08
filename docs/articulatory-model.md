# Articulatory model (sagittal vocal tract)

> Pedagogical pose library for the layered SVG rig. Source art: [`assets/vocal-tract/reference.svg`](../assets/vocal-tract/reference.svg) (vector trace; replaces the old raster). Runtime SVG: [`client/public/vocal-tract.svg`](../client/public/vocal-tract.svg). Backend map: `src/VoiceRay.Core/PoseMap.fs`. Frontend rig: `client/src/animation/SagittalPlayer.js`.

VoiceRay does **not** simulate biomechanics. Each IPA segment maps to a **normalized articulatory pose** — a small set of `0..1` parameters (jaw, tongue height/backness, tip, lip rounding/closure, velum) grounded in the IPA vowel chart and consonant place/manner. The **backend emits phonetics** (the pose parameters); the **frontend renders geometry** (`SagittalPlayer` converts parameters into tongue/lip/velum SVG paths and tweens between phonemes). This decoupling keeps the phonetics testable and lets the rig art evolve independently.

The static anatomy (head outline, hard palate, pharyngeal wall) is traced from `reference.svg`. The **tongue** is the real traced tongue path animated by a transform; the **tongue tip**, **velum**, and **lips** are generated procedurally from the pose.

## SVG layer legend

Layers are top-level `<g id="...">` groups in `vocal-tract.svg`. Paint order (back → front): outline → palate → pharynx → velum → jaw → teeth → tongue → tongue_tip → lips → glottis hint.

| Layer ID | Animates | Driven by | Role |
| -------- | -------- | --------- | ---- |
| `outline` | No | — | Skull, nose profile, neck frame (traced) |
| `palate` | No | — | Hard palate landmark (traced) |
| `pharynx` | No | — | Pharyngeal wall (traced) |
| `tongue` | Yes | `jawOpen`, `tongueHeight`, `tongueBackness` | Tongue body via `transform` on the traced tongue path |
| `tongue_tip` | Yes | `tongueTip`, `interdental` | Procedural tip raise / interdental protrusion |
| `lips_upper` | Yes | `lipRounding`, `lipClosure` | Upper lip rounding / closure (procedural) |
| `lips_lower` | Yes | `lipRounding`, `lipClosure`, `jawOpen` | Lower lip + jaw coupling (procedural) |
| `velum` | Yes | `velum` | Soft palate raised (oral) vs lowered (nasal) (procedural) |
| `jaw` / `teeth_upper` / `teeth_lower` / `glottis_hint` | Highlight only | `highlight` | Landmark groups for UI emphasis |

**Neutral pose:** `SagittalPlayer.NEUTRAL_POSE` (and backend `PoseMap.neutral`) — resting articulation rendered when no segment is active.

**Highlights:** backend `highlight` string array (e.g. `tongue_tip`, `alveolar`) drives UI emphasis; not separate animated paths.

## JSON pose shape

Each keyframe carries an `ArticulatoryPose` (normalized `0..1` parameters) — see [`api.md`](api.md#articulatorypose) for the full field reference:

```json
{
  "ipa": "t",
  "startMs": 120,
  "endMs": 180,
  "pose": {
    "jawOpen": 0.25,
    "tongueHeight": 0.55,
    "tongueBackness": 0.35,
    "tongueTip": 1.0,
    "interdental": 0.0,
    "lipRounding": 0.0,
    "lipClosure": 0.0,
    "velum": 0.0
  },
  "highlight": ["tongue_tip", "alveolar"]
}
```

The frontend `SagittalPlayer` interpolates poses across time (`poseAtTime` / `lerpPose`) so articulators tween smoothly between phonemes (a simple coarticulation model). `NEUTRAL_POSE` and `TEST_POSES` in `SagittalPlayer.js` mirror backend presets for local verification.

## IPA pose families (`en-US`)

`PoseMap.fs` maps phones to poses using these key parameters:

| Template | IPA symbols | Key parameters | Highlight tags |
| -------- | ----------- | -------------- | -------------- |
| Bilabial stop | `p`, `b` | `lipClosure≈1` | `bilabial` |
| Alveolar stop | `t`, `d` | `tongueTip≈1` | `tongue_tip`, `alveolar` |
| Velar stop | `k`, `ɡ`, `g` | high `tongueHeight` + `tongueBackness` | `velar` |
| Open vowel | `æ`, `ɑ` | high `jawOpen`, low `tongueHeight` | (vowel) |
| High front vowel | `ɛ`, `ɪ` | high `tongueHeight`, low `tongueBackness` | (vowel) |
| Rounded back vowel | `ʊ`, `ɔ` | `lipRounding>0`, high `tongueBackness` | (vowel) |
| Interdental | `θ` | `interdental≈1` | `interdental` |
| Post-alveolar | `ʃ` | mid `tongueTip`, blade retracted | `post_alveolar` |
| Rhotic | `ɹ` | bunched mid tongue, slight `lipRounding` | `rhotic` |
| Nasal velar | `ŋ` | `velum≈1`, high `tongueBackness` | `velar`, `nasal` |

Unknown IPA for a segment still emits a keyframe whose `pose` is `neutral` (no movement) — the timeline stays visible.

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
