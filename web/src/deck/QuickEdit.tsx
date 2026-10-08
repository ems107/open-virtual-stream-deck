import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useCatalog, useVariableNames } from '../editor/catalog'
import { useEditor, useSelectedControl } from '../editor/editorStore'
import { ControlInspector } from '../editor/inspector/ControlInspector'
import { findControl, fitsInGrid, newControl, overlapping } from '../editor/model'
import { Icon } from '../icons'
import type { ControlKind, LayoutMessage } from '../protocol'

export type QuickEditTarget = { controlId: string } | { row: number; col: number }

/**
 * On-device editing: loads the shown profile into the editor store (same autosave, undo and conflict
 * handling as the full editor) and shows the inspector in a bottom sheet.
 */
export function QuickEdit({ layout, target, onClose }: { layout: LayoutMessage; target: QuickEditTarget; onClose: () => void }) {
  const { t } = useTranslation()
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pressedBackdrop = useRef(false)
  const control = useSelectedControl()
  const updateControl = useEditor((s) => s.updateControl)
  const variables = useVariableNames()
  const loadCatalog = useCatalog((s) => s.load)

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        await loadCatalog()
        const editor = useEditor.getState()
        if (editor.profile?.id !== layout.profileId) await editor.open(layout.profileId)
        await useEditor.getState().loadProfiles()
        if (cancelled) return
        useEditor.setState({ pageId: layout.pageId })
        if ('controlId' in target) useEditor.getState().select({ kind: 'control', id: target.controlId })
        else useEditor.getState().select(null)
        setReady(true)
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [layout.profileId, layout.pageId, target, loadCatalog])

  const close = async () => {
    if (useEditor.getState().dirty) await useEditor.getState().save()
    onClose()
  }

  const add = (kind: ControlKind) => {
    if ('controlId' in target) return
    const created = newControl(kind, target.row, target.col)
    useEditor.getState().update((draft) => {
      const page = draft.pages.find((p) => p.id === layout.pageId)
      if (!page) return
      if (!fitsInGrid(created.position, draft.grid) || overlapping(page, created.position).length) created.position.rowSpan = 1
      page.controls.push(created)
    })
    useEditor.getState().select({ kind: 'control', id: created.id })
  }

  const remove = () => {
    if (!control) return
    const id = control.id
    useEditor.getState().update((draft) => {
      const found = findControl(draft, id)
      if (found) found.page.controls = found.page.controls.filter((c) => c.id !== id)
    })
    void close()
  }

  return (
    // The sheet opens on the tile's pointerup, so on touch screens the click that follows lands on this
    // backdrop: only close when the press also started here.
    <div
      className="sheet-backdrop"
      onPointerDown={(e) => (pressedBackdrop.current = e.target === e.currentTarget)}
      onClick={(e) => e.target === e.currentTarget && pressedBackdrop.current && void close()}
    >
      <div className="sheet quick-edit" onClick={(e) => e.stopPropagation()}>
        <header className="sheet-header">
          <strong>{control ? t('quickEdit.editTile') : t('quickEdit.newTile')}</strong>
          <span className="spacer" />
          {control && (
            <button className="icon-button danger" onClick={remove} title={t('common.delete')}>
              <Icon name="mdi:delete-outline" />
            </button>
          )}
          <button className="primary" onClick={() => void close()}>
            {t('common.done')}
          </button>
        </header>
        {error && <p className="error-text">{error}</p>}
        {!ready ? (
          <p className="muted">{t('common.loading')}</p>
        ) : control ? (
          <ControlInspector control={control} compact variables={variables} onChange={(mutate, key) => updateControl(control.id, mutate, key)} />
        ) : (
          <div className="empty-cell-actions">
            <button onClick={() => add('button')}>
              <Icon name="mdi:gesture-tap-button" /> {t('editor.addButton')}
            </button>
            <button onClick={() => add('slider')}>
              <Icon name="mdi:tune-vertical" /> {t('editor.addSlider')}
            </button>
            <button onClick={() => add('widget')}>
              <Icon name="mdi:chart-areaspline" /> {t('editor.addWidget')}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
