import { encodeWav16Mono, mixToMono, resampleMono } from './wav.js'

const TARGET_RATE = 16000

/**
 * E2E/test stub — minimal 16 kHz mono WAV without microphone.
 * @returns {Promise<{ stop: () => void; blob: Promise<Blob> }>}
 */
async function startRecordingStub() {
  const fixtureUrl =
    typeof window !== 'undefined' ? window.__VOICERAY_E2E_FIXTURE_URL__ : undefined

  if (fixtureUrl) {
    const response = await fetch(fixtureUrl)
    if (!response.ok) {
      throw new Error(`E2E fixture fetch failed (${response.status}): ${fixtureUrl}`)
    }
    const blob = await response.blob()
    return { stop: () => {}, blob: Promise.resolve(blob) }
  }

  const samples = new Float32Array(1600)
  const blob = encodeWav16Mono(samples, 16000)
  return { stop: () => {}, blob: Promise.resolve(blob) }
}

/**
 * Record microphone audio via MediaRecorder; call `stop()` to finish and get WAV.
 * @param {{ maxDurationMs?: number; onStatus?: (msg: string) => void }} [options]
 * @returns {Promise<{ stop: () => void; blob: Promise<Blob> }>}
 */
export async function startRecording(options = {}) {
  if (typeof window !== 'undefined' && window.__VOICERAY_E2E__) {
    return startRecordingStub()
  }

  const maxDurationMs = options.maxDurationMs ?? 8000
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
  const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
    ? 'audio/webm;codecs=opus'
    : 'audio/webm'
  const recorder = new MediaRecorder(stream, { mimeType })
  /** @type {Blob[]} */
  const chunks = []

  recorder.ondataavailable = (ev) => {
    if (ev.data.size > 0) chunks.push(ev.data)
  }

  let stopped = false

  const blob = new Promise((resolve, reject) => {
    recorder.onstop = async () => {
      try {
        stream.getTracks().forEach((t) => t.stop())
        const webm = new Blob(chunks, { type: mimeType })
        resolve(await webmToWav16Mono(webm))
      } catch (err) {
        reject(err)
      }
    }
    recorder.onerror = () => reject(new Error('MediaRecorder failed'))
  })

  const stop = () => {
    if (stopped) return
    stopped = true
    if (recorder.state !== 'inactive') recorder.stop()
  }

  recorder.start(250)
  options.onStatus?.('Recording…')
  window.setTimeout(stop, maxDurationMs)

  return { stop, blob }
}

/**
 * @param {Blob} webmBlob
 * @returns {Promise<Blob>}
 */
export async function webmToWav16Mono(webmBlob) {
  const ctx = new AudioContext()
  try {
    const arrayBuffer = await webmBlob.arrayBuffer()
    const audioBuffer = await ctx.decodeAudioData(arrayBuffer.slice(0))
    const mono = mixToMono(audioBuffer)
    const resampled = resampleMono(mono, audioBuffer.sampleRate, TARGET_RATE)
    return encodeWav16Mono(resampled, TARGET_RATE)
  } finally {
    await ctx.close()
  }
}
