/**
 * Layered sagittal vocal-tract player.
 *
 * The backend emits per-phoneme {@link ArticulatoryPose} feature vectors
 * (normalized 0..1 articulator parameters). This player converts those features
 * into SVG geometry for the rig in `client/public/vocal-tract.svg` and smoothly
 * interpolates between phonemes for natural coarticulation.
 */

/** SVG group ids present in the rig (animated + landmark groups for highlights). */
export const LAYER_IDS = [
  'outline',
  'palate',
  'pharynx',
  'velum',
  'jaw',
  'teeth_upper',
  'teeth_lower',
  'tongue',
  'tongue_tip',
  'lips_upper',
  'lips_lower',
  'glottis_hint',
]

/**
 * @typedef {Object} ArticulatoryPose
 * @property {number} jawOpen        0 closed .. 1 open
 * @property {number} tongueHeight   0 low .. 1 high
 * @property {number} tongueBackness 0 front .. 1 back
 * @property {number} tongueTip      0 neutral .. 1 raised to alveolar ridge
 * @property {number} interdental    0 .. 1 tip protruded between teeth
 * @property {number} lipRounding    0 spread .. 1 rounded
 * @property {number} lipClosure     0 open .. 1 sealed
 * @property {number} velum          0 raised/oral .. 1 lowered/nasal
 */

/** Resting articulation — mirrors `PoseMap.neutral` on the backend. */
export const NEUTRAL_POSE = /** @type {ArticulatoryPose} */ ({
  jawOpen: 0.35,
  tongueHeight: 0.45,
  tongueBackness: 0.45,
  tongueTip: 0,
  interdental: 0,
  lipRounding: 0,
  lipClosure: 0,
  velum: 0,
})

/** Coarticulation ramp (ms) used to ease from the previous phoneme's pose. */
const RAMP_MS = 70

/** Pedagogical reference poses for local dev / unit checks. */
export const TEST_POSES = /** @type {Record<string, ArticulatoryPose>} */ ({
  'high-front-vowel': { ...NEUTRAL_POSE, tongueHeight: 0.95, tongueBackness: 0.15, jawOpen: 0.2 },
  'open-vowel': { ...NEUTRAL_POSE, tongueHeight: 0.1, tongueBackness: 0.9, jawOpen: 0.9 },
  'rounded-back': { ...NEUTRAL_POSE, tongueHeight: 0.9, tongueBackness: 0.9, lipRounding: 0.9, jawOpen: 0.2 },
  'alveolar-stop': { ...NEUTRAL_POSE, tongueTip: 1, tongueHeight: 0.55, tongueBackness: 0.2, jawOpen: 0.18 },
  'bilabial-closure': { ...NEUTRAL_POSE, lipClosure: 1, jawOpen: 0.1, tongueHeight: 0.4 },
})

const lerp = (a, b, t) => a + (b - a) * t
const clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v)

/**
 * @param {ArticulatoryPose} a
 * @param {ArticulatoryPose} b
 * @param {number} t
 * @returns {ArticulatoryPose}
 */
export function lerpPose(a, b, t) {
  const out = /** @type {ArticulatoryPose} */ ({})
  for (const key of Object.keys(NEUTRAL_POSE)) {
    out[key] = lerp(a[key] ?? NEUTRAL_POSE[key], b[key] ?? NEUTRAL_POSE[key], t)
  }
  return out
}

// ---- Geometry generators (rig coordinate space matches vocal-tract.svg) ----

/** Tongue body is the traced reference path translated by articulation. */
export function tongueTransform(p) {
  const tx = (p.tongueBackness - 0.45) * 46 // back → toward pharynx (+x)
  const ty = -(p.tongueHeight - 0.45) * 78 + (p.jawOpen - 0.35) * 46 // high → up; open jaw → down
  return `translate(${tx.toFixed(2)} ${ty.toFixed(2)})`
}

