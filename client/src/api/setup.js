import { resolveApiPath } from './client.js'

/**
 * @typedef {{ at: string; message: string; level: string }} SetupLogEntry
 * @typedef {{ id: string; label: string; status: string; detail: string; required: boolean; canAutoProvision: boolean }} SetupResource
 * @typedef {{ state: string; ready: boolean; lastError?: string | null; logs: SetupLogEntry[]; resources: SetupResource[] }} SetupStatus
 */

/**
 * @returns {Promise<SetupStatus>}
 */
export async function fetchSetupStatus() {
  const response = await fetch(resolveApiPath('/api/v1/setup/status'))
  if (!response.ok) {
    throw new Error(`Setup status failed (${response.status})`)
  }
  return response.json()
}

/**
 * @returns {Promise<{ state: string; message?: string }>}
 */
export async function postSetupRun() {
  const response = await fetch(resolveApiPath('/api/v1/setup/run'), { method: 'POST' })
  const body = await response.json().catch(() => ({}))
  if (response.status === 409) {
    return body
  }
  if (!response.ok && response.status !== 202) {
    throw new Error(body.message || body.lastError || `Setup failed (${response.status})`)
  }
  return body
}

/** @deprecated Use postSetupRun */
export async function postProvisionSpeech() {
  return postSetupRun()
}

/**
 * @param {Response} response
 * @returns {Promise<never>}
 */
export async function throwApiError(response) {
  let detail = ''
  try {
    const json = await response.json()
    detail = json.error || json.message || JSON.stringify(json)
  } catch {
    detail = await response.text().catch(() => '')
  }
  throw new Error(detail || `Request failed (${response.status})`)
}

/**
 * @param {SetupStatus} status
 * @param {(status: SetupStatus) => void} [onUpdate]
 */
export function notifySetupUpdate(status, onUpdate) {
  onUpdate?.(status)
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

/**
 * Poll activity log while an async API call runs.
 * @param {(status: SetupStatus) => void} onUpdate
 * @returns {() => void} stop polling
 */
export function startActivityPolling(onUpdate) {
  let active = true

  const tick = async () => {
    while (active) {
      try {
        const status = await fetchSetupStatus()
        onUpdate(status)
      } catch {
        // ignore transient poll errors
      }
      await sleep(400)
    }
  }

  tick()

  return () => {
    active = false
  }
}

/**
 * Poll setup API until required resources are ready; starts setup when idle.
 * @param {{ onUpdate?: (status: SetupStatus) => void; pollMs?: number }} [options]
 */
export async function waitForSetupReady(options = {}) {
  const pollMs = options.pollMs ?? 400
  let startRequested = false

  options.onUpdate?.({
    state: 'running',
    ready: false,
    logs: [{ at: new Date().toISOString(), message: 'Checking required resources…', level: 'info' }],
    resources: [],
  })

  for (let attempt = 0; attempt < 3600; attempt += 1) {
    const status = await fetchSetupStatus()
    notifySetupUpdate(status, options.onUpdate)

    if (status.ready) {
      return status
    }

    if (status.state === 'failed') {
      throw new Error(status.lastError || 'Resource setup failed.')
    }

    if (status.state === 'running') {
      await sleep(pollMs)
      continue
    }

    if (status.state === 'succeeded' && !status.ready) {
      throw new Error(status.lastError || 'Setup finished but required resources are still missing.')
    }

    if (!startRequested) {
      options.onUpdate?.({
        ...status,
        logs: [
          ...(status.logs ?? []),
          { at: new Date().toISOString(), message: 'Starting resource setup…', level: 'info' },
        ],
      })
      await postSetupRun()
      startRequested = true
      await sleep(pollMs)
      continue
    }

    await sleep(pollMs)
  }

  throw new Error('Resource setup timed out. Check the setup log and try again.')
}

/**
 * @param {import('./speechSetup.js').SpeechCapabilities | null | undefined} speech
 */
export async function ensureSpeechReady(speech) {
  if (speech?.allRequiredReady) return
  await waitForSetupReady()
}
