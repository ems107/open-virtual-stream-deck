// Starts the OVSD host for e2e tests: builds nothing itself (run `npm run build` first so the
// embedded client is current), uses a throwaway data directory and no tray icon.
import { spawn } from 'node:child_process'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const port = process.argv[2] ?? '7399'
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const dataDir = mkdtempSync(join(tmpdir(), 'ovsd-e2e-'))

const child = spawn(
  'dotnet',
  ['run', '--project', join(root, 'server/src/OVSD.Host'), '--', '--no-tray', '--Ovsd:DryRun=true', '--Ovsd:Network=lan', `--Ovsd:Port=${port}`, `--Ovsd:DataDir=${dataDir}`],
  { stdio: 'inherit' },
)
const stop = () => child.kill()
process.on('SIGTERM', stop)
process.on('SIGINT', stop)
child.on('exit', (code) => process.exit(code ?? 0))
