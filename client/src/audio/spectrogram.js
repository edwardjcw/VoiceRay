/**
 * Lightweight STFT spectrogram for the compare view. Decodes audio with the Web Audio
 * API, runs a windowed radix-2 FFT over the mono signal, and paints a log-magnitude
 * heatmap onto a canvas. No external dependencies.
 */

/**
 * In-place iterative radix-2 Cooley–Tukey FFT.
 * @param {Float32Array} re real parts (length must be a power of two)
 * @param {Float32Array} im imaginary parts (same length, typically zeros)
 */
export function fftRadix2(re, im) {
  const n = re.length
  if (n <= 1) return
  if ((n & (n - 1)) !== 0) {
    throw new Error(`FFT length must be a power of two, got ${n}`)
  }

  // Bit-reversal permutation.
  for (let i = 1, j = 0; i < n; i++) {
    let bit = n >> 1
    for (; j & bit; bit >>= 1) {
      j ^= bit
    }
    j ^= bit
    if (i < j) {
      const tr = re[i]
      re[i] = re[j]
      re[j] = tr
      const ti = im[i]
      im[i] = im[j]
      im[j] = ti
    }
  }

  for (let len = 2; len <= n; len <<= 1) {
    const ang = (-2 * Math.PI) / len
    const wlenRe = Math.cos(ang)
    const wlenIm = Math.sin(ang)
    for (let i = 0; i < n; i += len) {
      let wRe = 1
      let wIm = 0
      for (let k = 0; k < len / 2; k++) {
        const uRe = re[i + k]
        const uIm = im[i + k]
        const vRe = re[i + k + len / 2] * wRe - im[i + k + len / 2] * wIm
        const vIm = re[i + k + len / 2] * wIm + im[i + k + len / 2] * wRe
        re[i + k] = uRe + vRe
        im[i + k] = uIm + vIm
        re[i + k + len / 2] = uRe - vRe
        im[i + k + len / 2] = uIm - vIm
        const nextWRe = wRe * wlenRe - wIm * wlenIm
        wIm = wRe * wlenIm + wIm * wlenRe
        wRe = nextWRe
      }
    }
  }
}

/**
 * Computes a magnitude spectrogram from a mono signal.
 * @param {Float32Array} samples mono PCM in [-1, 1]
 * @param {object} [opts]
 * @param {number} [opts.fftSize] power-of-two window size (default 512)
 * @param {number} [opts.hop] hop size in samples (default fftSize/4)
 * @returns {{ frames: number; bins: number; magnitudes: Float32Array; maxDb: number; minDb: number }}
 *   `magnitudes` is row-major [frame * bins + bin] in decibels.
 */
export function computeSpectrogram(samples, opts = {}) {
  const fftSize = opts.fftSize ?? 512
  const hop = opts.hop ?? Math.floor(fftSize / 4)
  const bins = fftSize / 2
  const n = samples.length

  if (n < fftSize) {
    return { frames: 0, bins, magnitudes: new Float32Array(0), maxDb: 0, minDb: -100 }
  }

  const frames = 1 + Math.floor((n - fftSize) / hop)
  const magnitudes = new Float32Array(frames * bins)

  // Precompute a Hann window.
  const window = new Float32Array(fftSize)
  for (let i = 0; i < fftSize; i++) {
    window[i] = 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (fftSize - 1))
  }

  const re = new Float32Array(fftSize)
  const im = new Float32Array(fftSize)
  let maxDb = -Infinity
  let minDb = Infinity

  for (let f = 0; f < frames; f++) {
    const start = f * hop
    for (let i = 0; i < fftSize; i++) {
      re[i] = samples[start + i] * window[i]
      im[i] = 0
    }
    fftRadix2(re, im)
    for (let b = 0; b < bins; b++) {
      const mag = Math.sqrt(re[b] * re[b] + im[b] * im[b]) / fftSize
      const db = 20 * Math.log10(mag + 1e-9)
      magnitudes[f * bins + b] = db
      if (db > maxDb) maxDb = db
      if (db < minDb) minDb = db
    }
  }

  if (!Number.isFinite(maxDb)) maxDb = 0
  if (!Number.isFinite(minDb)) minDb = -100

  return { frames, bins, magnitudes, maxDb, minDb }
}

/**
 * Maps a normalized intensity [0,1] to an RGB "viridis-ish" color.
 * @param {number} t
 * @returns {[number, number, number]}
 */
