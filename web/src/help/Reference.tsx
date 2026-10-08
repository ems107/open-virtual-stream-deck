import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'
import { useCatalog } from '../editor/catalog'
import { Icon } from '../icons'
import type { ActionDescriptor } from '../protocol'

// Reference pages generated from what the server actually supports, so they never go out of date.

/** Every built-in action, by category, with its options. */
export function ReferenceActions() {
  const { t, i18n } = useTranslation()
  const { actions, load } = useCatalog()
  useEffect(() => void load(), [load])

  const label = (key: string, fallback: string) => (i18n.exists(key) ? t(key) : fallback)
  const categories = [...new Set(actions.map((a) => a.category))]

  return (
    <>
      <h1>{t('wiki.pages.reference-actions')}</h1>
      <p>{t('wiki.actionsIntro')}</p>
      {categories.map((category) => (
        <section key={category}>
          <h2>{t(`categories.${category}`, { defaultValue: category })}</h2>
          {actions
            .filter((a) => a.category === category)
            .map((action) => (
              <ActionEntry key={action.id} action={action} label={label} />
            ))}
        </section>
      ))}
    </>
  )
}

function ActionEntry({ action, label }: { action: ActionDescriptor; label: (key: string, fallback: string) => string }) {
  const { t } = useTranslation()
  const description = label(`actionHelp.${action.id}`, action.description ?? '')
  return (
    <div className="reference-action">
      <h3>
        {action.icon && <Icon name={action.icon} />} {label(`actions.${action.id}`, action.name)} <code>{action.id}</code>
      </h3>
      {description && <p>{description}</p>}
      {action.params.length > 0 && (
        <table>
          <tbody>
            {action.params.map((param) => (
              <tr key={param.name}>
                <td>{label(`params.${param.name}`, param.label)}</td>
                <td>
                  {param.options?.length
                    ? param.options.map((o) => label(`optionLabels.${o.value}`, o.label)).join(' · ')
                    : param.optionsSource
                      ? t('wiki.paramList')
                      : (param.help ?? (param.placeholder ? `${t('wiki.paramExample')} ${param.placeholder}` : t(`wiki.paramTypes.${param.type}`)))}
                  {param.type === 'expression' && <span className="muted"> ({t('wiki.expression')})</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

/** Functions available in expressions and {{templates}}. */
export function ReferenceFunctions() {
  const { t } = useTranslation()
  const [functions, setFunctions] = useState<string[]>([])
  useEffect(() => {
    void api.get<string[]>('/api/functions').then(setFunctions)
  }, [])
  return (
    <>
      <h1>{t('wiki.pages.reference-functions')}</h1>
      <p>{t('wiki.functionsIntro')}</p>
      <div className="function-list">
        {functions.map((f) => (
          <code key={f}>{f}</code>
        ))}
      </div>
      <h2>{t('wiki.examples')}</h2>
      <div className="code-block">
        <pre>
          <code>{`{{round(sys.cpu)}}%                       → 37%
{{format(sys.ram.used, '0.0')}} GB         → 12.4 GB
{{truncate(media.title, 18)}}              → Bohemian Rhapsody…
{{upper(obs.scene)}}                       → JUEGO
{{default(user.counter, 0)}}               → 0 si aún no existe
{{json(user.weather, 'current.temp')}}     → un campo de una respuesta JSON
{{now('dddd HH:mm')}}                      → jueves 18:42
{{if(sys.cpu > 80, '🔥', '')}}`}</code>
        </pre>
      </div>
    </>
  )
}

/** Key names accepted by keyboard actions. */
export function ReferenceKeys() {
  const { t } = useTranslation()
  const { keys, load } = useCatalog()
  useEffect(() => void load(), [load])
  return (
    <>
      <h1>{t('wiki.pages.reference-keys')}</h1>
      <p>{t('wiki.keysIntro')}</p>
      <div className="function-list">
        {keys.map((k) => (
          <code key={k}>{k}</code>
        ))}
      </div>
    </>
  )
}
