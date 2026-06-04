/**
 * Layered sagittal vocal-tract player (Phase 1).
 * Applies discrete poses to groups in client/public/vocal-tract.svg.
 */

/** @type {readonly string[]} */
export const LAYER_IDS = [
  'outline',
  'palate',
  'jaw',
  'teeth_upper',
  'teeth_lower',
  'lips_upper',
  'lips_lower',
  'tongue',
  'velum',
  'glottis_hint',
]

/**
 * @typedef {Object} LayerPose
 * @property {string} [transform]
 * @property {string} [d]
 */

/**
 * @typedef {Record<string, LayerPose>} ArticulatoryPose
 */

/** Neutral — matches traced reference resting articulation. */
export const NEUTRAL_POSE = /** @type {ArticulatoryPose} */ ({})

/** Five pedagogical test poses for local dev / unit checks. */
export const TEST_POSES = /** @type {Record<string, ArticulatoryPose>} */ ({
  'high-front-vowel': {
    lips_upper: { transform: 'translate(0,-2) scale(1.02,0.92)' },
    lips_lower: { transform: 'translate(0,-1) scale(1.05,0.9)' },
    jaw: { transform: 'translate(0,2) rotate(-2 80 160)' },
    tongue: {
      d: 'M 52 98 Q 72 72 102 74 Q 128 78 138 96 Q 142 112 128 124 Q 108 132 82 128 Q 58 122 52 98 Z',
    },
    velum: { transform: 'translate(0,0)' },
  },
  'open-vowel': {
    lips_upper: { transform: 'translate(0,1)' },
    lips_lower: { transform: 'translate(0,6) scale(1,1.08)' },
    jaw: { transform: 'translate(0,10) rotate(6 80 160)' },
    teeth_lower: { transform: 'translate(0,8)' },
    tongue: {
      d: 'M 44 118 Q 58 128 88 132 Q 118 134 132 128 Q 140 120 136 108 Q 128 98 98 96 Q 68 96 52 104 Q 44 110 44 118 Z',
    },
    velum: { transform: 'translate(0,2)' },
  },
  'rounded-back': {
    lips_upper: { transform: 'translate(-4,2) scale(0.92,1.12)' },
    lips_lower: { transform: 'translate(-6,4) scale(0.9,1.15)' },
    jaw: { transform: 'translate(-2,4)' },
    tongue: {
      d: 'M 56 112 Q 78 108 108 114 Q 132 122 140 136 Q 144 150 128 158 Q 102 164 72 158 Q 54 150 52 136 Q 52 122 56 112 Z',
    },
    velum: { transform: 'translate(2,0)' },
  },
  'alveolar-stop': {
    lips_upper: { transform: 'translate(0,0)' },
    lips_lower: { transform: 'translate(0,2)' },
    jaw: { transform: 'translate(0,3)' },
    tongue: {
      d: 'M 46 96 Q 58 82 78 78 Q 92 76 98 88 Q 100 96 92 102 Q 78 108 62 106 Q 50 102 46 96 Z',
    },
    velum: { transform: 'translate(0,0)' },
  },
  'bilabial-closure': {
    lips_upper: { transform: 'translate(0,4) scale(1,0.85)' },
    lips_lower: { transform: 'translate(0,-2) scale(1,0.88)' },
    jaw: { transform: 'translate(0,2)' },
    tongue: {
      d: 'M 48 108 Q 62 100 88 102 Q 118 106 132 118 Q 142 128 138 142 Q 132 156 108 162 Q 82 166 62 158 Q 48 150 44 136 Q 42 122 48 108 Z',
    },
    velum: { transform: 'translate(0,0)' },
  },
})

export class SagittalPlayer {
  /**
   * @param {SVGElement | null} root
   */
  constructor(root) {
    this.root = root
    /** @type {ArticulatoryPose} */
    this.currentPose = { ...NEUTRAL_POSE }
    /** @type {Map<string, { transform: string | null; d: string | null }>} */
    this._defaults = new Map()
    if (root) {
      this._captureDefaults()
      this.resetToNeutral()
    }
  }

  _captureDefaults() {
    for (const id of LAYER_IDS) {
      const layer = this.getLayer(id)
      if (!layer) continue
      const path = layer.querySelector('path')
      this._defaults.set(id, {
        transform: layer.getAttribute('transform'),
        d: path?.getAttribute('d') ?? null,
      })
    }
  }

  /** @returns {Element | null} */
  getLayer(id) {
    if (!this.root) return null
    const el = this.root.querySelector(`#${id}`)
    return el ?? null
  }

  resetToNeutral() {
    this.applyPose(NEUTRAL_POSE, { restoreDefaults: true })
    this.currentPose = { ...NEUTRAL_POSE }
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
   * @param {ArticulatoryPose} pose
   * @param {{ restoreDefaults?: boolean }} [options]
   */
  applyPose(pose, options = {}) {
    if (!this.root) return

    const restore = options.restoreDefaults === true
    for (const id of LAYER_IDS) {
      const layer = this.getLayer(id)
      if (!layer) continue
      const path = layer.querySelector('path')
      const defaults = this._defaults.get(id)

      if (restore && defaults) {
        if (defaults.transform) layer.setAttribute('transform', defaults.transform)
        else layer.removeAttribute('transform')
        if (path && defaults.d) path.setAttribute('d', defaults.d)
        continue
      }

      const layerPose = pose[id]
      if (!layerPose) continue

      if (layerPose.transform) {
        layer.setAttribute('transform', layerPose.transform)
      }

      if (layerPose.d && path) {
        path.setAttribute('d', layerPose.d)
      }
    }

    this.currentPose = { ...pose }
  }

  /**
   * @param {Array<{ layers?: ArticulatoryPose; startMs?: number; endMs?: number }>} keyframes
   * @param {number} [timeMs]
   */
  playKeyframes(keyframes, timeMs = 0) {
    if (!this.root || !keyframes?.length) return

    let active = keyframes[0]
    for (const frame of keyframes) {
      const start = frame.startMs ?? 0
      const end = frame.endMs ?? Number.POSITIVE_INFINITY
      if (timeMs >= start && timeMs < end) {
        active = frame
        break
      }
      if (timeMs >= end) active = frame
    }

    if (active?.layers) {
      this.applyPose(active.layers)
    }
  }

  /**
   * Mark layers as ghost reference overlay (styling via CSS).
   * @param {ArticulatoryPose | null | undefined} pose
   */
  applyGhostPose(pose) {
    if (!this.root) return
    const ghostClass = 'voiceray-ghost-layer'
    for (const id of LAYER_IDS) {
      const layer = this.getLayer(id)
      if (!layer) continue
      layer.classList.remove(ghostClass)
      if (pose?.[id]) layer.classList.add(ghostClass)
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
