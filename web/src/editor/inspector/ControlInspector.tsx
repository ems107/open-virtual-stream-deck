import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ColorField, Field, NumberInput, Segmented, TemplateInput } from '../../components/ui'
import { Icon } from '../../icons'
import type { Appearance, BindingGesture, Control, ControlKind, Step } from '../../protocol'
import { AppearanceFields } from './AppearanceFields'
import { MacroEditor } from './MacroEditor'

type Tab = 'appearance' | 'actions' | 'state' | 'layout'

export interface ControlInspectorProps {
  control: Control
  onChange: (mutate: (control: Control) => void, key?: string) => void
  variables: string[]
  /** Compact mode for the on-device quick editor. */
  compact?: boolean
}

const GESTURES: Record<ControlKind, BindingGesture[]> = {
  button: ['tap', 'longPress', 'doubleTap', 'press', 'release'],
  slider: ['change', 'tap'],
  widget: ['tap', 'longPress', 'doubleTap'],
}

export function ControlInspector({ control, onChange, variables, compact }: ControlInspectorProps) {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('appearance')

  const tabs: { id: Tab; icon: string }[] = [
    { id: 'appearance', icon: 'mdi:palette' },
    { id: 'actions', icon: 'mdi:lightning-bolt' },
    { id: 'state', icon: 'mdi:toggle-switch' },
    { id: 'layout', icon: 'mdi:view-grid' },
  ]

  return (
    <div className={`inspector${compact ? ' compact' : ''}`}>
      <Field label={t('inspector.kind')}>
        <Segmented
          value={control.kind}
          onChange={(kind) => onChange((c) => changeKind(c, kind), 'kind')}
          options={[
            { value: 'button', label: t('kinds.button') },
            { value: 'slider', label: t('kinds.slider') },
            { value: 'widget', label: t('kinds.widget') },
          ]}
        />
      </Field>

      <nav className="tabs">
        {tabs.map((x) => (
          <button key={x.id} type="button" className={tab === x.id ? 'active' : ''} onClick={() => setTab(x.id)}>
            <Icon name={x.icon} />
            <span>{t(`inspector.tabs.${x.id}`)}</span>
          </button>
        ))}
      </nav>

      {tab === 'appearance' && (
        <AppearanceFields
          value={control.appearance}
          variables={variables}
          onChange={(mutate, key) => onChange((c) => mutate(c.appearance), `appearance.${key}`)}
        />
      )}
      {tab === 'actions' && <ActionsTab control={control} onChange={onChange} variables={variables} />}
      {tab === 'state' && <StateTab control={control} onChange={onChange} variables={variables} />}
      {tab === 'layout' && <LayoutTab control={control} onChange={onChange} />}
    </div>
  )
}

function changeKind(control: Control, kind: ControlKind) {
  control.kind = kind
  if (kind === 'slider' && !control.slider) control.slider = { min: 0, max: 100, step: 1, orientation: 'vertical' }
  if (kind === 'widget' && !control.widget) control.widget = { type: 'text', min: 0, max: 100, history: 60 }
}

function ActionsTab({ control, onChange, variables }: Omit<ControlInspectorProps, 'compact'>) {
  const { t } = useTranslation()
  const gestures = GESTURES[control.kind]
  const [open, setOpen] = useState<BindingGesture>(gestures.find((g) => control.bindings[g]?.length) ?? gestures[0])

  return (
    <div className="fields">
      {gestures.map((gesture) => {
        const steps = control.bindings[gesture] ?? []
        const isOpen = open === gesture
        return (
          <section key={gesture} className={`gesture${isOpen ? ' open' : ''}`}>
            <button type="button" className="gesture-header" onClick={() => setOpen(gesture)}>
              <span>{t(`gestures.${gesture}`)}</span>
              <span className="badge">{steps.length || ''}</span>
            </button>
            {isOpen && (
              <>
                <p className="muted small">{t(`gestureHelp.${gesture}`)}</p>
                <MacroEditor
                  steps={steps}
                  variables={variables}
                  onChange={(next: Step[]) => onChange((c) => void (c.bindings[gesture] = next.length ? next : null), `bindings.${gesture}`)}
                />
              </>
            )}
          </section>
        )
      })}
      <Field label={t('inspector.concurrency')} hint={t(`concurrency.${control.concurrency}Help`)}>
        <select value={control.concurrency} onChange={(e) => onChange((c) => void (c.concurrency = e.target.value as Control['concurrency']), 'concurrency')}>
          {(['parallel', 'ignore', 'restart', 'queue'] as const).map((c) => (
            <option key={c} value={c}>
              {t(`concurrency.${c}`)}
            </option>
          ))}
        </select>
      </Field>
    </div>
  )
}

