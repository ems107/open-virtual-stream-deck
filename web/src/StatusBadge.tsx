import { useTranslation } from 'react-i18next'
import { useConnection } from './state/connection'

export function StatusBadge() {
  const { t } = useTranslation()
  const { status, server, lastError } = useConnection()
  return (
    <div className={`status status-${status}`} title={lastError ?? undefined}>
      <span className="status-dot" />
      {t(`status.${status}`, { server: server?.serverName })}
    </div>
  )
}
