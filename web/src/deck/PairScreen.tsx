import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { claimPairing, guessDeviceName } from '../api'
import { Icon } from '../icons'
import { deckSocket, useConnection } from '../state/connection'

/** Shown when the server does not know this device: enter the PIN displayed on the PC. */
export function PairScreen({ initialError }: { initialError: string | null }) {
  const { t } = useTranslation()
  const [code, setCode] = useState('')
  const [name, setName] = useState(guessDeviceName())
  const [error, setError] = useState(initialError)
  const [busy, setBusy] = useState(false)

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await claimPairing(code, name)
      useConnection.setState({ status: 'connecting' })
      deckSocket.reconnectNow()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="pair-screen">
      <form className="card pair-card" onSubmit={submit}>
        <Icon name="mdi:link-variant" className="pair-icon" />
        <h1>{t('pairScreen.title')}</h1>
        <p className="muted">{t('pairScreen.instructions')}</p>
        <input
          className="pin-input"
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="\d{6}"
          maxLength={6}
          placeholder="000000"
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
          autoFocus
        />
        <label className="field">
          <span>{t('pairScreen.deviceName')}</span>
          <input value={name} onChange={(e) => setName(e.target.value)} maxLength={60} />
        </label>
        {error && <p className="error-text">{error}</p>}
        <button className="primary" disabled={code.length !== 6 || busy}>
          {t('pairScreen.connect')}
        </button>
      </form>
    </main>
  )
}
