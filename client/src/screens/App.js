import {
  fetchHealth,
  getApiBaseUrl,
  postReference,
  postAnalyze,
  postCompare,
  audioSrcFromPayload,
} from '../api/client.js'
import { mountSagittalPlayer, mountComparePlayers } from '../animation/SagittalPlayer.js'
import { KeyframeSync } from '../audio/syncPlayback.js'
import { startRecording } from '../audio/recorder.js'
import { session, updateSession, clearAnalyzeAndCompare } from '../state/session.js'
import { DEMO_WORDS } from '../constants/demoWords.js'
import { renderPhonemeStrip } from '../ui/phonemeStrip.js'

/** @type {'practice' | 'record' | 'compare'} */
let activeStep = 'practice'

/** @type {import('../animation/SagittalPlayer.js').SagittalPlayer | null} */
let practicePlayer = null
/** @type {import('../animation/SagittalPlayer.js').SagittalPlayer | null} */
let recordPlayer = null
/** @type {{ ghost: import('../animation/SagittalPlayer.js').SagittalPlayer; user: import('../animation/SagittalPlayer.js').SagittalPlayer } | null} */
let comparePlayers = null

/** @type {KeyframeSync | null} */
let activeSync = null
/** @type {HTMLAudioElement | null} */
let activeAudio = null

/** @type {{ stop: () => void; blob: Promise<Blob> } | null} */
let recordingSession = null

/**
 * @param {HTMLElement} mount
 */
export async function renderApp(mount) {
  mount.innerHTML = buildShell()
  wireShell(mount)
  await refreshHealth(mount)
  await showStep(mount, activeStep)
}

function buildShell() {
  const wordOptions = DEMO_WORDS.map(
    (w) => `<option value="${w}"${w === session.text ? ' selected' : ''}>${w}</option>`,
  ).join('')

  return `
    <main class="voiceray-app">
      <header class="app-header">
        <h1>VoiceRay</h1>
        <p class="tagline">Sagittal vocal-tract pronunciation coach</p>
        <p class="api-base">API: <code>${getApiBaseUrl() || '(same-origin / dev proxy)'}</code></p>
        <p id="health-status" class="health" data-testid="health-status">Checking API…</p>
      </header>

      <section class="word-bar">
        <label for="word-select">Word</label>
        <select id="word-select" data-testid="word-select">${wordOptions}</select>
        <span class="locale-tag">en-US</span>
      </section>

      <nav class="step-nav" aria-label="Practice flow">
        <button type="button" class="step-tab is-active" data-step="practice" data-testid="step-practice">Practice</button>
        <button type="button" class="step-tab" data-step="record" data-testid="step-record">Record</button>
        <button type="button" class="step-tab" data-step="compare" data-testid="step-compare">Compare</button>
      </nav>

      <div id="step-panels"></div>
      <p id="status-line" class="status-line" role="status" data-testid="status-line"></p>
    </main>
  `
}

/**
 * @param {HTMLElement} mount
 */
function wireShell(mount) {
  const wordSelect = mount.querySelector('#word-select')
  wordSelect?.addEventListener('change', async () => {
    updateSession({ text: wordSelect.value, reference: null })
    clearAnalyzeAndCompare()
    setStatus(mount, `Word set to "${session.text}". Load reference again.`)
    await showStep(mount, activeStep)
  })

  mount.querySelectorAll('.step-tab').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const step = btn.getAttribute('data-step')
      if (step === 'practice' || step === 'record' || step === 'compare') {
        await showStep(mount, step)
      }
    })
  })
}

/**
 * @param {HTMLElement} mount
 * @param {'practice' | 'record' | 'compare'} step
 */
async function showStep(mount, step) {
  activeStep = step
  mount.querySelectorAll('.step-tab').forEach((btn) => {
    btn.classList.toggle('is-active', btn.getAttribute('data-step') === step)
  })

  const panels = mount.querySelector('#step-panels')
  if (!panels) return

  stopPlayback()

  if (step === 'practice') {
    panels.innerHTML = practicePanelHtml()
    await initPracticePanel(mount)
  } else if (step === 'record') {
    panels.innerHTML = recordPanelHtml()
    await initRecordPanel(mount)
  } else {
    panels.innerHTML = comparePanelHtml()
    await initComparePanel(mount)
  }
}