/** Coronal tip lobe rising from the tongue front toward the alveolar ridge. */
export function tongueTipPath(p) {
  const t = Math.max(p.tongueTip, p.interdental)
  if (t < 0.02) return ''
  const baseX = 92
  const baseY = 200
  const ridgeX = 95 - p.interdental * 24 // interdental pushes the tip past the teeth
  const ridgeY = lerp(200, 178, t) + p.interdental * 6
  const w = 14
  return `M ${baseX - w} ${baseY} Q ${ridgeX - 6} ${ridgeY - 1} ${ridgeX} ${ridgeY} Q ${ridgeX + 9} ${ridgeY + 3} ${baseX + w} ${baseY} Q ${baseX} ${baseY + 9} ${baseX - w} ${baseY} Z`
}

/** Soft palate: raised seals the nasal port, lowered swings toward the tongue. */
export function velumPath(p) {
  const hingeX = 288
  const hingeY = 150
  const drop = p.velum
  const tipX = lerp(304, 280, drop)
  const tipY = lerp(196, 250, drop)
  const midX = lerp(302, 294, drop)
  const midY = lerp(172, 208, drop)
  return `M ${hingeX} ${hingeY} Q ${midX + 8} ${midY} ${tipX} ${tipY} Q ${midX} ${midY + 6} ${hingeX - 8} ${hingeY + 8} Z`
}

function lipEllipse(cx, cy, rx, ry) {
  return `M ${cx - rx} ${cy} C ${cx - rx} ${cy - ry}, ${cx + rx} ${cy - ry}, ${cx + rx} ${cy} C ${cx + rx} ${cy + ry}, ${cx - rx} ${cy + ry}, ${cx - rx} ${cy} Z`
}

export function lipsUpperPath(p) {
  const round = p.lipRounding
  const close = p.lipClosure
  const cx = 58 - round * 8 // rounding protrudes forward
  const cy = lerp(157, 163, close) // closed lip drops to meet
  const rx = 15 - round * 5 // rounding narrows the aperture
  const ry = 7 + round * 3
  return lipEllipse(cx, cy, rx, ry)
}

export function lipsLowerPath(p) {
  const round = p.lipRounding
  const close = p.lipClosure
  const jaw = p.jawOpen
  const cx = 59 - round * 8
  const cy = lerp(175, 167, close) + jaw * 7
  const rx = 15 - round * 5
  const ry = 8 + round * 3
  return lipEllipse(cx, cy, rx, ry)
}

/**
 * Resolve the interpolated pose for a time, easing in from the previous phoneme.
 * @param {Array<{ pose?: ArticulatoryPose; startMs?: number; endMs?: number }>} keyframes
 * @param {number} timeMs
 * @returns {ArticulatoryPose}
 */
export function poseAtTime(keyframes, timeMs) {
  if (!keyframes?.length) return { ...NEUTRAL_POSE }
  let idx = 0
  for (let i = 0; i < keyframes.length; i += 1) {
    const start = keyframes[i].startMs ?? 0
    if (timeMs >= start) idx = i
  }
  const cur = keyframes[idx].pose ?? NEUTRAL_POSE
  const prev = idx > 0 ? keyframes[idx - 1].pose ?? NEUTRAL_POSE : NEUTRAL_POSE
  const start = keyframes[idx].startMs ?? 0
  const ramp = clamp01((timeMs - start) / RAMP_MS)
  return lerpPose(prev, cur, ramp)
}

export class SagittalPlayer {
  /** @param {SVGElement | null} root */
  constructor(root) {
    this.root = root
    /** @type {ArticulatoryPose} */
    this.currentPose = { ...NEUTRAL_POSE }
    if (root) this.applyPose(NEUTRAL_POSE)
  }

  /** @returns {Element | null} */
  getLayer(id) {
    if (!this.root) return null
    return this.root.querySelector(`#${id}`) ?? null
  }

