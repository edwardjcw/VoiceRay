// @ts-check
import { test, expect } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(fileURLToPath(new URL('.', import.meta.url)), '../..')
const pitWavPath = path.join(repoRoot, 'tests', 'fixtures', 'pit-practice-pat.wav')
const apiBase = process.env.VOICERAY_API_URL || 'http://127.0.0.1:5000'

test.describe('pit.wav inference (real API)', () => {
  test('analyze pat practice with pit recording infers ih via whisper', async ({ request }) => {
    test.skip(!fs.existsSync(pitWavPath), 'pit-practice-pat.wav fixture missing')

    const response = await request.post(`${apiBase}/api/v1/analyze`, {
      multipart: {
        audio: {
          name: 'pit-practice-pat.wav',
          mimeType: 'audio/wav',
          buffer: fs.readFileSync(pitWavPath),
        },
        text: 'pat',
        locale: 'en-US',
      },
      timeout: 60_000,
    })

    expect(response.ok()).toBeTruthy()
    const body = await response.json()

    expect(body.phonemes).toHaveLength(3)
    expect(body.phonemes[1].ipa).toBe('ɪ')
    expect(body.metadata.phonemeInference).toBe('whisper:pit')
    expect(body.metadata.inferredWord).toBe('pit')
  })

  test('compare pat reference against pit analyze coaches ae to ih', async ({ request }) => {
    test.skip(!fs.existsSync(pitWavPath), 'pit-practice-pat.wav fixture missing')

    const analyzeResponse = await request.post(`${apiBase}/api/v1/analyze`, {
      multipart: {
        audio: {
          name: 'pit-practice-pat.wav',
          mimeType: 'audio/wav',
          buffer: fs.readFileSync(pitWavPath),
        },
        text: 'pat',
        locale: 'en-US',
      },
      timeout: 60_000,
    })
    expect(analyzeResponse.ok()).toBeTruthy()
    const analyze = await analyzeResponse.json()

    const referenceResponse = await request.post(`${apiBase}/api/v1/reference`, {
      data: { text: 'pat', locale: 'en-US' },
      timeout: 60_000,
    })
    expect(referenceResponse.ok()).toBeTruthy()
    const reference = await referenceResponse.json()

    const compareResponse = await request.post(`${apiBase}/api/v1/compare`, {
      data: {
        locale: 'en-US',
        referencePhonemes: reference.phonemes,
        userPhonemes: analyze.phonemes,
      },
    })
    expect(compareResponse.ok()).toBeTruthy()
    const compare = await compareResponse.json()

    const substitution = compare.segments.find(
      (segment) =>
        segment.kind === 'substitution' && segment.referenceIpa === 'æ' && segment.userIpa === 'ɪ',
    )
    expect(substitution).toBeTruthy()
    expect(compare.coaching.length).toBeGreaterThan(0)
    expect(compare.coaching[0].message.toLowerCase()).toContain('pit')
  })
})

test.describe('pit.wav UI flow (real API)', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      window.__VOICERAY_E2E__ = true
      window.__VOICERAY_E2E_FIXTURE_URL__ = '/e2e/pit-practice-pat.wav'
    })
    await page.goto('/')
  })

  test('record pit fixture while practicing pat surfaces coaching on compare', async ({ page }) => {
    test.skip(!fs.existsSync(pitWavPath), 'pit-practice-pat.wav fixture missing')

    await expect(page.getByTestId('health-status')).toContainText('API ok', { timeout: 30_000 })
    await page.getByTestId('load-reference').click()
    await expect(page.getByTestId('ipa-display')).toHaveText('pæt')

    await page.getByTestId('step-record').click()
    await page.getByTestId('record-start').click()
    await page.getByTestId('record-stop').click()
    await page.getByTestId('analyze-submit').click()

    await expect(page.getByTestId('status-line')).toContainText('pit', { timeout: 60_000 })

    await page.getByTestId('step-compare').click()
    await page.getByTestId('run-compare').click()

    await expect(page.getByTestId('status-line')).toContainText('Compare ready', { timeout: 15_000 })
    await expect(page.getByTestId('coaching-list').getByTestId('coaching-item')).toContainText(
      /pat|pit/i,
    )
  })
})
