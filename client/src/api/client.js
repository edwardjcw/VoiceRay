/** @returns {string} API origin without trailing slash; empty string uses same-origin (Vite dev proxy). */
export function getApiBaseUrl() {
  const configured = import.meta.env?.VITE_API_BASE_URL
  if (configured === undefined || configured === '') {
    return ''
  }
  return configured.replace(/\/$/, '')
}

/**
 * @returns {Promise<{ status: string, product?: string, speechProvider?: string }>}
 */
export async function fetchHealth() {
  const response = await fetch(`${getApiBaseUrl()}/api/v1/health`)
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return response.json()
}
