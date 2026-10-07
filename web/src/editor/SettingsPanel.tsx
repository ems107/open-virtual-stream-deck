import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import { Field, NumberInput, StatusDot, Toggle } from '../components/ui'
import { Icon } from '../icons'
import type { AppSettings, IntegrationStatus, ProfileListItem, ServerInfo } from '../protocol'

export function SettingsPanel() {
  const { t, i18n } = useTranslation()
  const [settings, setSettings] = useState<AppSettings | null>(null)
  const [saved, setSaved] = useState<AppSettings | null>(null)
  const [statuses, setStatuses] = useState<IntegrationStatus[]>([])
  const [profiles, setProfiles] = useState<ProfileListItem[]>([])
  const [server, setServer] = useState<ServerInfo | null>(null)
  const [autostart, setAutostart] = useState<boolean | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void api.get<AppSettings>('/api/settings').then((s) => {
      setSettings(s)
      setSaved(s)
    })
    void api.get<ProfileListItem[]>('/api/profiles').then(setProfiles)
    void api.get<ServerInfo>('/api/server').then((info) => {
      setServer(info)
      if (info.isLocal) void api.get<{ enabled: boolean }>('/api/autostart').then((a) => setAutostart(a.enabled))
    })
    const poll = () => void api.get<IntegrationStatus[]>('/api/integrations').then(setStatuses).catch(() => {})
    poll()
    const timer = window.setInterval(poll, 2000)
    return () => window.clearInterval(timer)
  }, [])

  if (!settings) return <p className="muted panel">{t('common.loading')}</p>
  const dirty = JSON.stringify(settings) !== JSON.stringify(saved)
  const patch = (mutate: (s: AppSettings) => void) =>
    setSettings((current) => {
      const next = structuredClone(current!)
      mutate(next)
      return next
    })

  const save = async () => {
    try {
      const result = await api.put<AppSettings>('/api/settings', settings)
      setSettings(result)
      setSaved(result)
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const status = (id: string) => statuses.find((s) => s.id === id)
  const hookUrl = `${server?.url ?? location.origin + '/'}api/hook/NAME?key=${settings.webhookKey}`

  return (
    <div className="panel settings">
      <header className="panel-header">
        <h1>{t('settings.title')}</h1>
        <button className="primary" disabled={!dirty} onClick={() => void save()}>
          <Icon name="mdi:content-save" /> {t('common.save')}
        </button>
      </header>
      {error && <div className="banner error">{error}</div>}

      <section className="card">
        <h2>{t('settings.general')}</h2>
        <Field label={t('settings.language')}>
          <select
            value={i18n.language}
            onChange={(e) => {
              void i18n.changeLanguage(e.target.value)
              try {
                localStorage.setItem('ovsd.lang', e.target.value)
              } catch {
                // ignore
              }
            }}
          >
            <option value="es">Español</option>
            <option value="en">English</option>
          </select>
        </Field>
        <Field label={t('settings.defaultProfile')} hint={t('settings.defaultProfileHint')}>
          <select value={settings.defaultProfileId ?? ''} onChange={(e) => patch((s) => void (s.defaultProfileId = e.target.value || null))}>
            <option value="">{t('settings.firstProfile')}</option>
            {profiles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </Field>
        {autostart !== null && (
          <Toggle
            checked={autostart}
            label={t('settings.autostart')}
            onChange={async (enabled) => setAutostart((await api.put<{ enabled: boolean }>('/api/autostart', { enabled })).enabled)}
          />
        )}
      </section>

      <section className="card">
        <h2>{t('settings.deck')}</h2>
        <div className="field-row">
          <Field label={t('settings.longPressMs')}>
            <NumberInput value={settings.deck.longPressMs} min={150} max={3000} onChange={(v) => patch((s) => void (s.deck.longPressMs = v))} />
          </Field>
          <Field label={t('settings.doubleTapMs')}>
            <NumberInput value={settings.deck.doubleTapMs} min={100} max={1000} onChange={(v) => patch((s) => void (s.deck.doubleTapMs = v))} />
          </Field>
        </div>
        <Toggle checked={settings.deck.haptics} label={t('settings.haptics')} onChange={(v) => patch((s) => void (s.deck.haptics = v))} />
      </section>

      <section className="card">
        <h2>{t('settings.metrics')}</h2>
        <Toggle checked={settings.metrics.gpuSensors} label={t('settings.gpuSensors')} onChange={(v) => patch((s) => void (s.metrics.gpuSensors = v))} />
        <Toggle checked={settings.metrics.cpuSensors} label={t('settings.cpuSensors')} onChange={(v) => patch((s) => void (s.metrics.cpuSensors = v))} />
        <p className="muted small">{t('settings.cpuSensorsHint')}</p>
      </section>

      <section className="card">
        <IntegrationHeader title="OBS Studio" status={status('obs')} />
        <Toggle checked={settings.obs.enabled} label={t('settings.enabled')} onChange={(v) => patch((s) => void (s.obs.enabled = v))} />
        <Field label="URL" hint={t('settings.obsHint')}>
          <input value={settings.obs.url} onChange={(e) => patch((s) => void (s.obs.url = e.target.value))} />
        </Field>
        <Field label={t('settings.password')}>
          <input type="password" value={settings.obs.password ?? ''} onChange={(e) => patch((s) => void (s.obs.password = e.target.value || null))} />
        </Field>
      </section>

      <section className="card">
        <IntegrationHeader title="MQTT / Home Assistant" status={status('mqtt')} />
        <Toggle checked={settings.mqtt.enabled} label={t('settings.enabled')} onChange={(v) => patch((s) => void (s.mqtt.enabled = v))} />
        <div className="field-row">
          <Field label={t('settings.host')}>
            <input value={settings.mqtt.host} onChange={(e) => patch((s) => void (s.mqtt.host = e.target.value))} />
          </Field>
          <Field label={t('settings.port')}>
            <NumberInput value={settings.mqtt.port} min={1} max={65535} onChange={(v) => patch((s) => void (s.mqtt.port = v))} />
          </Field>
        </div>
        <div className="field-row">
          <Field label={t('settings.username')}>
            <input value={settings.mqtt.username ?? ''} onChange={(e) => patch((s) => void (s.mqtt.username = e.target.value || null))} />
          </Field>
          <Field label={t('settings.password')}>
            <input type="password" value={settings.mqtt.password ?? ''} onChange={(e) => patch((s) => void (s.mqtt.password = e.target.value || null))} />
          </Field>
        </div>
        <Field label={t('settings.subscriptions')} hint={t('settings.subscriptionsHint')}>
          <textarea
            className="mono"
            rows={3}
            value={settings.mqtt.subscriptions.join('\n')}
            onChange={(e) => patch((s) => void (s.mqtt.subscriptions = e.target.value.split('\n')))}
            onBlur={() => patch((s) => void (s.mqtt.subscriptions = s.mqtt.subscriptions.map((x) => x.trim()).filter(Boolean)))}
          />
        </Field>
      </section>

      <section className="card">
        <IntegrationHeader title="Discord" status={status('discord')} />
        <p className="muted small">{t('settings.discordHelp')}</p>
        <Toggle checked={settings.discord.enabled} label={t('settings.enabled')} onChange={(v) => patch((s) => void (s.discord.enabled = v))} />
        <div className="field-row">
          <Field label="Client ID">
            <input value={settings.discord.clientId ?? ''} onChange={(e) => patch((s) => void (s.discord.clientId = e.target.value || null))} />
          </Field>
          <Field label="Client secret">
            <input type="password" value={settings.discord.clientSecret ?? ''} onChange={(e) => patch((s) => void (s.discord.clientSecret = e.target.value || null))} />
          </Field>
        </div>
        <Field label="Redirect URI">
          <input value={settings.discord.redirectUri ?? ''} onChange={(e) => patch((s) => void (s.discord.redirectUri = e.target.value || null))} />
        </Field>
        {settings.discord.accessToken && (
          <button onClick={() => patch((s) => void (s.discord = { ...s.discord, accessToken: null, refreshToken: null, tokenExpires: null }))}>
            <Icon name="mdi:key-remove" /> {t('settings.discordForget')}
          </button>
        )}
      </section>

      <section className="card">
        <h2>{t('settings.webhooks')}</h2>
        <p className="muted small">{t('settings.webhooksHelp')}</p>
        <code className="block">{hookUrl}</code>
        <button
          onClick={() =>
            patch((s) => void (s.webhookKey = Array.from(crypto.getRandomValues(new Uint8Array(12)), (b) => b.toString(16).padStart(2, '0')).join('')))
          }
        >
          <Icon name="mdi:refresh" /> {t('settings.regenerateKey')}
        </button>
      </section>
    </div>
  )
}

function IntegrationHeader({ title, status }: { title: string; status: IntegrationStatus | undefined }) {
  const { t } = useTranslation()
  const state = status?.state ?? 'disabled'
  const dot = state === 'connected' ? 'ok' : state === 'connecting' ? 'warn' : state === 'error' ? 'bad' : 'off'
  return (
    <div className="integration-header">
      <h2>{title}</h2>
      <span className="integration-status">
        <StatusDot state={dot} /> {t(`integration.${state}`)}
        {status?.message && <span className="muted small"> — {status.message}</span>}
      </span>
    </div>
  )
}
