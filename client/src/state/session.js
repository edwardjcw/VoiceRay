/** @typedef {import('../api/client.js').ReferenceResponse} ReferenceResponse */
/** @typedef {import('../api/client.js').AnalyzeResponse} AnalyzeResponse */
/** @typedef {import('../api/client.js').CompareResponse} CompareResponse */

/** Shared session state across practice → record → compare. */
export const session = {
  /** @type {string} */
  locale: 'en-US',
  /** @type {string} */
  text: 'pat',
  /** @type {ReferenceResponse | null} */
  reference: null,
  /** @type {AnalyzeResponse | null} */
  analyze: null,
  /** @type {CompareResponse | null} */
  compare: null,
}

/**
 * @param {Partial<typeof session>} patch
 */
export function updateSession(patch) {
  Object.assign(session, patch)
}

export function clearAnalyzeAndCompare() {
  session.analyze = null
  session.compare = null
}
