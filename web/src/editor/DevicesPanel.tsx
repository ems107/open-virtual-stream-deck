import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import { Modal, StatusDot, Toggle } from '../components/ui'
import { Icon } from '../icons'
import type { DeviceView, ProfileListItem, ServerInfo } from '../protocol'
import { PairCode } from '../pair/PairCode'
import { deckSocket } from '../state/connection'

export function DevicesPanel() {
  const { t } = useTranslation()
  const [devices, setDevices] = useState<DeviceView[]>([])
  const [profiles, setProfiles] = useState<ProfileListItem[]>([])
  const [server, setServer] = useState<ServerInfo | null>(null)
  const [pairing, setPairing] = useState(false)

  const refresh = useCallback(async () => {
    const [d, p] = await Promise.all([api.get<DeviceView[]>('/api/devices'), api.get<ProfileListItem[]>('/api/profiles')])
    setDevices(d)
    setProfiles(p)
  }, [])

  useEffect(() => {
    void refresh()
    void api.get<ServerInfo>('/api/server').then(setServer)
    const timer = window.setInterval(() => void refresh(), 5000)
    const unsubscribe = deckSocket.onConfigChanged(() => void refresh())
    return () => {
      window.clearInterval(timer)
      unsubscribe()
    }
  }, [refresh])

  const change = async (id: string, body: Partial<{ name: string; profileId: string; autoProfile: boolean }>) => {
    await api.put(`/api/devices/${id}`, body)
    await refresh()
  }

  return (
    <div className="panel">
      <header className="panel-header">
        <h1>{t('devices.title')}</h1>
        {server?.isLocal && (
          <button className="primary" onClick={() => setPairing(true)}>
            <Icon name="mdi:qrcode" /> {t('devices.pair')}
          </button>
        )}
      </header>
      <p className="muted">{t('devices.help')}</p>

      {devices.length === 0 ? (
        <div className="card empty">{t('devices.none')}</div>
      ) : (
        <div className="device-list">
          {devices.map((d) => (
            <div className="card device" key={d.id}>
              <div className="device-main">
                <StatusDot state={d.online ? 'ok' : 'off'} />
                <input
                  className="device-name"
                  defaultValue={d.name}
                  onBlur={(e) => e.target.value.trim() && e.target.value !== d.name && void change(d.id, { name: e.target.value.trim() })}
                />
                <span className="muted small">
                  {d.online ? t('devices.online') : d.lastSeen ? t('devices.lastSeen', { date: new Date(d.lastSeen).toLocaleString() }) : t('devices.never')}
                </span>
              </div>
              <div className="device-options">
                <label className="field inline">
                  <span className="field-label">{t('devices.profile')}</span>
                  <select value={d.profileId ?? ''} onChange={(e) => void change(d.id, { profileId: e.target.value })}>
                    <option value="">{t('devices.defaultProfile')}</option>
                    {profiles.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
                <Toggle checked={d.autoProfile} onChange={(autoProfile) => void change(d.id, { autoProfile })} label={t('devices.auto')} />
                <button
                  className="danger"
                  onClick={async () => {
                    if (!confirm(t('devices.removeConfirm', { name: d.name }))) return
                    await api.delete(`/api/devices/${d.id}`)
                    await refresh()
                  }}
                >
                  <Icon name="mdi:link-off" /> {t('devices.remove')}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
      <p className="muted small">{t('devices.autoHelp')}</p>

      {pairing && (
        <Modal title={t('devices.pair')} onClose={() => setPairing(false)}>
          <PairCode />
        </Modal>
      )}
    </div>
  )
}
