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
} from '../src/animation/SagittalPlayer.js'

const __dirname = dirname(fileURLToPath(import.meta.url))
const svgPath = join(__dirname, '..', 'public', 'vocal-tract.svg')

const REQUIRED_LAYER_IDS = [
  'outline',
  'teeth_upper',
  'teeth_lower',
  'lips_upper',
  'lips_lower',
  'jaw',
  'tongue',
  'velum',
  'palate',
]

/** @param {Record<string, { transform?: string; d?: string }>} shapes */
function makeMockSvgRoot(shapes) {
  const layers = new Map()
  for (const id of LAYER_IDS) {
    const spec = shapes[id] ?? { d: 'M 0 0' }
    const pathEl = {
      attrs: { d: spec.d ?? 'M 0 0' },
      getAttribute(name) {
        return this.attrs[name] ?? null
      },
      setAttribute(name, value) {
        this.attrs[name] = value
      },
    }
    const group = {
      attrs: {},
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
  it('defines required animate layers per plan', () => {
    const markup = readFileSync(svgPath, 'utf8')
    for (const id of REQUIRED_LAYER_IDS) {
      assert.match(markup, new RegExp(`id="${id}"`), `missing #${id}`)
    }
    assert.match(markup, /viewBox="0 0 217 232"/)
  })
})

describe('SagittalPlayer', () => {
  it('exposes neutral pose and five test poses', () => {
    assert.deepEqual(NEUTRAL_POSE, {})
    assert.equal(Object.keys(TEST_POSES).length, 5)
  })

  it('applies and restores test poses', () => {
    const root = makeMockSvgRoot({
      tongue: { d: 'M 1 1' },
    })
    const player = new SagittalPlayer(root)
    const tongue = player.getLayer('tongue')
    const path = tongue?.querySelector('path')
    const defaultD = path?.getAttribute('d')

    assert.ok(player.applyTestPose('open-vowel'))
    assert.notEqual(path?.getAttribute('d'), defaultD)

    player.resetToNeutral()
    assert.equal(path?.getAttribute('d'), defaultD)
  })

  it('playKeyframes selects segment by timeMs', () => {
    const root = makeMockSvgRoot({
      tongue: { d: 'M 0 0' },
    })
    const player = new SagittalPlayer(root)
    const keyframes = [
      {
        startMs: 0,
        endMs: 100,
        layers: TEST_POSES['high-front-vowel'],
      },
      {
        startMs: 100,
        endMs: 200,
        layers: TEST_POSES['open-vowel'],
      },
    ]
    player.playKeyframes(keyframes, 150)
    const path = player.getLayer('tongue')?.querySelector('path')
    assert.equal(path?.getAttribute('d'), TEST_POSES['open-vowel'].tongue.d)
  })

  it('lists animation layer ids used by the player', () => {
    assert.ok(LAYER_IDS.includes('tongue'))
    assert.ok(LAYER_IDS.includes('velum'))
  })
})
