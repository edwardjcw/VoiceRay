import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { getApiBaseUrl } from '../src/api/client.js'

describe('getApiBaseUrl', () => {
  it('returns empty string when env is unset (dev proxy)', () => {
    assert.equal(getApiBaseUrl(), '')
  })
})
