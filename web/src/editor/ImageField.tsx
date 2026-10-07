import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { uploadImage } from '../api'
import { Icon } from '../icons'

/** Image URL, a "{{template}}" (e.g. {{media.art}}) or an uploaded file (stored on the server). */
export function ImageField(props: { value: string | null | undefined; onChange: (value: string | null) => void; variables?: string[] }) {
  const { t } = useTranslation()
  const fileInput = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const value = props.value ?? ''
  const isTemplate = value.includes('{{')

  const upload = async (file: File | undefined) => {
    if (!file) return
    setBusy(true)
    setError(null)
    try {
      props.onChange(await uploadImage(file))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div
      className="image-field"
      onDragOver={(e) => e.preventDefault()}
      onDrop={(e) => {
        e.preventDefault()
        void upload(e.dataTransfer.files[0])
      }}
    >
      <div className="image-thumb">
        {value && !isTemplate ? <img src={value} alt="" /> : <Icon name={isTemplate ? 'mdi:code-braces' : 'mdi:image-plus'} />}
      </div>
      <div className="image-controls">
        <input
          value={value}
          placeholder={t('image.placeholder')}
          list={props.variables ? 'image-variables' : undefined}
          onChange={(e) => props.onChange(e.target.value || null)}
        />
        <div className="row">
          <button type="button" onClick={() => fileInput.current?.click()} disabled={busy}>
            <Icon name="mdi:upload" /> {busy ? t('common.loading') : t('image.upload')}
          </button>
          {value && (
            <button type="button" onClick={() => props.onChange(null)}>
              <Icon name="mdi:delete-outline" />
            </button>
          )}
        </div>
        {error && <small className="error-text">{error}</small>}
      </div>
      <input ref={fileInput} type="file" accept="image/*" hidden onChange={(e) => void upload(e.target.files?.[0])} />
      {props.variables && (
        <datalist id="image-variables">
          <option value="{{media.art}}" />
        </datalist>
      )}
    </div>
  )
}
