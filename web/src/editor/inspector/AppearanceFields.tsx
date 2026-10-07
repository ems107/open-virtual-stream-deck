import { useTranslation } from 'react-i18next'
import { ColorField, Field, Segmented, TemplateInput } from '../../components/ui'
import type { Appearance } from '../../protocol'
import { IconPicker } from '../IconPicker'
import { ImageField } from '../ImageField'

interface AppearanceFieldsProps {
  value: Appearance
  onChange: (mutate: (look: Appearance) => void, key: string) => void
  variables: string[]
  /** State overrides: empty fields mean "same as the default appearance". */
  partial?: boolean
}

export function AppearanceFields({ value, onChange, variables, partial }: AppearanceFieldsProps) {
  const { t } = useTranslation()
  const inherited = partial ? t('common.inherited') : undefined

  return (
    <div className="fields">
      <Field label={t('appearance.text')} hint={t('appearance.textHint')}>
        <TemplateInput
          value={value.text}
          multiline
          placeholder={inherited}
          variables={variables}
          onChange={(text) => onChange((l) => void (l.text = text || null), 'text')}
        />
      </Field>
      <div className="field-row">
        <Field label={t('appearance.fontSize')}>
          <input
            type="range"
            min={6}
            max={60}
            value={value.fontSize ?? 13}
            onChange={(e) => onChange((l) => void (l.fontSize = e.target.valueAsNumber), 'fontSize')}
          />
        </Field>
        <Field label={t('appearance.textPosition')}>
          <Segmented
            value={value.textPosition ?? null}
            onChange={(position) => onChange((l) => void (l.textPosition = l.textPosition === position ? null : position), 'textPosition')}
            options={[
              { value: 'top', label: t('appearance.top'), icon: 'mdi:format-vertical-align-top' },
              { value: 'center', label: t('appearance.center'), icon: 'mdi:format-vertical-align-center' },
              { value: 'bottom', label: t('appearance.bottom'), icon: 'mdi:format-vertical-align-bottom' },
            ]}
          />
        </Field>
      </div>
      <ColorField label={t('appearance.textColor')} value={value.textColor} onChange={(c) => onChange((l) => void (l.textColor = c), 'textColor')} />

      <Field label={t('appearance.icon')}>
        <IconPicker value={value.icon} onChange={(icon) => onChange((l) => void (l.icon = icon), 'icon')} />
      </Field>
      <ColorField label={t('appearance.iconColor')} value={value.iconColor} onChange={(c) => onChange((l) => void (l.iconColor = c), 'iconColor')} />

      <Field label={t('appearance.image')} hint={t('appearance.imageHint')}>
        <ImageField value={value.image} variables={variables} onChange={(image) => onChange((l) => void (l.image = image), 'image')} />
      </Field>
      {value.image && (
        <Field label={t('appearance.imageFit')}>
          <Segmented
            value={value.imageFit ?? 'cover'}
            onChange={(fit) => onChange((l) => void (l.imageFit = fit), 'imageFit')}
            options={[
              { value: 'cover', label: t('appearance.cover') },
              { value: 'contain', label: t('appearance.contain') },
            ]}
          />
        </Field>
      )}

      <ColorField label={t('appearance.background')} value={value.background} onChange={(c) => onChange((l) => void (l.background = c), 'background')} />
    </div>
  )
}