function practicePanelHtml() {
  const ref = session.reference
  return `
    <section class="panel" data-testid="practice-panel">
      <div class="panel-actions">
        <button type="button" id="btn-load-reference" data-testid="load-reference">Load reference</button>
        <button type="button" id="btn-play-reference" data-testid="play-reference" ${ref ? '' : 'disabled'}>Play reference</button>
      </div>
      <p class="ipa-display" data-testid="ipa-display">${ref?.ipaDisplay ?? '—'}</p>
      <div id="practice-phonemes" class="phoneme-strip" data-testid="practice-phonemes"></div>
      <div id="practice-sagittal" class="sagittal-view" data-testid="sagittal-view"></div>
      <audio id="practice-audio" class="sr-audio" data-testid="practice-audio"></audio>
    </section>
  `
}

function recordPanelHtml() {
  const banner = session.analyze?.metadata?.deviceBanner ?? ''
  return `
    <section class="panel" data-testid="record-panel">
      <div id="device-banner" class="device-banner${banner ? ' is-visible' : ''}" data-testid="device-banner" role="alert">
        ${banner || 'Device banner appears after analyze.'}
      </div>
      <div class="panel-actions">
        <button type="button" id="btn-start-record" data-testid="record-start">Start recording</button>
        <button type="button" id="btn-stop-record" data-testid="record-stop" disabled>Stop</button>
        <button type="button" id="btn-analyze" data-testid="analyze-submit" disabled>Analyze</button>
        <button type="button" id="btn-play-user" data-testid="play-user" disabled>Play recording</button>
      </div>
      <div id="record-phonemes" class="phoneme-strip" data-testid="record-phonemes"></div>
      <div id="record-sagittal" class="sagittal-view" data-testid="record-sagittal"></div>
      <audio id="record-audio" class="sr-audio" data-testid="record-audio"></audio>
    </section>
  `
}

function comparePanelHtml() {
  const coaching = session.compare?.coaching ?? []
  const coachingHtml = coaching.length
    ? coaching
        .map(
          (c) =>
            `<li data-testid="coaching-item">${escapeHtml(c.message)}</li>`,
        )
        .join('')
    : '<li class="ipa-muted">Run analyze, then open Compare.</li>'

  return `
    <section class="panel" data-testid="compare-panel">
      <div class="panel-actions">
        <button type="button" id="btn-run-compare" data-testid="run-compare">Run compare</button>
        <button type="button" id="btn-play-compare" data-testid="play-compare" disabled>Scrub compare</button>
      </div>
      <div class="compare-legend">
        <span class="legend-ghost">Ghost = reference</span>
        <span class="legend-user">Solid = your recording</span>
      </div>
      <div id="compare-phonemes" class="phoneme-strip" data-testid="compare-phonemes"></div>
      <div id="compare-sagittal" class="sagittal-view compare-view" data-testid="compare-sagittal"></div>
      <ul id="coaching-list" class="coaching-list" data-testid="coaching-list">${coachingHtml}</ul>
      <audio id="compare-audio" class="sr-audio" data-testid="compare-audio"></audio>
    </section>
  `
}

/**
 * @param {HTMLElement} mount
 */
async function initPracticePanel(mount) {
  const container = mount.querySelector('#practice-sagittal')
  if (container) {
    practicePlayer = await mountSagittalPlayer(container)
  }

  const ref = session.reference
  const strip = mount.querySelector('#practice-phonemes')
  if (strip && ref) renderPhonemeStrip(strip, ref.phonemes)

  mount.querySelector('#btn-load-reference')?.addEventListener('click', async () => {
    try {
      setStatus(mount, 'Loading reference…')
      const reference = await postReference({ text: session.text, locale: session.locale })
      updateSession({ reference })
      clearAnalyzeAndCompare()
      setStatus(mount, `Reference loaded for "${session.text}".`)
      await showStep(mount, 'practice')
    } catch (err) {
      setStatus(mount, err instanceof Error ? err.message : 'Reference failed')
    }
  })

  mount.querySelector('#btn-play-reference')?.addEventListener('click', async () => {
    const reference = session.reference
    if (!reference || !practicePlayer) return
    const src = audioSrcFromPayload(reference)
    if (!src) {
      setStatus(mount, 'No reference audio in response.')
      return
    }
    await playWithSync(
      mount.querySelector('#practice-audio'),
      src,
      reference.keyframes,
      practicePlayer,
      mount.querySelector('#practice-phonemes'),
      reference.phonemes,
    )
  })
}

