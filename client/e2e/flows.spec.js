// @ts-check
import { test, expect } from '@playwright/test'
import { installMockApi } from './mockApi.js'

test.describe('VoiceRay practice / record / compare flows', () => {
  test.beforeEach(async ({ page }) => {
    await installMockApi(page)
    await page.addInitScript(() => {
      window.__VOICERAY_E2E__ = true
    })
    await page.goto('/')
  })

  test('health and practice reference load', async ({ page }) => {
    await expect(page.getByTestId('health-status')).toContainText('API ok', { timeout: 15_000 })
    await expect(page.getByTestId('practice-panel')).toBeVisible()
    await page.getByTestId('load-reference').click()
    await expect(page.getByTestId('ipa-display')).toHaveText('pæt')
    await expect(page.getByTestId('play-reference')).toBeEnabled()
    await expect(page.getByTestId('sagittal-view').locator('svg')).toBeVisible()
  })

  test('record shows CPU device banner after analyze', async ({ page }) => {
    await page.getByTestId('load-reference').click()
    await expect(page.getByTestId('ipa-display')).toHaveText('pæt')
    await page.getByTestId('step-record').click()
    const banner = page.getByTestId('device-banner')
    await expect(banner).not.toHaveClass(/is-visible/)

    await page.getByTestId('record-start').click()
    await page.getByTestId('record-stop').click()
    await page.getByTestId('analyze-submit').click()

    await expect(banner).toHaveClass(/is-visible/)
    await expect(banner).toContainText('CPU')
    await expect(page.getByTestId('play-user')).toBeEnabled()
  })

  test('compare ghost overlay and coaching', async ({ page }) => {
    await page.getByTestId('load-reference').click()
    await page.getByTestId('step-record').click()
    await page.getByTestId('record-start').click()
    await page.getByTestId('record-stop').click()
    await page.getByTestId('analyze-submit').click()

    await page.getByTestId('step-compare').click()
    await expect(page.getByTestId('ghost-overlay')).toBeVisible()
    await expect(page.getByTestId('compare-sagittal').locator('.sagittal-ghost svg')).toBeVisible()
    await expect(page.getByTestId('compare-sagittal').locator('.sagittal-user svg')).toBeVisible()

    await page.getByTestId('run-compare').click()
    await expect(page.getByTestId('coaching-list').getByTestId('coaching-item')).toContainText(
      'voiceless',
    )
  })

  test('navigates all step tabs', async ({ page }) => {
    for (const id of ['step-practice', 'step-record', 'step-compare']) {
      await page.getByTestId(id).click()
      await expect(page.getByTestId(id)).toHaveClass(/is-active/)
    }
  })
})
