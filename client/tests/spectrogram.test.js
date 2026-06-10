import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { fftRadix2, computeSpectrogram } from '../src/audio/spectrogram.js'

describe('fftRadix2', () => {
  it('rejects non-power-of-two lengths', () => {
    assert.throws(() => fftRadix2(new Float32Array(6), new Float32Array(6)))
  })

  it('puts a pure cosine into a single frequency bin', () => {
    const n = 8
    const re = new Float32Array(n)
    const im = new Float32Array(n)
    // cos(2π·1·t/n) → energy at bins 1 and n-1.
    for (let i = 0; i < n; i++) {
      re[i] = Math.cos((2 * Math.PI * i) / n)
    }
    fftRadix2(re, im)
    const mag = (b) => Math.hypot(re[b], im[b])
    assert.ok(mag(1) > 1, 'bin 1 should carry the cosine energy')
    assert.ok(mag(2) < 1e-6, 'bin 2 should be ~zero')
    assert.ok(mag(3) < 1e-6, 'bin 3 should be ~zero')
  })
})

describe('computeSpectrogram', () => {
  it('returns no frames when the signal is shorter than the window', () => {
    const spec = computeSpectrogram(new Float32Array(100), { fftSize: 512 })
    assert.equal(spec.frames, 0)
    assert.equal(spec.bins, 256)
  })

  it('peaks at the bin matching a pure tone', () => {
    const sampleRate = 16000
    const fftSize = 512
    const freq = 1000
    const samples = new Float32Array(sampleRate) // 1 second
    for (let i = 0; i < samples.length; i++) {
      samples[i] = Math.sin((2 * Math.PI * freq * i) / sampleRate)
    }

    const spec = computeSpectrogram(samples, { fftSize })
    assert.ok(spec.frames > 0)

    // Inspect a middle frame; find the loudest bin.
    const frame = Math.floor(spec.frames / 2)
    let peakBin = 0
    let peakDb = -Infinity
    for (let b = 0; b < spec.bins; b++) {
      const db = spec.magnitudes[frame * spec.bins + b]
      if (db > peakDb) {
        peakDb = db
        peakBin = b
      }
    }

    const expectedBin = Math.round(freq / (sampleRate / fftSize)) // 1000 / 31.25 = 32
    assert.ok(
      Math.abs(peakBin - expectedBin) <= 1,
      `peak bin ${peakBin} should be near ${expectedBin}`,
    )
  })
})
