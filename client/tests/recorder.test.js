import { describe, it, beforeEach, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { startRecording } from '../src/audio/recorder.js'

describe('startRecording', () => {
  /** @type {typeof globalThis} */
  let g

  beforeEach(() => {
    g = globalThis
    g.window = { __VOICERAY_E2E__: true }
  })

  afterEach(() => {
    delete g.window
  })

  it('returns a WAV blob from the e2e stub without Promise generic syntax errors', async () => {
    const session = await startRecording()
    assert.equal(typeof session.stop, 'function')
    const blob = await session.blob
    assert.ok(blob instanceof Blob)
    assert.ok(blob.size > 44)
  })
})
