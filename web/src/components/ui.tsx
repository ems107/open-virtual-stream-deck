import { useEffect, useId, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../icons'

export function Modal(props: { title: string; onClose: () => void; children: ReactNode; wide?: boolean; footer?: ReactNode }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && props.onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [props])
  return (
    <div className="modal-backdrop" onPointerDown={(e) => e.target === e.currentTarget && props.onClose()}>
      <div className={`modal${props.wide ? ' wide' : ''}`} role="dialog" aria-label={props.title}>
        <header className="modal-header">
          <h2>{props.title}</h2>
          <button className="icon-button" onClick={props.onClose} aria-label="close">
            <Icon name="mdi:close" />
          </button>
        </header>
        <div className="modal-body">{props.children}</div>
        {props.footer && <footer className="modal-footer">{props.footer}</footer>}
      </div>
    </div>
  )
}

export function Field(props: { label: string; children: ReactNode; hint?: ReactNode; inline?: boolean }) {
  return (
    <label className={`field${props.inline ? ' inline' : ''}`}>
      <span className="field-label">{props.label}</span>
      {props.children}
      {props.hint && <small className="field-hint">{props.hint}</small>}
    </label>
  )
}

export function Segmented<T extends string>(props: {
  value: T | null | undefined
  options: { value: T; label: string; icon?: string }[]
  onChange: (value: T) => void
  allowNone?: boolean
}) {
  return (
    <div className="segmented" role="radiogroup">
      {props.options.map((o) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={props.value === o.value}
          className={props.value === o.value ? 'active' : ''}
          title={o.label}
          onClick={() => props.onChange(o.value)}
        >
          {o.icon ? <Icon name={o.icon} /> : o.label}
        </button>
      ))}
    </div>
  )
}

/** Color picker plus free text (so "transparent" or templates still work). Empty means inherited. */
export function ColorField(props: { label: string; value: string | null | undefined; onChange: (value: string | null) => void; placeholder?: string }) {
  const { t } = useTranslation()
  const value = props.value ?? ''
  const isHex = /^#[0-9a-f]{6}$/i.test(value)
  return (
    <Field label={props.label}>
      <div className="color-field">
        <input type="color" value={isHex ? value : '#000000'} onChange={(e) => props.onChange(e.target.value)} />
        <input value={value} placeholder={props.placeholder ?? t('common.inherited')} onChange={(e) => props.onChange(e.target.value || null)} />
        {value && (
          <button type="button" className="icon-button" onClick={() => props.onChange(null)} title={t('common.clear')}>
            <Icon name="mdi:close" />
          </button>
        )}
      </div>
    </Field>
  )
}

/** Text that may contain {{templates}}: suggests variable names after typing "{{". */
export function TemplateInput(props: {
  value: string | null | undefined
  onChange: (value: string) => void
  placeholder?: string
  multiline?: boolean
  variables?: string[]
  monospace?: boolean
}) {
  const listId = useId()
  const common = {
    value: props.value ?? '',
    placeholder: props.placeholder,
    className: props.monospace ? 'mono' : undefined,
    spellCheck: false,
  }
  return (
    <>
      {props.multiline ? (
        <textarea {...common} rows={2} onChange={(e) => props.onChange(e.target.value)} />
      ) : (
        <input {...common} list={props.variables ? listId : undefined} onChange={(e) => props.onChange(e.target.value)} />
      )}
      {props.variables && !props.multiline && (
        <datalist id={listId}>
          {props.variables.map((v) => (
            <option key={v} value={`{{${v}}}`} />
          ))}
        </datalist>
      )}
    </>
  )
}

export function Toggle(props: { checked: boolean; onChange: (checked: boolean) => void; label: string }) {
  return (
    <label className="toggle">
      <input type="checkbox" checked={props.checked} onChange={(e) => props.onChange(e.target.checked)} />
      <span className="toggle-track" />
      <span>{props.label}</span>
    </label>
  )
}

export function NumberInput(props: { value: number | null | undefined; onChange: (value: number) => void; min?: number; max?: number; step?: number }) {
  return (
    <input
      type="number"
      value={props.value ?? ''}
      min={props.min}
      max={props.max}
      step={props.step ?? 1}
      onChange={(e) => {
        const n = e.target.valueAsNumber
        if (!Number.isNaN(n)) props.onChange(n)
      }}
    />
  )
}

export function StatusDot({ state }: { state: 'ok' | 'warn' | 'bad' | 'off' }) {
  return <span className={`status-dot dot-${state}`} />
}
