/**
 * Render IPA phoneme segments with active highlight.
 * @param {HTMLElement} el
 * @param {{ ipa: string; startMs: number; endMs: number }[]} phonemes
 * @param {number} [timeMs]
 */
export function renderPhonemeStrip(el, phonemes, timeMs = -1) {
  if (!phonemes?.length) {
    el.innerHTML = '<span class="ipa-muted">No phonemes</span>'
    return
  }
  el.innerHTML = phonemes
    .map((seg) => {
      const active =
        timeMs >= 0 && timeMs >= seg.startMs && timeMs < seg.endMs ? ' ipa-active' : ''
      return `<span class="ipa-seg${active}" data-ipa="${seg.ipa}">/${seg.ipa}/</span>`
    })
    .join('')
}
