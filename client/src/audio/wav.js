/**
 * Encode mono 16-bit PCM WAV.
 * @param {Float32Array} samples - normalized -1..1
 * @param {number} sampleRate
 * @returns {Blob}
 */
export function encodeWav16Mono(samples, sampleRate = 16000) {
  const numChannels = 1
  const bitsPerSample = 16
  const blockAlign = (numChannels * bitsPerSample) / 8
  const byteRate = sampleRate * blockAlign
  const dataSize = samples.length * 2
  const buffer = new ArrayBuffer(44 + dataSize)
  const view = new DataView(buffer)

  const writeString = (offset, str) => {
    for (let i = 0; i < str.length; i++) {
      view.setUint8(offset + i, str.charCodeAt(i))
    }
  }

  writeString(0, 'RIFF')
  view.setUint32(4, 36 + dataSize, true)
  writeString(8, 'WAVE')
  writeString(12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, numChannels, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, byteRate, true)
  view.setUint16(32, blockAlign, true)
  view.setUint16(34, bitsPerSample, true)
  writeString(36, 'data')
  view.setUint32(40, dataSize, true)

  let offset = 44
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true)
    offset += 2
  }

  return new Blob([buffer], { type: 'audio/wav' })
}

/**
 * Resample mono float buffer to target rate (linear).
 * @param {Float32Array} input
 * @param {number} inputRate
 * @param {number} targetRate
 * @returns {Float32Array}
 */
export function resampleMono(input, inputRate, targetRate) {
  if (inputRate === targetRate) return input
  const ratio = inputRate / targetRate
  const outLength = Math.max(1, Math.round(input.length / ratio))
  const output = new Float32Array(outLength)
  for (let i = 0; i < outLength; i++) {
    const src = i * ratio
    const idx = Math.floor(src)
    const frac = src - idx
    const a = input[idx] ?? 0
    const b = input[idx + 1] ?? a
    output[i] = a + (b - a) * frac
  }
  return output
}

/**
 * @param {AudioBuffer} buffer
 * @returns {Float32Array}
 */
export function mixToMono(buffer) {
  if (buffer.numberOfChannels === 1) {
    return buffer.getChannelData(0).slice()
  }
  const len = buffer.length
  const mono = new Float32Array(len)
  for (let c = 0; c < buffer.numberOfChannels; c++) {
    const ch = buffer.getChannelData(c)
    for (let i = 0; i < len; i++) mono[i] += ch[i] / buffer.numberOfChannels
  }
  return mono
}
