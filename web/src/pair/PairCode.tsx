import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import type { PairInfo } from '../protocol'

/** QR code + PIN for pairing a device. Only works from this PC (the server refuses remote requests). */
export function PairCode() {
  const { t } = useTranslation()
  const [info, setInfo] = useState<PairInfo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [url, setUrl] = useState<string | null>(null)
  const [now, setNow] = useState(Date.now())

  const create = useCallback(async () => {
    try {
      const pair = await api.post<PairInfo>('/api/pair/new')
      setInfo(pair)
      setUrl((current) => pair.urls.find((u) => current && u.startsWith(current.split('?')[0])) ?? pair.url)
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    }
  }, [])

  useEffect(() => {
    void create()
    const tick = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(tick)
  }, [create])

  const remaining = info ? Math.max(0, Math.round((new Date(info.expiresAt).getTime() - now) / 1000)) : 0
  useEffect(() => {
    if (info && remaining === 0) void create()
  }, [info, remaining, create])

  if (error) return <p className="error-text">{error}</p>
  if (!info || !url) return <p className="muted">{t('common.loading')}</p>

  return (
    <div className="pair-code">
      <p>{t('pair.instructions')}</p>
      <img className="qr" src={`/api/qr.png?text=${encodeURIComponent(url)}`} alt="QR" width={260} height={260} />
      <div className="pin" aria-label="PIN">
        {info.code.slice(0, 3)} {info.code.slice(3)}
      </div>
      <p className="muted small">{t('pair.expires', { seconds: remaining })}</p>
      <p className="small">{t('pair.manual')}</p>
      {info.urls.length > 1 ? (
        <select value={url} onChange={(e) => setUrl(e.target.value)}>
          {info.urls.map((u) => (
            <option key={u} value={u}>
              {u.split('?')[0]}
            </option>
          ))}
        </select>
      ) : (
        <code>{url.split('?')[0]}</code>
      )}
      <p className="muted small">{t('pair.firewall')}</p>
    </div>
  )
}
