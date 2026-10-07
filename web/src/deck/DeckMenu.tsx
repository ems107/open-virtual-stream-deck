import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { setToken } from '../api'
import { Icon } from '../icons'
import { deckSocket, useConnection } from '../state/connection'
import { useDeck } from '../state/deck'
import { isKeepAwakeActive, keepAwakePreferred, setKeepAwakePreferred } from './keepAwake'

interface DeckMenuProps {
  editMode: boolean
  onToggleEdit: () => void
}

/** Small corner button opening the deck options (profiles, navigation, screen, edit mode). */
export function DeckMenu({ editMode, onToggleEdit }: DeckMenuProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const layout = useDeck((s) => s.layout)
  const server = useConnection((s) => s.server)
  const [keepAwake, setKeepAwake] = useState(keepAwakePreferred())
  const [fullscreen, setFullscreen] = useState(!!document.fullscreenElement)

  const toggleFullscreen = async () => {
    try {
      if (document.fullscreenElement) await document.exitFullscreen()
      else await document.documentElement.requestFullscreen({ navigationUI: 'hide' })
    } catch {
      // Not allowed (e.g. iOS Safari): add to home screen instead.
    }
    setFullscreen(!!document.fullscreenElement)
  }

  const close = () => setOpen(false)

  return (
    <>
      <button className={`deck-menu-button${editMode ? ' active' : ''}`} onClick={() => setOpen(true)} aria-label={t('deck.menu')}>
        <Icon name={editMode ? 'mdi:pencil' : 'mdi:dots-vertical'} />
      </button>
      {open && (
        <div className="sheet-backdrop" onClick={close}>
          <div className="sheet deck-menu" onClick={(e) => e.stopPropagation()}>
            <header className="sheet-header">
              <strong>{server?.deviceName ?? server?.serverName ?? 'OVSD'}</strong>
              <span className="muted">{layout?.profileName}{layout?.pageName ? ` · ${layout.pageName}` : ''}</span>
            </header>

            {layout && layout.profiles.length > 1 && (
              <section>
                <h3>{t('deck.profiles')}</h3>
                <div className="chip-list">
                  {layout.profiles.map((p) => (
                    <button
                      key={p.id}
                      className={`chip${p.id === layout.profileId ? ' active' : ''}`}
                      onClick={() => {
                        deckSocket.send({ type: 'selectProfile', profileId: p.id })
                        close()
                      }}
                    >
                      {p.name}
                    </button>
                  ))}
                </div>
              </section>
            )}

            <section className="menu-grid">
              <MenuButton icon="mdi:home" label={t('deck.home')} onClick={() => { deckSocket.send({ type: 'navigate', target: 'home' }); close() }} />
              <MenuButton icon="mdi:arrow-left" label={t('deck.back')} disabled={!layout?.canGoBack} onClick={() => { deckSocket.send({ type: 'navigate', target: 'back' }); close() }} />
              <MenuButton icon={fullscreen ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'} label={t('deck.fullscreen')} onClick={() => void toggleFullscreen()} />
              <MenuButton
                icon={keepAwake ? 'mdi:monitor-eye' : 'mdi:monitor-off'}
                label={keepAwake ? t('deck.keepAwakeOn') : t('deck.keepAwakeOff')}
                active={keepAwake && isKeepAwakeActive()}
                onClick={() => {
                  setKeepAwakePreferred(!keepAwake)
                  setKeepAwake(!keepAwake)
                }}
              />
              <MenuButton icon="mdi:pencil" label={editMode ? t('deck.editDone') : t('deck.edit')} active={editMode} onClick={() => { onToggleEdit(); close() }} />
              <MenuButton icon="mdi:open-in-new" label={t('deck.openEditor')} onClick={() => { location.href = '/editor' }} />
              <MenuButton icon="mdi:refresh" label={t('deck.reload')} onClick={() => location.reload()} />
              {server?.deviceId && (
                <MenuButton
                  icon="mdi:link-off"
                  label={t('deck.unpair')}
                  onClick={() => {
                    if (!confirm(t('deck.unpairConfirm'))) return
                    setToken(null)
                    location.reload()
                  }}
                />
              )}
            </section>
          </div>
        </div>
      )}
    </>
  )
}

function MenuButton(props: { icon: string; label: string; onClick: () => void; active?: boolean; disabled?: boolean }) {
  return (
    <button className={`menu-item${props.active ? ' active' : ''}`} onClick={props.onClick} disabled={props.disabled}>
      <Icon name={props.icon} />
      <span>{props.label}</span>
    </button>
  )
}
