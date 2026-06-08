/** @param {import('@playwright/test').Page} page */
export async function installMockApi(page) {
  const keyframes = [
    {
      ipa: 'p',
      startMs: 0,
      endMs: 100,
      pose: {
        jawOpen: 0.1,
        tongueHeight: 0.4,
        tongueBackness: 0.45,
        tongueTip: 0,
        interdental: 0,
        lipRounding: 0,
        lipClosure: 1,
        velum: 0,
      },
      highlight: ['bilabial'],
    },
    {
      ipa: 'æ',
      startMs: 100,
      endMs: 250,
      pose: {
        jawOpen: 0.82,
        tongueHeight: 0.18,
        tongueBackness: 0.2,
        tongueTip: 0,
        interdental: 0,
        lipRounding: 0,
        lipClosure: 0,
        velum: 0,
      },
      highlight: ['open_vowel'],
    },
  ]

  const referencePhonemes = [
    { ipa: 'p', startMs: 0, endMs: 100 },
    { ipa: 'æ', startMs: 100, endMs: 250 },
    { ipa: 't', startMs: 250, endMs: 350 },
  ]

  const userPhonemes = [
    { ipa: 'p', startMs: 0, endMs: 110 },
    { ipa: 'æ', startMs: 110, endMs: 260 },
    { ipa: 'd', startMs: 260, endMs: 360 },
  ]

  const setupStatusBody = {
    state: 'succeeded',
    ready: true,
    lastError: null,
    logs: [{ at: new Date().toISOString(), message: 'All required resources are ready.', level: 'info' }],
    resources: [
      { id: 'piper', label: 'Piper TTS', status: 'ready', detail: 'ready', required: true, canAutoProvision: true },
      { id: 'wav2vec2', label: 'Wav2Vec2 phoneme model', status: 'ready', detail: '', required: false, canAutoProvision: true },
      { id: 'whisper', label: 'Whisper (fallback)', status: 'ready', detail: '', required: false, canAutoProvision: true },
      { id: 'vocalTract', label: 'Sagittal', status: 'ready', detail: '', required: true, canAutoProvision: true },
      { id: 'mfa', label: 'MFA', status: 'optional', detail: '', required: false, canAutoProvision: false },
    ],
  }

  await page.route('**/api/v1/setup/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(setupStatusBody),
    })
  })

  await page.route('**/api/v1/setup/run', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ state: 'succeeded', message: 'ready' }),
    })
  })

  await page.route('**/api/v1/health', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'ok',
        product: 'VoiceRay',
        speechProvider: 'Local',
        speech: {
          piperReady: true,
          piperStatus: 'ready',
          canAutoProvision: true,
          whisperCacheAvailable: true,
          wav2vec2Ready: true,
          vocalTractReady: true,
          allRequiredReady: true,
          setupState: 'succeeded',
        },
      }),
    })
  })

  await page.route('**/api/v1/reference', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        audioBase64: SILENT_WAV_BASE64,
        phonemes: referencePhonemes,
        keyframes,
        ipaDisplay: 'pæt',
      }),
    })
  })

  await page.route('**/api/v1/analyze', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        phonemes: userPhonemes,
        keyframes,
        scores: [{ ipa: 'p', score: 90, accuracy: 'good' }],
        audioEcho: SILENT_WAV_BASE64,
        metadata: {
          alignmentEngine: 'wav2vec2',
          computeDevice: 'cpu',
          deviceBanner:
            'Alignment running on CPU — enable CUDA for GPU acceleration (VOICERAY_FORCE_CPU unset).',
          sampleRateHz: 16000,
          channels: 1,
          phonemeInference: 'wav2vec2',
          inferenceNote: 'wav2vec2 heard: pæd',
        },
      }),
    })
  })

  await page.route('**/api/v1/compare', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        segments: [{ kind: 'substitution', referenceIpa: 't', userIpa: 'd' }],
        coaching: [
          {
            message: 'Use a voiceless alveolar stop, not a voiced one.',
            highlightLayers: ['tongue'],
            referenceIpa: 't',
            userIpa: 'd',
          },
        ],
      }),
    })
  })
}

/** Minimal valid 16-bit mono WAV (silence, ~100ms at 16kHz). */
const SILENT_WAV_BASE64 =
  'UklGRigAAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQQAAAAAAA=='
