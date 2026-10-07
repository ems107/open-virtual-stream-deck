import { Icon } from '../icons'
import { useDeck } from '../state/deck'

const ICONS = {
  info: 'mdi:information',
  success: 'mdi:check-circle',
  warning: 'mdi:alert',
  error: 'mdi:alert-circle',
} as const

export function Toasts() {
  const toasts = useDeck((s) => s.toasts)
  const dismiss = useDeck((s) => s.dismiss)
  return (
    <div className="toasts" aria-live="polite">
      {toasts.map((t) => (
        <button key={t.id} className={`toast toast-${t.level}`} onClick={() => dismiss(t.id)}>
          <Icon name={ICONS[t.level]} />
          <span>{t.message}</span>
        </button>
      ))}
    </div>
  )
}
