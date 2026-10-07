import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field, Modal } from '../../components/ui'
import { Icon } from '../../icons'
import type { ActionDescriptor, Step } from '../../protocol'
import { useCatalog } from '../catalog'
import { stepSummary } from '../model'
import { ActionParamsForm, VariableInput } from './ActionParamsForm'

const STEP_ICONS: Record<Step['type'], string> = {
  action: 'mdi:play-circle-outline',
  delay: 'mdi:timer-sand',
  if: 'mdi:call-split',
  set: 'mdi:variable',
  repeat: 'mdi:repeat',
}

interface MacroEditorProps {
  steps: Step[]
  onChange: (steps: Step[]) => void
  variables: string[]
  depth?: number
}

/** Editable list of macro steps; conditions and loops nest another MacroEditor. */
export function MacroEditor({ steps, onChange, variables, depth = 0 }: MacroEditorProps) {
  const { t } = useTranslation()
  const [picking, setPicking] = useState(false)
  const [expanded, setExpanded] = useState<number | null>(steps.length === 1 ? 0 : null)

  const replace = (index: number, step: Step) => onChange(steps.map((s, i) => (i === index ? step : s)))
  const remove = (index: number) => onChange(steps.filter((_, i) => i !== index))
  const move = (index: number, delta: number) => {
    const target = index + delta
    if (target < 0 || target >= steps.length) return
    const next = [...steps]
    ;[next[index], next[target]] = [next[target], next[index]]
    onChange(next)
    setExpanded(target)
  }
  const add = (step: Step) => {
    onChange([...steps, step])
    setExpanded(steps.length)
    setPicking(false)
  }

  return (
    <div className={`macro depth-${depth}`}>
      {steps.length === 0 && <p className="muted small">{t('macro.empty')}</p>}
      {steps.map((step, index) => (
        <StepCard
          key={index}
          step={step}
          expanded={expanded === index}
          onToggle={() => setExpanded(expanded === index ? null : index)}
          onChange={(s) => replace(index, s)}
          onRemove={() => remove(index)}
          onMove={(delta) => move(index, delta)}
          variables={variables}
          depth={depth}
        />
      ))}
      <button type="button" className="add-step" onClick={() => setPicking(true)}>
        <Icon name="mdi:plus" /> {t('macro.addStep')}
      </button>
      {picking && <StepPicker onPick={add} onClose={() => setPicking(false)} allowBlocks={depth < 3} />}
    </div>
  )
}

function StepCard(props: {
  step: Step
  expanded: boolean
  onToggle: () => void
  onChange: (step: Step) => void
  onRemove: () => void
  onMove: (delta: number) => void
  variables: string[]
  depth: number
}) {
  const { t, i18n } = useTranslation()
  const byId = useCatalog((s) => s.byId)
  const { step } = props
  const actionName = (id: string) => (i18n.exists(`actions.${id}`) ? t(`actions.${id}`) : byId[id]?.name ?? id)
  const icon = step.type === 'action' ? byId[step.action]?.icon ?? STEP_ICONS.action : STEP_ICONS[step.type]

  return (
    <div className={`step-card${props.expanded ? ' expanded' : ''}`}>
      <div className="step-header" onClick={props.onToggle}>
        <Icon name={icon} />
        <span className="step-title">{step.type === 'action' ? actionName(step.action) : t(`macro.step.${step.type}`)}</span>
        {!props.expanded && <span className="step-summary">{stepSummary(step, actionName)}</span>}
        <span className="step-tools" onClick={(e) => e.stopPropagation()}>
          <button type="button" className="icon-button" onClick={() => props.onMove(-1)} title={t('common.moveUp')}>
            <Icon name="mdi:chevron-up" />
          </button>
          <button type="button" className="icon-button" onClick={() => props.onMove(1)} title={t('common.moveDown')}>
            <Icon name="mdi:chevron-down" />
          </button>
          <button type="button" className="icon-button danger" onClick={props.onRemove} title={t('common.delete')}>
            <Icon name="mdi:delete-outline" />
          </button>
        </span>
      </div>
      {props.expanded && <div className="step-body">{renderBody(step, props, t)}</div>}
    </div>
  )
}