function intensityToColor(t) {
  const x = Math.max(0, Math.min(1, t))
  // Simple dark-blue → cyan → yellow ramp.
  const r = Math.round(255 * Math.max(0, Math.min(1, 1.6 * x - 0.5)))
  const g = Math.round(255 * Math.max(0, Math.min(1, 1.4 * x)))
  const b = Math.round(255 * Math.max(0, Math.min(1, 1.2 - 1.6 * x)))
  return [r, g, b]
}

/**
 * Renders a spectrogram onto a canvas (time on X, frequency on Y, low freq at bottom).
 * @param {HTMLCanvasElement} canvas
 * @param {{ frames: number; bins: number; magnitudes: Float32Array; maxDb: number; minDb: number }} spec
 * @param {object} [opts]
 * @param {number} [opts.maxBinFraction] fraction of bins to display (e.g. 0.5 for ~half Nyquist)
 * @param {number} [opts.dynamicRangeDb] dB range below the peak to map (default 70)
 */
export function drawSpectrogram(canvas, spec, opts = {}) {
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const width = canvas.width
  const height = canvas.height
  ctx.clearRect(0, 0, width, height)

  if (!spec || spec.frames === 0 || spec.bins === 0) {
    ctx.fillStyle = '#10131a'
    ctx.fillRect(0, 0, width, height)
    ctx.fillStyle = '#8a93a6'
    ctx.font = '12px sans-serif'
    ctx.fillText('No audio to render', 8, height / 2)
    return
  }

  const maxBinFraction = opts.maxBinFraction ?? 0.6
  const dynamicRange = opts.dynamicRangeDb ?? 70
  const displayBins = Math.max(1, Math.floor(spec.bins * maxBinFraction))
  const floorDb = spec.maxDb - dynamicRange

  const image = ctx.createImageData(width, height)
  const data = image.data

  for (let px = 0; px < width; px++) {
    const f = Math.min(spec.frames - 1, Math.floor((px / width) * spec.frames))
    for (let py = 0; py < height; py++) {
      // Bottom of canvas = low frequency.
      const binF = (1 - py / height) * displayBins
      const b = Math.min(displayBins - 1, Math.floor(binF))
      const db = spec.magnitudes[f * spec.bins + b]
      const t = (db - floorDb) / dynamicRange
      const [r, g, bl] = intensityToColor(t)
      const idx = (py * width + px) * 4
      data[idx] = r
      data[idx + 1] = g
      data[idx + 2] = bl
      data[idx + 3] = 255
    }
  }

  ctx.putImageData(image, 0, 0)
}

let sharedAudioContext = null

/** @returns {AudioContext | null} */
function getAudioContext() {
  if (sharedAudioContext) return sharedAudioContext
  const Ctor = globalThis.AudioContext || globalThis.webkitAudioContext
  if (!Ctor) return null
  sharedAudioContext = new Ctor()
  return sharedAudioContext
}

/**
 * Decodes an audio URL (or data URI) into a mono Float32Array.
 * @param {string} src
 * @returns {Promise<{ samples: Float32Array; sampleRate: number }>}
 */
export async function decodeAudioToMono(src) {
  const ctx = getAudioContext()
  if (!ctx) throw new Error('Web Audio API is unavailable in this environment')

  const response = await fetch(src)
  if (!response.ok) throw new Error(`Could not fetch audio (${response.status})`)
  const arrayBuffer = await response.arrayBuffer()
  const audioBuffer = await ctx.decodeAudioData(arrayBuffer)

  const channels = audioBuffer.numberOfChannels
  const length = audioBuffer.length
  const mono = new Float32Array(length)
  for (let c = 0; c < channels; c++) {
    const ch = audioBuffer.getChannelData(c)
    for (let i = 0; i < length; i++) {
      mono[i] += ch[i] / channels
    }
  }
  return { samples: mono, sampleRate: audioBuffer.sampleRate }
}

/**
 * Decodes an audio source and renders its spectrogram into the canvas.
 * @param {HTMLCanvasElement} canvas
 * @param {string} src
 * @param {object} [opts] forwarded to {@link drawSpectrogram}
 */
export async function renderSpectrogramFromSrc(canvas, src, opts = {}) {
  const { samples } = await decodeAudioToMono(src)
  const spec = computeSpectrogram(samples, { fftSize: opts.fftSize ?? 512 })
  drawSpectrogram(canvas, spec, opts)
  return spec
}
