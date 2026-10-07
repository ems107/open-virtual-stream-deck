import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../../icons'

const MODIFIERS: Record<string, string> = {
  ControlLeft: 'Ctrl', ControlRight: 'Ctrl', ShiftLeft: 'Shift', ShiftRight: 'Shift',
  AltLeft: 'Alt', AltRight: 'Alt', MetaLeft: 'Win', MetaRight: 'Win', OSLeft: 'Win', OSRight: 'Win',
}

const NAMED: Record<string, string> = {
  Escape: 'Esc', Enter: 'Enter', NumpadEnter: 'Enter', Tab: 'Tab', Space: 'Space', Backspace: 'Backspace',
  Delete: 'Delete', Insert: 'Insert', Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown',
  ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right', PrintScreen: 'PrintScreen',
  ScrollLock: 'ScrollLock', Pause: 'Pause', CapsLock: 'CapsLock', NumLock: 'NumLock', ContextMenu: 'Menu',
  NumpadAdd: 'NumAdd', NumpadSubtract: 'NumSubtract', NumpadMultiply: 'NumMultiply', NumpadDivide: 'NumDivide',
  NumpadDecimal: 'NumDecimal', Minus: 'Minus', Equal: 'Equals', Comma: 'Comma', Period: 'Period',
  Semicolon: 'Semicolon', Quote: 'Quote', Slash: 'Slash', Backslash: 'Backslash', BracketLeft: 'BracketLeft',
  BracketRight: 'BracketRight', Backquote: 'Backquote', IntlBackslash: 'IntlBackslash',
  MediaPlayPause: 'MediaPlayPause', MediaTrackNext: 'MediaNext', MediaTrackPrevious: 'MediaPrevious',
  MediaStop: 'MediaStop', AudioVolumeUp: 'VolumeUp', AudioVolumeDown: 'VolumeDown', AudioVolumeMute: 'VolumeMute',
}

/** Maps KeyboardEvent.code (physical key) to the server's key names; null for unsupported keys. */
export function keyName(code: string): string | null {
  if (MODIFIERS[code]) return MODIFIERS[code]
  if (NAMED[code]) return NAMED[code]
  let m = /^Key([A-Z])$/.exec(code)
  if (m) return m[1]
  m = /^Digit(\d)$/.exec(code)
  if (m) return m[1]
  m = /^Numpad(\d)$/.exec(code)
  if (m) return `Num${m[1]}`
  m = /^F(\d{1,2})$/.exec(code)
  if (m && Number(m[1]) >= 1 && Number(m[1]) <= 24) return code
  return null
}

/** Builds "Ctrl+Shift+K" from a keydown event; null while only modifiers are held. */
export function chordFromEvent(e: Pick<KeyboardEvent, 'code' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>): string | null {
  const key = keyName(e.code)
  if (!key || Object.values(MODIFIERS).includes(key)) return null
  const parts: string[] = []
  if (e.ctrlKey) parts.push('Ctrl')
  if (e.shiftKey) parts.push('Shift')
  if (e.altKey) parts.push('Alt')
  if (e.metaKey) parts.push('Win')
  parts.push(key)
  return parts.join('+')
}

/** Text field plus a "record" button that captures the next key combination. */
export function HotkeyInput(props: { value: string; onChange: (value: string) => void }) {
  const { t } = useTranslation()
  const [recording, setRecording] = useState(false)

  useEffect(() => {
    if (!recording) return
    const onKey = (e: KeyboardEvent) => {
      e.preventDefault()
      e.stopPropagation()
      if (e.code === 'Escape' && !e.ctrlKey && !e.altKey && !e.shiftKey) {
        setRecording(false)
        return
      }
      const chord = chordFromEvent(e)
      if (chord) {
        props.onChange(chord)
        setRecording(false)
      }
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [recording, props])

  return (
    <div className="hotkey-input">
      <input value={props.value} placeholder="Ctrl+Shift+M" className="mono" onChange={(e) => props.onChange(e.target.value)} />
      <button type="button" className={recording ? 'recording' : ''} onClick={() => setRecording(!recording)}>
        <Icon name={recording ? 'mdi:record-circle' : 'mdi:keyboard'} /> {recording ? t('hotkey.press') : t('hotkey.record')}
      </button>
    </div>
  )
}
