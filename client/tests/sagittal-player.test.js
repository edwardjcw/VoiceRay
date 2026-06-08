import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import {
  SagittalPlayer,
  NEUTRAL_POSE,
  TEST_POSES,
  LAYER_IDS,
  poseAtTime,
  lerpPose,
  tongueTransform,
} from '../src/animation/SagittalPlayer.js'

const __dirname = dirname(fileURLToPath(import.meta.url))
const svgPath = join(__dirname, '..', 'public', 'vocal-tract.svg')

const REQUIRED_LAYER_IDS = [
  'outline',
  'palate',
  'pharynx',
  'velum',
  'tongue',
  'tongue_tip',
  'lips_upper',
  'lips_lower',
  'jaw',
  'teeth_upper',
  'teeth_lower',
]

/** Minimal SVG-group mock with a single <path> child per group. */
function makeMockSvgRoot() {
  const layers = new Map()
  for (const id of LAYER_IDS) {
    const pathEl = {
      attrs: { d: 'M 0 0' },
      getAttribute(name) {
        return this.attrs[name] ?? null
      },
      setAttribute(name, value) {
        this.attrs[name] = value
      },
    }
    const group = {
      attrs: {},
      classList: { _s: new Set(), add(c) { this._s.add(c) }, remove(c) { this._s.delete(c) }, contains(c) { return this._s.has(c) } },
      getAttribute(name) {
        return this.attrs[name] ?? null
      },
      setAttribute(name, value) {
        this.attrs[name] = value
      },
      removeAttribute(name) {
        delete this.attrs[name]
      },
      querySelector(sel) {
        return sel === 'path' ? pathEl : null
      },
    }
    layers.set(id, group)
  }
  return {
    querySelector(sel) {
      const id = sel.startsWith('#') ? sel.slice(1) : sel
      return layers.get(id) ?? null
    },
  }
}

describe('vocal-tract.svg', () => {
  it('defines the required animated + landmark layers', () => {
    const markup = readFileSync(svgPath, 'utf8')
    for (const id of REQUIRED_LAYER_IDS) {
      assert.match(markup, new RegExp(`id="${id}"`), `missing #${id}`)
    }
    assert.match(markup, /viewBox="-12 64 372 452"/)
    // Rig is real geometry now — no static reference raster.
    assert.doesNotMatch(markup, /<image/)
  })
})

describe('SagittalPlayer pose model', () => {
  it('neutral pose exposes the full articulatory parameter set', () => {
    const keys = Object.keys(NEUTRAL_POSE).sort()
    assert.deepEqual(keys, [
      'interdental',
      'jawOpen',
      'lipClosure',
      'lipRounding',
      'tongueBackness',
      'tongueHeight',
      'tongueTip',
      'velum',
    ])
    assert.equal(Object.keys(TEST_POSES).length, 5)
  })

  it('tongueTransform moves up+front for high-front and down+back for low-back', () => {
    const front = tongueTransform({ ...NEUTRAL_POSE, tongueHeight: 0.95, tongueBackness: 0.15, jawOpen: 0.2 })
    const back = tongueTransform({ ...NEUTRAL_POSE, tongueHeight: 0.1, tongueBackness: 0.9, jawOpen: 0.9 })
    const [fx, fy] = front.match(/-?\d+\.?\d*/g).map(Number)
    const [bx, by] = back.match(/-?\d+\.?\d*/g).map(Number)
    assert.ok(fx < 0, 'high-front tongue shifts toward the front (−x)')
    assert.ok(fy < 0, 'high tongue shifts up (−y)')
    assert.ok(bx > 0, 'back tongue shifts toward the pharynx (+x)')
    assert.ok(by > 0, 'low + open jaw shifts the tongue down (+y)')
  })

  it('applyPose updates tongue transform and renders lip closure geometry', () => {
    const root = makeMockSvgRoot()
    const player = new SagittalPlayer(root)
    const neutralTransform = player.getLayer('tongue').getAttribute('transform')

    player.applyPose(TEST_POSES['high-front-vowel'])
    assert.notEqual(player.getLayer('tongue').getAttribute('transform'), neutralTransform)

    player.applyPose(TEST_POSES['bilabial-closure'])
    const upper = player.getLayer('lips_upper').querySelector('path').getAttribute('d')
    const lower = player.getLayer('lips_lower').querySelector('path').getAttribute('d')
    assert.ok(upper.length > 0 && lower.length > 0, 'lips render closure geometry')
  })

  it('applyTestPose returns false for unknown poses', () => {
    const player = new SagittalPlayer(makeMockSvgRoot())
    assert.equal(player.applyTestPose('does-not-exist'), false)
    assert.equal(player.applyTestPose('open-vowel'), true)
  })
})

describe('pose interpolation', () => {
  const keyframes = [
    { ipa: 'p', startMs: 0, endMs: 100, pose: TEST_POSES['bilabial-closure'] },
    { ipa: 'a', startMs: 100, endMs: 250, pose: TEST_POSES['open-vowel'] },
  ]

  it('poseAtTime selects the active segment', () => {
    const p = poseAtTime(keyframes, 200)
    // Well past the ramp → fully the open vowel.
    assert.equal(p.jawOpen, TEST_POSES['open-vowel'].jawOpen)
  })

  it('poseAtTime eases in from the previous phoneme at a boundary', () => {
    const atStart = poseAtTime(keyframes, 100)
    // At the boundary the pose still reflects the previous (closed) phoneme.
    assert.ok(atStart.jawOpen < TEST_POSES['open-vowel'].jawOpen)
  })

  it('lerpPose blends every parameter', () => {
    const mid = lerpPose(NEUTRAL_POSE, TEST_POSES['open-vowel'], 0.5)
    assert.equal(mid.jawOpen, (NEUTRAL_POSE.jawOpen + TEST_POSES['open-vowel'].jawOpen) / 2)
  })
})