function StateTab({ control, onChange, variables }: Omit<ControlInspectorProps, 'compact'>) {
  const { t } = useTranslation()
  const state = control.state
  const mode = state?.mode ?? 'none'
  const appearances = state?.appearances ?? {}
  const [newState, setNewState] = useState('')
  const [selected, setSelected] = useState<string>(Object.keys(appearances)[0] ?? 'on')

  const setMode = (m: 'none' | 'toggle' | 'expression') =>
    onChange((c) => {
      if (m === 'none') c.state = null
      else c.state = { mode: m, expression: c.state?.expression ?? null, appearances: c.state?.appearances ?? { on: {} } }
    }, 'state.mode')

  const updateLook = (name: string, mutate: (look: Appearance) => void, key: string) =>
    onChange((c) => {
      if (!c.state) return
      const look = c.state.appearances[name] ?? {}
      mutate(look)
      c.state.appearances[name] = look
    }, `state.${name}.${key}`)

  const stateNames = mode === 'toggle' ? ['on'] : Object.keys(appearances)
  const current = stateNames.includes(selected) ? selected : stateNames[0]

  return (
    <div className="fields">
      <Field label={t('state.mode')}>
        <Segmented
          value={mode}
          onChange={setMode}
          options={[
            { value: 'none', label: t('state.none') },
            { value: 'toggle', label: t('state.toggle') },
            { value: 'expression', label: t('state.expression') },
          ]}
        />
      </Field>
      <p className="muted small">{t(`state.${mode}Help`)}</p>
      {mode === 'toggle' && (
        <p className="muted small">
          {t('state.toggleVariable')} <code>{`toggle.${control.id}`}</code>
        </p>
      )}

      {mode === 'expression' && (
        <>
          <Field label={t('state.expressionLabel')} hint={t('state.expressionHint')}>
            <input
              className="mono"
              value={state?.expression ?? ''}
              placeholder="audio.mic.muted"
              onChange={(e) => onChange((c) => void (c.state && (c.state.expression = e.target.value)), 'state.expression')}
            />
          </Field>
          <div className="state-names">
            {stateNames.map((name) => (
              <button key={name} type="button" className={`chip${name === current ? ' active' : ''}`} onClick={() => setSelected(name)}>
                {name}
                <span
                  className="chip-remove"
                  onClick={(e) => {
                    e.stopPropagation()
                    onChange((c) => void (c.state && delete c.state.appearances[name]), 'state.remove')
                  }}
                >
                  ×
                </span>
              </button>
            ))}
            <input className="chip-input" value={newState} placeholder={t('state.addState')} onChange={(e) => setNewState(e.target.value)} />
            <button
              type="button"
              disabled={!newState.trim()}
              onClick={() => {
                const name = newState.trim()
                onChange((c) => void (c.state && (c.state.appearances[name] = c.state.appearances[name] ?? {})), 'state.add')
                setSelected(name)
                setNewState('')
              }}
            >
              <Icon name="mdi:plus" />
            </button>
          </div>
        </>
      )}

      {mode !== 'none' && current && (
        <>
          <h4>{t('state.appearanceFor', { state: current })}</h4>
          <AppearanceFields value={appearances[current] ?? {}} variables={variables} partial onChange={(mutate, key) => updateLook(current, mutate, key)} />
        </>
      )}
    </div>
  )
}

