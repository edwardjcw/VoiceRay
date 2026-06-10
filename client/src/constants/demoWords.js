/** Demo pedagogical words (en-US) — must match backend G2P stub. */
export const DEMO_WORDS = [
  'pat',
  'pet',
  'pit',
  'pot',
  'put',
  'cat',
  'dog',
  'think',
  'red',
  'ship',
]

/** Supported input languages for typed practice words. */
export const LOCALES = [
  { code: 'en-US', label: 'English (US)' },
  { code: 'fr-FR', label: 'French' },
]

/**
 * Suggestion words per locale (shown as a datalist next to the free-text input).
 * en-US suggestions are the demo lexicon (exact phonemes); other locales are common
 * words synthesized + phoneme-recognized on the fly.
 */
export const SUGGESTIONS_BY_LOCALE = {
  'en-US': DEMO_WORDS,
  'fr-FR': [
    'chat',
    'chien',
    'rouge',
    'bonjour',
    'merci',
    'pain',
    'vin',
    'lune',
    'deux',
    'tu',
  ],
}

/**
 * @param {string} locale
 * @returns {string[]}
 */
export function suggestionsForLocale(locale) {
  return SUGGESTIONS_BY_LOCALE[locale] ?? []
}

/**
 * @param {string} locale
 * @returns {string} a sensible default word for the locale
 */
export function defaultWordForLocale(locale) {
  return suggestionsForLocale(locale)[0] ?? ''
}
