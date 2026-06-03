import { fetchHealth, getApiBaseUrl } from '../api/client.js'

/**
 * @param {HTMLElement} mount
 */
export async function renderApp(mount) {
  mount.innerHTML = `
    <main class="voiceray-app">
      <h1>VoiceRay</h1>
      <p class="tagline">Sagittal vocal-tract pronunciation coach (scaffold)</p>
      <p class="api-base">API: <code>${getApiBaseUrl() || '(same-origin / dev proxy)'}</code></p>
      <p id="health-status" class="health">Checking API…</p>
    </main>
  `

  const healthEl = mount.querySelector('#health-status')
  try {
    const health = await fetchHealth()
    healthEl.textContent = `API ${health.status} (${health.speechProvider ?? 'unknown'} speech)`
    healthEl.classList.add('health-ok')
  } catch {
    healthEl.textContent =
      'API unreachable — start VoiceRay.Api (dotnet run) for local dev.'
    healthEl.classList.add('health-warn')
  }
}
