import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import { Field, TemplateInput, Toggle } from '../../components/ui'
import type { ActionDescriptor, ParamDescriptor } from '../../protocol'
import { useOptions } from '../catalog'
import { useEditor } from '../editorStore'
import { HotkeyInput } from './HotkeyInput'

interface ActionParamsFormProps {
  descriptor: ActionDescriptor
  params: Record<string, string>
  onChange: (name: string, value: string) => void
  variables: string[]
}

/** "target=app" or "method=POST,PUT": show a parameter only when another one has one of those values. */
export function isVisible(param: ParamDescriptor, params: Record<string, string>, descriptor: ActionDescriptor): boolean {
  if (!param.showIf) return true
  const [name, values] = param.showIf.split('=')
  const other = descriptor.params.find((p) => p.name === name)
  const current = params[name] ?? other?.default ?? ''
  return values.split(',').includes(current)
}

export function ActionParamsForm({ descriptor, params, onChange, variables }: ActionParamsFormProps) {
  const { t, i18n } = useTranslation()
  if (descriptor.params.length === 0) return <p className="muted small">{t('macro.noParams')}</p>
  return (
    <div className="fields">
      {descriptor.params
        .filter((p) => isVisible(p, params, descriptor))
        .map((param) => {
          const key = `params.${param.name}`
          const label = i18n.exists(key) ? t(key) : param.label
          return (
            <Field key={param.name} label={label + (param.required ? ' *' : '')} hint={param.help ?? undefined}>
              <ParamInput param={param} value={params[param.name] ?? param.default ?? ''} onChange={(v) => onChange(param.name, v)} variables={variables} />
            </Field>
          )
        })}
    </div>
  )
}

function ParamInput(props: { param: ParamDescriptor; value: string; onChange: (value: string) => void; variables: string[] }) {
  const { param, value, onChange } = props
  const { t } = useTranslation()
  switch (param.type) {
    case 'multilineText':
    case 'json':
      return <TemplateInput value={value} onChange={onChange} multiline monospace={param.type === 'json'} placeholder={param.placeholder ?? undefined} />
    case 'bool':
      return <Toggle checked={value === 'true'} onChange={(c) => onChange(String(c))} label="" />
    case 'hotkey':
      return <HotkeyInput value={value} onChange={onChange} />
    case 'select':
      return <SelectParam {...props} />
    case 'page':
      return <PageSelect value={value} onChange={onChange} />
    case 'profile':
      return <ProfileSelect value={value} onChange={onChange} />
    case 'expression':
      return <input className="mono" value={value} placeholder={param.placeholder ?? 'audio.volume + 5'} onChange={(e) => onChange(e.target.value)} />
    case 'variable':
      return <VariableInput value={value} onChange={onChange} variables={props.variables} placeholder={param.placeholder ?? 'user.name'} />
    case 'number':
      return <TemplateInput value={value} onChange={onChange} placeholder={param.placeholder ?? '0'} variables={props.variables} />
    default:
      return <TemplateInput value={value} onChange={onChange} placeholder={param.placeholder ?? t('macro.templateAllowed')} variables={props.variables} />
  }
}

function SelectParam({ param, value, onChange }: { param: ParamDescriptor; value: string; onChange: (value: string) => void }) {
  const listId = useId()
  const { t, i18n } = useTranslation()
  const dynamic = useOptions(param.optionsSource)
  // Static options get translated labels for common values (toggle/on/off...); dynamic ones are real names.
  const options = param.options
    ? param.options.map((o) => ({ ...o, label: i18n.exists(`optionLabels.${o.value}`) ? t(`optionLabels.${o.value}`) : o.label }))
    : dynamic
  if (param.allowCustom || (param.optionsSource && options.length === 0)) {
    return (
      <>
        <input list={listId} value={value} placeholder={param.placeholder ?? undefined} onChange={(e) => onChange(e.target.value)} />
        <datalist id={listId}>
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </datalist>
      </>
    )
  }
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      {!param.default && <option value="">—</option>}
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  )
}

function PageSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const pages = useEditor((s) => s.profile?.pages ?? [])
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">—</option>
      {pages.map((p) => (
        <option key={p.id} value={p.id}>
          {p.name}
        </option>
      ))}
    </select>
  )
}

function ProfileSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const profiles = useEditor((s) => s.profiles)
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">—</option>
      {profiles.map((p) => (
        <option key={p.id} value={p.id}>
          {p.name}
        </option>
      ))}
    </select>
  )
}

export function VariableInput(props: { value: string; onChange: (value: string) => void; variables: string[]; placeholder?: string }) {
  const listId = useId()
  return (
    <>
      <input className="mono" list={listId} value={props.value} placeholder={props.placeholder} onChange={(e) => props.onChange(e.target.value)} />
      <datalist id={listId}>
        {props.variables.map((v) => (
          <option key={v} value={v} />
        ))}
      </datalist>
    </>
  )
}
