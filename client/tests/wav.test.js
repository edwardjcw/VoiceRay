import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { encodeWav16Mono, resampleMono } from '../src/audio/wav.js'

describe('wav encoder', () => {
  it('encodes 16-bit mono WAV header', () => {
    const samples = new Float32Array([0, 0.5, -0.5])
    const blob = encodeWav16Mono(samples, 16000)
    assert.equal(blob.type, 'audio/wav')
    assert.ok(blob.size > 44)
  })

  it('resampleMono returns same length when rates match', () => {
    const input = new Float32Array([1, 2, 3])
    const out = resampleMono(input, 16000, 16000)
    assert.equal(out.length, input.length)
  })
})
