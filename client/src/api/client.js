/** @returns {string} API origin without trailing slash; empty string uses same-origin (Vite dev proxy). */
export function getApiBaseUrl() {
  const configured = import.meta.env?.VITE_API_BASE_URL
  if (configured === undefined || configured === '') {
    return ''
  }
  return configured.replace(/\/$/, '')
}

/**
 * @param {string} path
 * @returns {string}
 */
export function resolveApiPath(path) {
  const base = getApiBaseUrl()
  if (!path) return base || ''
  if (path.startsWith('http://') || path.startsWith('https://')) return path
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${base}${normalized}`
}

/**
 * @typedef {{ ipa: string; startMs: number; endMs: number }} PhonemeSegment
 * @typedef {{ ipa: string; startMs: number; endMs: number; layers: object; highlight?: string[] }} ArticulatoryKeyframe
 * @typedef {{ alignmentEngine: string; computeDevice: string; deviceBanner: string; sampleRateHz: number; channels: number }} AnalyzeMetadata
 * @typedef {{ audioUrl?: string; audioBase64?: string; phonemes: PhonemeSegment[]; keyframes: ArticulatoryKeyframe[]; ipaDisplay: string }} ReferenceResponse
 * @typedef {{ phonemes: PhonemeSegment[]; keyframes: ArticulatoryKeyframe[]; scores: object[]; audioEcho?: string; metadata: AnalyzeMetadata }} AnalyzeResponse
 * @typedef {{ segments: object[]; coaching: { message: string; highlightLayers?: string[]; referenceIpa?: string; userIpa?: string }[] }} CompareResponse
 */

/**
 * @returns {Promise<{ status: string; product?: string; speechProvider?: string }>}
 */
export async function fetchHealth() {
  const response = await fetch(resolveApiPath('/api/v1/health'))
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return response.json()
}

/**
 * @param {{ text: string; locale: string }} body
 * @returns {Promise<ReferenceResponse>}
 */
export async function postReference(body) {
  const response = await fetch(resolveApiPath('/api/v1/reference'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(`Reference failed (${response.status})${detail ? `: ${detail}` : ''}`)
  }
  return response.json()
}

/**
 * @param {{ audio: Blob; text: string; locale: string }} params
 * @returns {Promise<AnalyzeResponse>}
 */
export async function postAnalyze({ audio, text, locale }) {
  const form = new FormData()
  form.append('audio', audio, 'recording.wav')
  form.append('text', text)
  form.append('locale', locale)
  const response = await fetch(resolveApiPath('/api/v1/analyze'), {
    method: 'POST',
    body: form,
  })
  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(`Analyze failed (${response.status})${detail ? `: ${detail}` : ''}`)
  }
  return response.json()
}

/**
 * @param {{ referencePhonemes: PhonemeSegment[]; userPhonemes: PhonemeSegment[]; locale: string }} body
 * @returns {Promise<CompareResponse>}
 */
export async function postCompare(body) {
  const response = await fetch(resolveApiPath('/api/v1/compare'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(`Compare failed (${response.status})${detail ? `: ${detail}` : ''}`)
  }
  return response.json()
}

/**
 * @param {ReferenceResponse | AnalyzeResponse | null | undefined} payload
 * @returns {string | null}
 */
export function audioSrcFromPayload(payload) {
  if (!payload) return null
  if (payload.audioUrl) return resolveApiPath(payload.audioUrl)
  if (payload.audioBase64) {
    return `data:audio/wav;base64,${payload.audioBase64}`
  }
  if (payload.audioEcho) {
    return `data:audio/wav;base64,${payload.audioEcho}`
  }
  return null
}
