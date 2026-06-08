// @ts-check
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export default async function globalTeardown() {
  const statePath = path.join(path.dirname(fileURLToPath(import.meta.url)), '.integration-servers.json')

  if (!fs.existsSync(statePath)) return

  const state = JSON.parse(fs.readFileSync(statePath, 'utf8'))

  for (const pid of [state.apiPid, state.previewPid]) {
    if (!pid) continue
    try {
      process.kill(pid, 'SIGTERM')
    } catch {
      // already stopped
    }
  }

  fs.rmSync(statePath, { force: true })
}