/**
 * @param {HTMLElement} mount
 */
async function initRecordPanel(mount) {
  const container = mount.querySelector('#record-sagittal')
  if (container) {
    recordPlayer = await mountSagittalPlayer(container)
  }

  /** @type {Blob | null} */
  let pendingWav = null

  const analyze = session.analyze
  if (analyze) {
    updateDeviceBanner(mount, analyze.metadata?.deviceBanner)
    const strip = mount.querySelector('#record-phonemes')
    if (strip) renderPhonemeStrip(strip, analyze.phonemes)
    mount.querySelector('#btn-play-user')?.removeAttribute('disabled')
  }

  mount.querySelector('#btn-start-record')?.addEventListener('click', async () => {
    try {
      if (!session.reference) {
        setStatus(mount, 'Load reference on Practice first.')
        return
      }
      pendingWav = null
      recordingSession = await startRecording({
        onStatus: (msg) => setStatus(mount, msg),
      })
      mount.querySelector('#btn-start-record')?.setAttribute('disabled', 'true')
      mount.querySelector('#btn-stop-record')?.removeAttribute('disabled')
      mount.querySelector('#btn-analyze')?.setAttribute('disabled', 'true')
      setStatus(mount, 'Recording… click Stop when done.')
    } catch (err) {
      setStatus(mount, err instanceof Error ? err.message : 'Mic unavailable')
    }
  })

  mount.querySelector('#btn-stop-record')?.addEventListener('click', async () => {
    if (!recordingSession) return
    recordingSession.stop()
    try {
      pendingWav = await recordingSession.blob
      mount.querySelector('#btn-analyze')?.removeAttribute('disabled')
      setStatus(mount, 'Recording ready — Analyze.')
    } catch (err) {
      setStatus(mount, err instanceof Error ? err.message : 'Recording failed')
    } finally {
      mount.querySelector('#btn-start-record')?.removeAttribute('disabled')
      mount.querySelector('#btn-stop-record')?.setAttribute('disabled', 'true')
      recordingSession = null
    }
  })

  mount.querySelector('#btn-analyze')?.addEventListener('click', async () => {
    if (!pendingWav) return
    try {
      setStatus(mount, 'Analyzing…')
      const result = await postAnalyze({
        audio: pendingWav,
        text: session.text,
        locale: session.locale,
      })
      updateSession({ analyze: result, compare: null })
      updateDeviceBanner(mount, result.metadata?.deviceBanner)
      const strip = mount.querySelector('#record-phonemes')
      if (strip) renderPhonemeStrip(strip, result.phonemes)
      mount.querySelector('#btn-play-user')?.removeAttribute('disabled')
      setStatus(mount, 'Analyze complete.')
    } catch (err) {
      setStatus(mount, err instanceof Error ? err.message : 'Analyze failed')
    }
  })

  mount.querySelector('#btn-play-user')?.addEventListener('click', async () => {
    const result = session.analyze
    if (!result || !recordPlayer) return
    const src = audioSrcFromPayload(result)
    if (!src) {
      setStatus(mount, 'No user audio to play.')
      return
    }
    await playWithSync(
      mount.querySelector('#record-audio'),
      src,
      result.keyframes,
      recordPlayer,
      mount.querySelector('#record-phonemes'),
      result.phonemes,
    )
  })
}

/**
 * @param {HTMLElement} mount
 */
