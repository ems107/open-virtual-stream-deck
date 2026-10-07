import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Modal } from '../components/ui'
import { getLoadedSet, Icon, ICON_SETS, loadIconSet, searchIcons } from '../icons'

const RESULT_LIMIT = 300

export function IconPicker(props: { value: string | null | undefined; onChange: (icon: string | null) => void }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  return (
    <div className="icon-field">
      <button type="button" className="icon-preview" onClick={() => setOpen(true)} title={t('icons.choose')}>
        {props.value ? <Icon name={props.value} /> : <Icon name="mdi:image-off-outline" className="muted" />}
      </button>
      <input value={props.value ?? ''} placeholder="mdi:play" onChange={(e) => props.onChange(e.target.value || null)} />
      {open && (
        <IconPickerDialog
          onClose={() => setOpen(false)}
          onPick={(icon) => {
            props.onChange(icon)
            setOpen(false)
          }}
        />
      )}
    </div>
  )
}

function IconPickerDialog(props: { onClose: () => void; onPick: (icon: string | null) => void }) {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const [prefix, setPrefix] = useState('mdi')
  const [loaded, setLoaded] = useState(0)

  useEffect(() => {
    if (getLoadedSet(prefix)) return
    void loadIconSet(prefix)?.then(() => setLoaded((n) => n + 1))
  }, [prefix])

  // `loaded` re-runs the search once the set arrives.
  const results = useMemo(() => (loaded >= 0 ? searchIcons(prefix, query, RESULT_LIMIT) : []), [prefix, query, loaded])

  return (
    <Modal title={t('icons.choose')} onClose={props.onClose} wide>
      <div className="icon-search">
        <input autoFocus placeholder={t('icons.search')} value={query} onChange={(e) => setQuery(e.target.value)} />
        <select value={prefix} onChange={(e) => setPrefix(e.target.value)}>
          {ICON_SETS.map((s) => (
            <option key={s} value={s}>
              {s === 'mdi' ? 'Material Design' : 'Lucide'}
            </option>
          ))}
        </select>
        <button type="button" onClick={() => props.onPick(null)}>
          {t('icons.none')}
        </button>
      </div>
      {!getLoadedSet(prefix) ? (
        <p className="muted">{t('common.loading')}</p>
      ) : (
        <div className="icon-grid">
          {results.map((name) => (
            <button key={name} type="button" title={name} onClick={() => props.onPick(name)}>
              <Icon name={name} />
            </button>
          ))}
          {results.length === RESULT_LIMIT && <p className="muted icon-more">{t('icons.refine')}</p>}
        </div>
      )}
    </Modal>
  )
}
