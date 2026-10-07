import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import { Icon } from '../icons'

/** Live table of every variable, to build templates and conditions. */
export function VariablesPanel() {
  const { t } = useTranslation()
  const [variables, setVariables] = useState<Record<string, unknown>>({})
  const [filter, setFilter] = useState('')
  const [copied, setCopied] = useState<string | null>(null)

  useEffect(() => {
    const load = () => void api.get<Record<string, unknown>>('/api/variables').then(setVariables).catch(() => {})
    load()
    const timer = window.setInterval(load, 1000)
    return () => window.clearInterval(timer)
  }, [])

  const rows = useMemo(() => {
    const q = filter.toLowerCase()
    return Object.entries(variables).filter(([name, value]) => !q || name.toLowerCase().includes(q) || String(value).toLowerCase().includes(q))
  }, [variables, filter])

  const copy = async (name: string) => {
    const text = /^[\w.]+$/.test(name) ? `{{${name}}}` : `{{[${name}]}}`
    try {
      await navigator.clipboard.writeText(text)
      setCopied(name)
      setTimeout(() => setCopied(null), 1200)
    } catch {
      // Clipboard needs a secure context; ignore over plain HTTP from other devices.
    }
  }

  return (
    <div className="panel">
      <header className="panel-header">
        <h1>{t('variables.title')}</h1>
        <input className="search" placeholder={t('variables.filter')} value={filter} onChange={(e) => setFilter(e.target.value)} />
      </header>
      <p className="muted">{t('variables.help')}</p>
      <table className="variables">
        <thead>
          <tr>
            <th>{t('common.name')}</th>
            <th>{t('variables.value')}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map(([name, value]) => (
            <tr key={name}>
              <td className="mono">{name}</td>
              <td className="value">{formatValue(value)}</td>
              <td>
                <button className="icon-button" title={t('variables.copy')} onClick={() => void copy(name)}>
                  <Icon name={copied === name ? 'mdi:check' : 'mdi:content-copy'} />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return '—'
  const text = typeof value === 'string' ? value : JSON.stringify(value)
  return text.length > 160 ? text.slice(0, 160) + '…' : text
}
