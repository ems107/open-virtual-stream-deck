import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { claimPairing, guessDeviceName } from '../api'
import { Toasts } from '../components/Toasts'
import { deckSocket, useConnection } from '../state/connection'
import { useDeck } from '../state/deck'
import { DeckGrid } from './DeckGrid'
import { DeckMenu } from './DeckMenu'
import { enterFullscreen, fullscreenPreferred, fullscreenSupported, setFullscreenPreferred, useIsFullscreen } from './fullscreen'
import { Icon } from '../icons'
import { enableKeepAwake } from './keepAwake'
import { PairScreen } from './PairScreen'
import { QuickEdit, type QuickEditTarget } from './QuickEdit'

export function DeckPage() {
  const { t } = useTranslation()
  const status = useConnection((s) => s.status)
  const layout = useDeck((s) => s.layout)
  const [editMode, setEditMode] = useState(false)
  const [editing, setEditing] = useState<QuickEditTarget | null>(null)
  const [pairError, setPairError] = useState<string | null>(null)
  const fullscreen = useIsFullscreen()
  const [dismissed, setDismissed] = useState(false)
  useEffect(() => {
    if (fullscreen) setDismissed(false)
  }, [fullscreen])
  const resumeFullscreen =
    !fullscreen && !dismissed && !editing && status === 'connected' && fullscreenSupported() && fullscreenPreferred()

  // Opened from the QR code: /?pair=123456 → claim a token and reconnect as this device.
  useEffect(() => {
    const params = new URLSearchParams(location.search)
    const code = params.get('pair')
    if (!code) return
    history.replaceState(null, '', location.pathname)
    claimPairing(code, guessDeviceName())
      .then(() => {
        useConnection.setState({ status: 'connecting' })
        deckSocket.reconnectNow()
      })
      .catch((e: Error) => setPairError(e.message))
  }, [])

  if (status === 'unpaired') return <PairScreen initialError={pairError} />

  return (
    <main
      className="deck-page"
      style={{ background: layout?.theme.background }}
      onPointerDown={enableKeepAwake}
    >
      {layout && layout.rows > 0 ? (
        <DeckGrid
          layout={layout}
          editMode={editMode}
          onEditTile={(controlId) => setEditing({ controlId })}
          onAddAt={(row, col) => setEditing({ row, col })}
        />
      ) : (
        <div className="deck-empty">{status === 'connected' ? t('deck.empty') : t(`status.${status}`)}</div>
      )}

      {editMode && <div className="edit-banner">{t('deck.editModeHint')}</div>}
      <DeckMenu editMode={editMode} onToggleEdit={() => setEditMode((v) => !v)} />
      {status !== 'connected' && layout && <div className="disconnected-overlay">{t(`status.${status}`)}</div>}
      {editing && layout && <QuickEdit layout={layout} target={editing} onClose={() => setEditing(null)} />}
      {resumeFullscreen && (
        // A full-screen catcher, so the tap that restores fullscreen can't also press a button.
        <div className="fullscreen-resume" onClick={() => void enterFullscreen(layout ?? undefined).catch(() => setDismissed(true))}>
          <button className="primary">
            <Icon name="mdi:fullscreen" /> {t('deck.resumeFullscreen')}
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation()
              setFullscreenPreferred(false)
              setDismissed(true)
            }}
          >
            {t('deck.stayWindowed')}
          </button>
        </div>
      )}
      <Toasts />
    </main>
  )
}