function renderBody(step: Step, props: { onChange: (s: Step) => void; variables: string[]; depth: number }, t: (k: string) => string) {
  switch (step.type) {
    case 'action':
      return <ActionStepBody step={step} onChange={props.onChange} variables={props.variables} />
    case 'delay':
      return (
        <Field label={t('macro.delayMs')}>
          <input type="number" min={0} value={step.ms} onChange={(e) => props.onChange({ ...step, ms: e.target.valueAsNumber || 0 })} />
        </Field>
      )
    case 'set':
      return (
        <div className="fields">
          <Field label={t('macro.variable')}>
            <VariableInput value={step.name} onChange={(name) => props.onChange({ ...step, name })} variables={props.variables} placeholder="user.counter" />
          </Field>
          <Field label={t('macro.value')} hint={t('macro.expressionHint')}>
            <input className="mono" value={step.value} onChange={(e) => props.onChange({ ...step, value: e.target.value })} />
          </Field>
        </div>
      )
    case 'if':
      return (
        <div className="fields">
          <Field label={t('macro.condition')} hint={t('macro.expressionHint')}>
            <input className="mono" value={step.condition} placeholder="obs.scene == 'Main'" onChange={(e) => props.onChange({ ...step, condition: e.target.value })} />
          </Field>
          <div className="branch">
            <span className="branch-label">{t('macro.then')}</span>
            <MacroEditor steps={step.then} onChange={(then) => props.onChange({ ...step, then })} variables={props.variables} depth={props.depth + 1} />
          </div>
          <div className="branch">
            <span className="branch-label">{t('macro.else')}</span>
            <MacroEditor steps={step.else} onChange={(e) => props.onChange({ ...step, else: e })} variables={props.variables} depth={props.depth + 1} />
          </div>
        </div>
      )
    case 'repeat':
      return (
        <div className="fields">
          <Field label={t('macro.times')} hint={t('macro.indexHint')}>
            <input type="number" min={1} max={1000} value={step.count} onChange={(e) => props.onChange({ ...step, count: e.target.valueAsNumber || 1 })} />
          </Field>
          <div className="branch">
            <MacroEditor steps={step.steps} onChange={(steps) => props.onChange({ ...step, steps })} variables={props.variables} depth={props.depth + 1} />
          </div>
        </div>
      )
  }
}

function ActionStepBody({ step, onChange, variables }: { step: Extract<Step, { type: 'action' }>; onChange: (s: Step) => void; variables: string[] }) {
  const { t, i18n } = useTranslation()
  const descriptor = useCatalog((s) => s.byId[step.action])
  if (!descriptor) return <p className="error-text">{t('macro.unknownAction', { id: step.action })}</p>
  const description = i18n.exists(`actionHelp.${step.action}`) ? t(`actionHelp.${step.action}`) : descriptor.description
  return (
    <>
      {description && <p className="muted small">{description}</p>}
      <ActionParamsForm
        descriptor={descriptor}
        params={step.params}
        variables={variables}
        onChange={(name, value) => onChange({ ...step, params: { ...step.params, [name]: value } })}
      />
    </>
  )
}

/** Chooser for a new step: actions grouped by category, plus the control-flow blocks. */
function StepPicker(props: { onPick: (step: Step) => void; onClose: () => void; allowBlocks: boolean }) {
  const { t, i18n } = useTranslation()
  const actions = useCatalog((s) => s.actions)
  const [query, setQuery] = useState('')

  const groups = useMemo(() => {
    const q = query.toLowerCase()
    const name = (a: ActionDescriptor) => (i18n.exists(`actions.${a.id}`) ? t(`actions.${a.id}`) : a.name)
    const filtered = actions.filter((a) => !q || name(a).toLowerCase().includes(q) || a.id.includes(q))
    const map = new Map<string, { descriptor: ActionDescriptor; name: string }[]>()
    for (const a of filtered) map.set(a.category, [...(map.get(a.category) ?? []), { descriptor: a, name: name(a) }])
    return [...map.entries()]
  }, [actions, query, t, i18n])

  const pickAction = (descriptor: ActionDescriptor) =>
    props.onPick({
      type: 'action',
      action: descriptor.id,
      params: Object.fromEntries(descriptor.params.filter((p) => p.default != null).map((p) => [p.name, p.default!])),
    })

  return (
    <Modal title={t('macro.addStep')} onClose={props.onClose} wide>
      {props.allowBlocks && (
        <div className="block-buttons">
          <button type="button" onClick={() => props.onPick({ type: 'delay', ms: 500 })}>
            <Icon name={STEP_ICONS.delay} /> {t('macro.step.delay')}
          </button>
          <button type="button" onClick={() => props.onPick({ type: 'if', condition: '', then: [], else: [] })}>
            <Icon name={STEP_ICONS.if} /> {t('macro.step.if')}
          </button>
          <button type="button" onClick={() => props.onPick({ type: 'set', name: 'user.', value: '' })}>
            <Icon name={STEP_ICONS.set} /> {t('macro.step.set')}
          </button>
          <button type="button" onClick={() => props.onPick({ type: 'repeat', count: 2, steps: [] })}>
            <Icon name={STEP_ICONS.repeat} /> {t('macro.step.repeat')}
          </button>
        </div>
      )}
      <input autoFocus className="search" placeholder={t('macro.searchActions')} value={query} onChange={(e) => setQuery(e.target.value)} />
      <div className="action-groups">
        {groups.map(([category, items]) => (
          <section key={category}>
            <h3>{t(`categories.${category}`, { defaultValue: category })}</h3>
            <div className="action-list">
              {items.map(({ descriptor, name }) => (
                <button type="button" key={descriptor.id} onClick={() => pickAction(descriptor)}>
                  <Icon name={descriptor.icon ?? 'mdi:play'} />
                  <span>{name}</span>
                </button>
              ))}
            </div>
          </section>
        ))}
      </div>
    </Modal>
  )
}
