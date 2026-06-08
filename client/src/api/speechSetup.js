import { resolveApiPath } from './client.js'
import { fetchSetupStatus, postSetupRun, throwApiError, waitForSetupReady } from './setup.js'

/**
 * @typedef {{ piperReady?: boolean; piperStatus?: string; canAutoProvision?: boolean; whisperCacheAvailable?: boolean; vocalTractReady?: boolean; allRequiredReady?: boolean; setupState?: string }} SpeechCapabilities
 * @typedef {{ status: string; product?: string; speechProvider?: string; speech?: SpeechCapabilities }} HealthResponse
 */

/**
 * @param {HealthResponse} health
 * @returns {SpeechCapabilities | null}
 */
export function speechFromHealth(health) {
  return health?.speech ?? null
}

/**
 * @returns {Promise<HealthResponse>}
 */
export async function fetchHealth() {
  const response = await fetch(resolveApiPath('/api/v1/health'))
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return response.json()
}

export {
  fetchSetupStatus,
  postSetupRun,
  postProvisionSpeech,
  throwApiError,
  waitForSetupReady,
  ensureSpeechReady,
  startActivityPolling,
} from './setup.js'