async function initComparePanel(mount) {
  const container = mount.querySelector('#compare-sagittal')
  if (container) {
    comparePlayers = await mountComparePlayers(container)
  }

  const ref = session.reference
  const user = session.analyze
  if (ref && comparePlayers) {
    comparePlayers.ghost.playKeyframes(ref.keyframes, 0)
    comparePlayers.ghost.applyGhostPose(ref.keyframes[0]?.layers ?? null)
  }
  if (user && comparePlayers) {
    comparePlayers.user.playKeyframes(user.keyframes, 0)
  }

  const strip = mount.querySelector('#compare-phonemes')
  if (strip && ref && user) {
    renderPhonemeStrip(strip, user.phonemes)
  }

  mount.querySelector('#btn-run-compare')?.addEventListener('click', async () => {
    const reference = session.reference
    const analyze = session.analyze
    if (!reference || !analyze) {
      setStatus(mount, 'Need reference + analyze first.')
      return
    }
    try {
      setStatus(mount, 'Comparing…')
      const compare = await postCompare({
        referencePhonemes: reference.phonemes,
        userPhonemes: analyze.phonemes,
        locale: session.locale,
      })
      updateSession({ compare })
      await showStep(mount, 'compare')
      setStatus(mount, `Compare: ${compare.segments?.length ?? 0} segments.`)
    } catch (err) {
      setStatus(mount, err instanceof Error ? err.message : 'Compare failed')
    }
  })

  mount.querySelector('#btn-play-compare')?.addEventListener('click', async () => {
    const reference = session.reference
    const analyze = session.analyze
    if (!reference || !analyze || !comparePlayers) return
    const src = audioSrcFromPayload(analyze) ?? audioSrcFromPayload(reference)
    if (!src) {
      setStatus(mount, 'No audio for compare scrub.')
      return
    }
    const audio = mount.querySelector('#compare-audio')
    if (!(audio instanceof HTMLAudioElement)) return
    stopPlayback()
    audio.src = src
    activeAudio = audio
    await audio.play()
    activeSync = new KeyframeSync(comparePlayers.user, audio, analyze.keyframes)
    activeSync.start()
    const tick = () => {
      if (!audio.paused) {
        const ms = audio.currentTime * 1000
        comparePlayers?.ghost.playKeyframes(reference.keyframes, ms)
        const ghostLayers = reference.keyframes.find(
          (f) => ms >= (f.startMs ?? 0) && ms < (f.endMs ?? Number.POSITIVE_INFINITY),
        )?.layers
        if (ghostLayers) comparePlayers?.ghost.applyGhostPose(ghostLayers)
        renderPhonemeStrip(
          mount.querySelector('#compare-phonemes'),
          analyze.phonemes,
          ms,
        )
        requestAnimationFrame(tick)
      }
    }
    requestAnimationFrame(tick)
    audio.onended = () => stopPlayback()
  })

  if (session.reference && session.analyze) {
    mount.querySelector('#btn-play-compare')?.removeAttribute('disabled')
  }
}

/**
 * @param {HTMLElement | null} audioEl
 * @param {string} src
 * @param {object[]} keyframes
 * @param {import('../animation/SagittalPlayer.js').SagittalPlayer} player
 * @param {HTMLElement | null} stripEl
 * @param {{ ipa: string; startMs: number; endMs: number }[]} phonemes
 */
async function playWithSync(audioEl, src, keyframes, player, stripEl, phonemes) {
  if (!(audioEl instanceof HTMLAudioElement)) return
  stopPlayback()
  audioEl.src = src
  activeAudio = audioEl
  await audioEl.play()
  activeSync = new KeyframeSync(player, audioEl, keyframes)
  activeSync.start()
  const tick = () => {
    if (!audioEl.paused && stripEl) {
      renderPhonemeStrip(stripEl, phonemes, audioEl.currentTime * 1000)
      requestAnimationFrame(tick)
    }
  }
  requestAnimationFrame(tick)
  audioEl.onended = () => stopPlayback()
}

function stopPlayback() {
  activeSync?.dispose()
  activeSync = null
  if (activeAudio) {
    activeAudio.pause()
    activeAudio.onended = null
    activeAudio = null
  }
}

/**
 * @param {HTMLElement} mount
 * @param {string | undefined} banner
 */
function updateDeviceBanner(mount, banner) {
  const el = mount.querySelector('#device-banner')
  if (!el) return
  if (banner) {
    el.textContent = banner
    el.classList.add('is-visible')
  }
}

/**
 * @param {HTMLElement} mount
 */
async function refreshHealth(mount) {
  const healthEl = mount.querySelector('#health-status')
  if (!healthEl) return
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

/**
 * @param {HTMLElement} mount
 * @param {string} message
 */
function setStatus(mount, message) {
  const el = mount.querySelector('#status-line')
  if (el) el.textContent = message
}

/**
 * @param {string} text
 */
function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}
