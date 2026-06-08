// @ts-check
import { spawn } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const clientDir = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(clientDir, '..', '..')
const apiProject = path.join(repoRoot, 'src', 'VoiceRay.Api', 'VoiceRay.Api.fsproj')
const statePath = path.join(clientDir, '.integration-servers.json')

/**
 * @param {string} url
 * @param {number} timeoutMs
 */
async function waitForUrl(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url)
      if (response.ok) return
    } catch {
      // retry
    }
    await new Promise((resolve) => setTimeout(resolve, 1000))
  }

  throw new Error(`Timed out waiting for ${url}`)
}

export default async function globalSetup() {
  const whisperDevice = process.env.VOICERAY_WHISPER_DEVICE || 'auto'
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: 'http://127.0.0.1:5000',
    VOICERAY_WHISPER_DEVICE: whisperDevice,
  }

  const api = spawn(
    'dotnet',
    ['run', '--project', apiProject, '--no-launch-profile', '--urls', 'http://127.0.0.1:5000'],
    { cwd: repoRoot, env, stdio: 'ignore', shell: true },
  )

  const preview = spawn(
    'npx',
    ['vite', 'preview', '--host', '127.0.0.1', '--port', '4173'],
    { cwd: path.join(repoRoot, 'client'), env, stdio: 'ignore', shell: true },
  )

  await waitForUrl('http://127.0.0.1:5000/api/v1/health', 180_000)
  await waitForUrl('http://127.0.0.1:4173', 60_000)

  fs.writeFileSync(
    statePath,
    JSON.stringify({ apiPid: api.pid, previewPid: preview.pid }, null, 2),
  )
}
