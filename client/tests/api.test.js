import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { getApiBaseUrl, resolveApiPath, audioSrcFromPayload } from '../src/api/client.js'

describe('getApiBaseUrl', () => {
  it('returns empty string when env is unset (dev proxy)', () => {
    assert.equal(getApiBaseUrl(), '')
  })
})

describe('resolveApiPath', () => {
  it('prefixes relative API paths', () => {
    assert.equal(resolveApiPath('/api/v1/health'), '/api/v1/health')
  })
})

describe('audioSrcFromPayload', () => {
  it('resolves media URLs for dev proxy playback', () => {
    assert.equal(
      audioSrcFromPayload({ audioUrl: '/media/reference/abc.wav' }),
      '/media/reference/abc.wav',
    )
  })
})
