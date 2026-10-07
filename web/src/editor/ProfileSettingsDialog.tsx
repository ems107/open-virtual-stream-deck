import { useTranslation } from 'react-i18next'
import { ColorField, Field, Modal, NumberInput } from '../components/ui'
import { Icon } from '../icons'
import { useEditor } from './editorStore'
import { GridSizeFields } from './ProfileEditor'

/** Name, grid size, theme and automatic switching rules of the current profile. */
export function ProfileSettingsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const profile = useEditor((s) => s.profile)
  const update = useEditor((s) => s.update)
  if (!profile) return null
  const theme = profile.theme

  return (
    <Modal title={t('editor.profileSettings')} onClose={onClose} footer={<button className="primary" onClick={onClose}>{t('common.done')}</button>}>
      <div className="fields">
        <Field label={t('common.name')}>
          <input value={profile.name} onChange={(e) => update((d) => void (d.name = e.target.value), 'name')} />
        </Field>
        <GridSizeFields profile={profile} />

        <h4>{t('theme.title')}</h4>
        <ColorField label={t('theme.background')} value={theme.background} onChange={(v) => update((d) => void (d.theme.background = v ?? '#111318'), 'theme.background')} />
        <ColorField label={t('theme.tileBackground')} value={theme.tileBackground} onChange={(v) => update((d) => void (d.theme.tileBackground = v ?? '#1f232b'), 'theme.tile')} />
        <ColorField label={t('theme.textColor')} value={theme.textColor} onChange={(v) => update((d) => void (d.theme.textColor = v ?? '#e8eaf0'), 'theme.text')} />
        <div className="field-row">
          <Field label={t('theme.gap')}>
            <NumberInput value={theme.gap} min={0} max={40} onChange={(v) => update((d) => void (d.theme.gap = v), 'theme.gap')} />
          </Field>
          <Field label={t('theme.radius')}>
            <NumberInput value={theme.radius} min={0} max={60} onChange={(v) => update((d) => void (d.theme.radius = v), 'theme.radius')} />
          </Field>
        </div>

        <h4>{t('rules.title')}</h4>
        <p className="muted small">{t('rules.help')}</p>
        {profile.matchRules.map((rule, index) => (
          <div className="rule-row" key={index}>
            <input
              placeholder={t('rules.process')}
              value={rule.process ?? ''}
              onChange={(e) => update((d) => void (d.matchRules[index].process = e.target.value || null), `rule.${index}.process`)}
            />
            <input
              placeholder={t('rules.title_contains')}
              value={rule.titleContains ?? ''}
              onChange={(e) => update((d) => void (d.matchRules[index].titleContains = e.target.value || null), `rule.${index}.title`)}
            />
            <button className="icon-button danger" onClick={() => update((d) => void d.matchRules.splice(index, 1))}>
              <Icon name="mdi:delete-outline" />
            </button>
          </div>
        ))}
        <button onClick={() => update((d) => void d.matchRules.push({ process: '', titleContains: null }))}>
          <Icon name="mdi:plus" /> {t('rules.add')}
        </button>
      </div>
    </Modal>
  )
}