function LayoutTab({ control, onChange }: Pick<ControlInspectorProps, 'control' | 'onChange'>) {
  const { t } = useTranslation()
  const slider = control.slider
  const widget = control.widget
  return (
    <div className="fields">
      <div className="field-row">
        <Field label={t('layout.rowSpan')}>
          <NumberInput value={control.position.rowSpan ?? 1} min={1} max={12} onChange={(v) => onChange((c) => void (c.position.rowSpan = Math.max(1, v)), 'rowSpan')} />
        </Field>
        <Field label={t('layout.colSpan')}>
          <NumberInput value={control.position.colSpan ?? 1} min={1} max={16} onChange={(v) => onChange((c) => void (c.position.colSpan = Math.max(1, v)), 'colSpan')} />
        </Field>
      </div>

      {control.kind === 'slider' && slider && (
        <>
          <h4>{t('kinds.slider')}</h4>
          <div className="field-row">
            <Field label={t('layout.min')}>
              <NumberInput value={slider.min} onChange={(v) => onChange((c) => void (c.slider!.min = v), 'slider.min')} />
            </Field>
            <Field label={t('layout.max')}>
              <NumberInput value={slider.max} onChange={(v) => onChange((c) => void (c.slider!.max = v), 'slider.max')} />
            </Field>
            <Field label={t('layout.step')}>
              <NumberInput value={slider.step} min={0} step={0.1} onChange={(v) => onChange((c) => void (c.slider!.step = v), 'slider.step')} />
            </Field>
          </div>
          <Field label={t('layout.orientation')}>
            <Segmented
              value={slider.orientation}
              onChange={(o) => onChange((c) => void (c.slider!.orientation = o), 'slider.orientation')}
              options={[
                { value: 'vertical', label: t('layout.vertical') },
                { value: 'horizontal', label: t('layout.horizontal') },
              ]}
            />
          </Field>
          <Field label={t('layout.valueExpression')} hint={t('layout.valueExpressionHint')}>
            <TemplateInput monospace value={slider.valueExpression} placeholder="audio.volume" onChange={(v) => onChange((c) => void (c.slider!.valueExpression = v || null), 'slider.valueExpression')} />
          </Field>
          <ColorField label={t('layout.color')} value={slider.color} onChange={(v) => onChange((c) => void (c.slider!.color = v), 'slider.color')} />
        </>
      )}

      {control.kind === 'widget' && widget && (
        <>
          <h4>{t('kinds.widget')}</h4>
          <Field label={t('layout.widgetType')}>
            <Segmented
              value={widget.type}
              onChange={(type) => onChange((c) => void (c.widget!.type = type), 'widget.type')}
              options={[
                { value: 'text', label: t('layout.widgetText') },
                { value: 'graph', label: t('layout.widgetGraph') },
                { value: 'gauge', label: t('layout.widgetGauge') },
              ]}
            />
          </Field>
          {widget.type !== 'text' && (
            <>
              <Field label={t('layout.widgetExpression')} hint={t('layout.widgetExpressionHint')}>
                <TemplateInput monospace value={widget.expression} placeholder="sys.cpu" onChange={(v) => onChange((c) => void (c.widget!.expression = v || null), 'widget.expression')} />
              </Field>
              <div className="field-row">
                <Field label={t('layout.min')}>
                  <NumberInput value={widget.min} onChange={(v) => onChange((c) => void (c.widget!.min = v), 'widget.min')} />
                </Field>
                <Field label={t('layout.max')}>
                  <NumberInput value={widget.max} onChange={(v) => onChange((c) => void (c.widget!.max = v), 'widget.max')} />
                </Field>
                {widget.type === 'graph' && (
                  <Field label={t('layout.history')}>
                    <NumberInput value={widget.history} min={2} max={600} onChange={(v) => onChange((c) => void (c.widget!.history = v), 'widget.history')} />
                  </Field>
                )}
              </div>
              <ColorField label={t('layout.color')} value={widget.color} onChange={(v) => onChange((c) => void (c.widget!.color = v), 'widget.color')} />
            </>
          )}
        </>
      )}
    </div>
  )
}
