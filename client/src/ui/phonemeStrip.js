/**
 * Render IPA phoneme segments with active highlight.
 * @param {HTMLElement} el
 * @param {{ ipa: string; startMs: number; endMs: number }[]} phonemes
 * @param {number} [timeMs]
 */
/**
 * @param {HTMLElement} el
 * @param {{ ipa: string; startMs: number; endMs: number }[]} phonemes
 * @param {number} [timeMs]
 * @param {{ substitutionIpa?: Set<string> }} [options]
 */
export function renderPhonemeStrip(el, phonemes, timeMs = -1, options = {}) {
  if (!phonemes?.length) {
    el.innerHTML = '<span class="ipa-muted">No phonemes</span>'
    return
  }
  const substitutionIpa = options.substitutionIpa
  el.innerHTML = phonemes
    .map((seg) => {
      const active =
        timeMs >= 0 && timeMs >= seg.startMs && timeMs < seg.endMs ? ' ipa-active' : ''
      const substituted =
        substitutionIpa?.has(seg.ipa) ? ' ipa-substitution' : ''
      return `<span class="ipa-seg${active}${substituted}" data-ipa="${seg.ipa}">/${seg.ipa}/</span>`
    })
    .join('')
}

/**
 * @param {HTMLElement} el
 * @param {{ analyze?: { phonemes: { ipa: string }[] }; compare?: { segments?: { kind?: string; referenceIpa?: string; userIpa?: string }[] } }} session
 */
export function renderComparePhonemeStrip(el, session) {
  const phonemes = session.analyze?.phonemes ?? []
  const substitutionIpa = new Set(
    (session.compare?.segments ?? [])
      .filter((s) => s.kind === 'substitution' && s.userIpa)
      .map((s) => s.userIpa),
  )
  renderPhonemeStrip(el, phonemes, -1, { substitutionIpa })
}
