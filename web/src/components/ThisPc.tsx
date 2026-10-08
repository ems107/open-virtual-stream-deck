import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import { Icon } from '../icons'
import type { ServerInfo } from '../protocol'
import { StatusDot } from './ui'

// /api/system (this PC only). Mirrors OVSD.Host.Api.SystemEndpoints.
interface NetworkProfile {
  name: string
  interfaceAlias: string
  interfaceIndex: number
  category: 'public' | 'private' | 'domain' | 'unknown'
}
interface SystemStatus {
  lan: { listening: boolean; firewallAllowed: boolean; mode: string; network: NetworkProfile | null }
  sensors: { service: 'notInstalled' | 'stopped' | 'running'; driverInstalled?: boolean | null; cpuTemperature?: number | null; cpuName?: string | null; error?: string | null }
  elevated: boolean
}
interface SetupResult {
  ok: boolean
  cancelled: boolean
  error: string | null
  restarting: boolean
}

/** Waits for the server to come back after a restart, then reloads the page. */
async function reloadWhenBack() {
  await new Promise((r) => setTimeout(r, 1500))
  for (let i = 0; i < 40; i++) {
    try {
      const info = await api.get<ServerInfo>('/api/server')
      if (info.lanReady) break
    } catch {
      // still restarting
    }
    await new Promise((r) => setTimeout(r, 500))
  }
  location.reload()
}

/** Runs the elevated LAN setup (one UAC prompt on the PC) and restarts the server when needed. */
function useLanSetup() {
  const [state, setState] = useState<'idle' | 'working' | 'restarting'>('idle')
  const [message, setMessage] = useState<string | null>(null)
  const { t } = useTranslation()
  const run = async () => {
    setState('working')
    setMessage(null)
    try {
      const result = await api.post<SetupResult>('/api/system/lan')
      if (result.restarting) {
        setState('restarting')
        await reloadWhenBack()
        return
      }
      if (result.cancelled) setMessage(t('system.cancelled'))
      else if (!result.ok) setMessage(result.error ?? t('system.failed'))
    } catch (e) {
      setMessage((e as Error).message)
    }
    setState('idle')
  }
  return { state, message, run }
}

/** Shown on the editor and the pairing page while other devices can't reach OVSD yet. */
export function LanBanner() {
  const { t } = useTranslation()
  const [server, setServer] = useState<ServerInfo | null>(null)
  const setup = useLanSetup()
  useEffect(() => {
    void api.get<ServerInfo>('/api/server').then(setServer).catch(() => {})
  }, [])
  if (!server?.isLocal || server.lanReady) return null
  return (
    <div className="banner warning lan-banner">
      <Icon name="mdi:wall-fire" />
      <span>{t('system.lanBlocked')}</span>
      <button className="primary" disabled={setup.state !== 'idle'} onClick={() => void setup.run()}>
        {setup.state === 'restarting' ? t('system.restarting') : setup.state === 'working' ? t('system.waitingUac') : t('system.allowLan')}
      </button>
      {setup.message && <span className="muted small">{setup.message}</span>}
    </div>
  )
}

/** Settings card: network access and the CPU sensor service. */
export function ThisPcCard() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<SystemStatus | null>(null)
  const [sensorsBusy, setSensorsBusy] = useState(false)
  const [sensorsMessage, setSensorsMessage] = useState<string | null>(null)
  const lan = useLanSetup()

  const refresh = useCallback(() => void api.get<SystemStatus>('/api/system').then(setStatus).catch(() => {}), [])
  useEffect(() => {
    refresh()
    const timer = window.setInterval(refresh, 3000)
    return () => window.clearInterval(timer)
  }, [refresh])

  if (!status) return null
  const network = status.lan.network
  const publicNetwork = network?.category === 'public'
  const lanOk = status.lan.listening && status.lan.firewallAllowed && !publicNetwork

  const setSensors = async (enabled: boolean) => {
    setSensorsBusy(true)
    setSensorsMessage(enabled ? t('system.sensorsInstalling') : null)
    try {
      const result = await api.post<SetupResult>('/api/system/sensors', { enabled })
      setSensorsMessage(result.cancelled ? t('system.cancelled') : result.ok ? null : (result.error ?? t('system.failed')))
    } catch (e) {
      setSensorsMessage((e as Error).message)
    }
    setSensorsBusy(false)
    refresh()
  }

  const sensors = status.sensors
  const sensorsRunning = sensors.service === 'running'
  const temperature = sensors.cpuTemperature

  return (
    <section className="card this-pc">
      <h2>{t('system.title')}</h2>

      <div className="system-row">
        <StatusDot state={lanOk ? 'ok' : 'warn'} />
        <div className="system-text">
          <strong>{t('system.lan')}</strong>
          <span className="muted small">
            {status.lan.mode === 'local'
              ? t('system.lanLocalMode')
              : !status.lan.firewallAllowed || !status.lan.listening
                ? t('system.lanBlockedShort')
                : publicNetwork
                  ? t('system.publicNetwork', { name: network!.name })
                  : t('system.lanReady', { name: network?.name ?? '' })}
          </span>
          {lan.message && <span className="muted small">{lan.message}</span>}
        </div>
        {status.lan.mode !== 'local' && !lanOk && (
          <button className="primary" disabled={lan.state !== 'idle'} onClick={() => void lan.run()}>
            <Icon name="mdi:shield-check" />{' '}
            {lan.state === 'restarting' ? t('system.restarting') : lan.state === 'working' ? t('system.waitingUac') : publicNetwork && status.lan.firewallAllowed ? t('system.makePrivate') : t('system.allowLan')}
          </button>
        )}
      </div>

      <div className="system-row">
        <StatusDot state={sensorsRunning && temperature != null ? 'ok' : sensorsRunning ? 'warn' : 'off'} />
        <div className="system-text">
          <strong>{t('system.cpuTemp')}</strong>
          <span className="muted small">
            {sensorsRunning
              ? temperature != null
                ? t('system.sensorsOk', { temp: temperature.toFixed(0), cpu: sensors.cpuName ?? '' })
                : (sensors.error ?? (sensors.driverInstalled === false ? t('system.sensorsNoDriver') : t('system.sensorsStarting')))
              : sensors.service === 'stopped'
                ? t('system.sensorsStopped')
                : t('system.sensorsOff')}
          </span>
          {sensorsMessage && <span className="muted small">{sensorsMessage}</span>}
        </div>
        {sensors.service === 'notInstalled' ? (
          <button disabled={sensorsBusy} onClick={() => void setSensors(true)}>
            <Icon name="mdi:thermometer" /> {sensorsBusy ? t('system.waitingUac') : t('system.enable')}
          </button>
        ) : (
          <button disabled={sensorsBusy} onClick={() => void setSensors(false)}>
            {sensorsBusy ? t('system.waitingUac') : t('system.disable')}
          </button>
        )}
      </div>
      <p className="muted small">{t('system.sensorsHelp')}</p>
    </section>
  )
}