  /** @param {string} id @param {string} d */
  _setPath(id, d) {
    const layer = this.getLayer(id)
    const path = layer?.querySelector('path')
    if (path) path.setAttribute('d', d)
  }

  /** @param {string} id @param {string} transform */
  _setTransform(id, transform) {
    const layer = this.getLayer(id)
    if (layer) layer.setAttribute('transform', transform)
  }

  resetToNeutral() {
    this.applyPose(NEUTRAL_POSE)
  }

  /**
   * @param {string} name
   * @returns {boolean}
   */
  applyTestPose(name) {
    const pose = TEST_POSES[name]
    if (!pose) return false
    this.applyPose(pose)
    return true
  }

  /**
   * Render a full articulatory pose onto the rig.
   * @param {ArticulatoryPose} pose
   */
  applyPose(pose) {
    if (!this.root) return
    const p = { ...NEUTRAL_POSE, ...pose }
    const transform = tongueTransform(p)
    this._setTransform('tongue', transform)
    this._setTransform('tongue_tip', transform)
    this._setPath('tongue_tip', tongueTipPath(p))
    this._setPath('velum', velumPath(p))
    this._setPath('lips_upper', lipsUpperPath(p))
    this._setPath('lips_lower', lipsLowerPath(p))
    this.currentPose = p
  }

  /**
   * @param {Array<{ pose?: ArticulatoryPose; startMs?: number; endMs?: number }>} keyframes
   * @param {number} [timeMs]
   */
  playKeyframes(keyframes, timeMs = 0) {
    if (!this.root || !keyframes?.length) return
    this.applyPose(poseAtTime(keyframes, timeMs))
  }

  /**
   * Render a pose as a ghost reference overlay (styled via CSS).
   * @param {ArticulatoryPose | null | undefined} pose
   */
  applyGhostPose(pose) {
    if (!this.root) return
    const ghostClass = 'voiceray-ghost-layer'
    for (const id of LAYER_IDS) {
      this.getLayer(id)?.classList.add(ghostClass)
    }
    if (pose) this.applyPose(pose)
  }
}

/**
 * Load vocal-tract.svg into a container and return a player instance.
 * @param {HTMLElement} container
 * @returns {Promise<SagittalPlayer>}
 */
export async function mountSagittalPlayer(container) {
  const res = await fetch('/vocal-tract.svg')
  if (!res.ok) throw new Error(`Failed to load vocal-tract.svg (${res.status})`)
  const text = await res.text()
  container.innerHTML = text
  const svg = container.querySelector('svg')
  return new SagittalPlayer(svg instanceof SVGElement ? svg : null)
}

/**
 * Mount reference (ghost) and user sagittal players for compare overlay.
 * @param {HTMLElement} container
 * @returns {Promise<{ ghost: SagittalPlayer; user: SagittalPlayer }>}
 */
export async function mountComparePlayers(container) {
  const res = await fetch('/vocal-tract.svg')
  if (!res.ok) throw new Error(`Failed to load vocal-tract.svg (${res.status})`)
  const text = await res.text()
  container.innerHTML = `
    <div class="sagittal-stack" data-testid="ghost-overlay">
      <div class="sagittal-ghost" aria-hidden="true"></div>
      <div class="sagittal-user"></div>
    </div>
  `
  const ghostHost = container.querySelector('.sagittal-ghost')
  const userHost = container.querySelector('.sagittal-user')
  if (!ghostHost || !userHost) throw new Error('Compare mount failed')
  ghostHost.innerHTML = text
  userHost.innerHTML = text
  const ghostSvg = ghostHost.querySelector('svg')
  const userSvg = userHost.querySelector('svg')
  return {
    ghost: new SagittalPlayer(ghostSvg instanceof SVGElement ? ghostSvg : null),
    user: new SagittalPlayer(userSvg instanceof SVGElement ? userSvg : null),
  }
}
