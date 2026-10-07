import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

export function PairPage() {
  const { t } = useTranslation()
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    void fetch('/api/server')
      .then((r) => r.json() as Promise<{ url: string }>)
      .then((info) => setUrl(info.url))
  }, [])

  return (
    <main className="page pair-page">
      <h1>{t('pair.title')}</h1>
      <p>{t('pair.instructions')}</p>
      <img className="qr" src="/api/pair/qr.png" alt="QR" width={300} height={300} />
      {url && <code className="pair-url">{url}</code>}
    </main>
  )
}
