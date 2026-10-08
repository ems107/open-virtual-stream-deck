import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, download } from '../api'
import { Field, Modal, NumberInput } from '../components/ui'
import { Icon } from '../icons'
import type { BackupInfo, ControlKind, Profile } from '../protocol'
import { deckSocket } from '../state/connection'
import { useCatalog, useVariableNames } from './catalog'
import { EditorGrid } from './EditorGrid'
import { copyToClipboard, readClipboard, useCurrentPage, useEditor, useSelectedControl } from './editorStore'
import { ControlInspector } from './inspector/ControlInspector'
import { cloneControl, findControl, firstFreeCell, fitsInGrid, newControl, overlapping } from './model'
import { PagesPanel } from './PagesPanel'
import { ProfileSettingsDialog } from './ProfileSettingsDialog'

export function ProfileEditor() {
  const { t } = useTranslation()
  const { profiles, profile, loadProfiles, open, dirty, saving, error, conflict, past, future, undo, redo } = useEditor()
  const page = useCurrentPage()
  const loadCatalog = useCatalog((s) => s.load)
  const [dialog, setDialog] = useState<'settings' | 'backups' | null>(null)
  const importInput = useRef<HTMLInputElement>(null)

  useEffect(() => {
    void loadCatalog()
    void loadProfiles().then(() => {
      const { profiles: list, profile: current } = useEditor.getState()
      if (current || list.length === 0) return
      let remembered: string | null = null
      try {
        remembered = localStorage.getItem('ovsd.editor.profile')
      } catch {
        // ignore
      }
      void open(list.find((p) => p.id === remembered)?.id ?? list[0].id)
    })
  }, [loadCatalog, loadProfiles, open])

  // Someone else (another editor, quick edit on a device, an import...) changed profiles.
  useEffect(
    () =>
      deckSocket.onConfigChanged((message) => {
        if (message.kind !== 'profiles') return
        void loadProfiles()
        const state = useEditor.getState()
        if (state.profile?.id === message.id && message.revision != null && message.revision !== state.baseRevision && !state.dirty && !state.saving)
          void state.reloadFromServer()
      }),
    [loadProfiles],
  )

  useKeyboardShortcuts()

  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (useEditor.getState().dirty) e.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [])

  const createProfile = async () => {
    const name = prompt(t('editor.newProfileName'), t('editor.newProfile'))
    if (!name) return
    const created = await api.post<Profile>('/api/profiles', { name, rows: 3, cols: 5 })
    await loadProfiles()
    await open(created.id)
  }

  const importProfile = async (file: File | undefined) => {
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    try {
      const imported = await api.post<Profile>('/api/profiles/import', form)
      await loadProfiles()
      await open(imported.id)
    } catch (e) {
      alert((e as Error).message)
    }
  }

  return (
    <div className="profile-editor">
      <div className="toolbar">
        <select value={profile?.id ?? ''} onChange={(e) => void open(e.target.value)}>
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <button title={t('editor.newProfile')} onClick={() => void createProfile()}>
          <Icon name="mdi:plus" />
        </button>
        {profile && (
          <>
            <button title={t('editor.profileSettings')} onClick={() => setDialog('settings')}>
              <Icon name="mdi:cog-outline" /> <span className="hide-narrow">{t('editor.profileSettings')}</span>
            </button>
            <span className="toolbar-sep" />
            <button title={`${t('editor.undo')} (Ctrl+Z)`} disabled={past.length === 0} onClick={undo}>
              <Icon name="mdi:undo" />
            </button>
            <button title={`${t('editor.redo')} (Ctrl+Y)`} disabled={future.length === 0} onClick={redo}>
              <Icon name="mdi:redo" />
            </button>
            <span className="toolbar-sep" />
            <button
              title={t('editor.duplicateProfile')}
              onClick={async () => {
                const copy = await api.post<Profile>(`/api/profiles/${profile.id}/duplicate`)
                await loadProfiles()
                await open(copy.id)
              }}
            >
              <Icon name="mdi:content-duplicate" />
            </button>
            <button title={t('editor.export')} onClick={() => void download(`/api/profiles/${profile.id}/export`, `${profile.name}.ovsd.zip`)}>
              <Icon name="mdi:export" />
            </button>
            <button title={t('editor.import')} onClick={() => importInput.current?.click()}>
              <Icon name="mdi:import" />
            </button>
            <button title={t('editor.backups')} onClick={() => setDialog('backups')}>
              <Icon name="mdi:history" />
            </button>
            <button
              className="danger"
              title={t('editor.deleteProfile')}
              disabled={profiles.length <= 1}
              onClick={async () => {
                if (!confirm(t('editor.deleteProfileConfirm', { name: profile.name }))) return
                await api.delete(`/api/profiles/${profile.id}`)
                useEditor.setState({ profile: null, dirty: false })
                await loadProfiles()
                const next = useEditor.getState().profiles[0]
                if (next) await open(next.id)
              }}
            >
              <Icon name="mdi:delete-outline" />
            </button>
            <span className="save-state">
              {saving ? t('editor.saving') : dirty ? t('editor.unsaved') : error ? '' : t('editor.saved')}
            </span>
          </>
        )}
        <input ref={importInput} type="file" accept=".zip" hidden onChange={(e) => void importProfile(e.target.files?.[0])} />
      </div>

      {conflict && <ConflictBanner />}
      {error && <div className="banner error">{error}</div>}

      {profile && page ? (
        <div className="editor-body">
          <PagesPanel profile={profile} />
          <section className="editor-canvas">
            <div className="canvas-title">
              <strong>{page.name}</strong>
              <span className="muted">
                {profile.grid.rows} × {profile.grid.cols}
              </span>
            </div>
            <EditorGrid profile={profile} page={page} />
            <p className="muted small">{t('editor.gridHint')}</p>
          </section>
          <InspectorPanel profile={profile} />
        </div>
      ) : (
        <p className="muted">{t('common.loading')}</p>
      )}

      {dialog === 'settings' && profile && <ProfileSettingsDialog onClose={() => setDialog(null)} />}
      {dialog === 'backups' && profile && <BackupsDialog profileId={profile.id} onClose={() => setDialog(null)} />}
    </div>
  )
}

function InspectorPanel({ profile }: { profile: Profile }) {
  const { t } = useTranslation()
  const selection = useEditor((s) => s.selection)
  const control = useSelectedControl()
  const updateControl = useEditor((s) => s.updateControl)
  const variables = useVariableNames()

  if (selection?.kind === 'cell') return <aside className="inspector-panel"><EmptyCellActions row={selection.row} col={selection.col} /></aside>
  if (!control)
    return (
      <aside className="inspector-panel">
        <p className="muted">{t('editor.selectHint')}</p>
      </aside>
    )

  const blocked = (() => {
    const found = findControl(profile, control.id)
    if (!found) return false
    return !fitsInGrid(control.position, profile.grid) || overlapping(found.page, control.position, control.id).length > 0
  })()

  return (
    <aside className="inspector-panel">
      <ControlActions controlId={control.id} />
      {blocked && <div className="banner warning">{t('editor.overlapWarning')}</div>}
      <ControlInspector key={control.id} control={control} variables={variables} onChange={(mutate, key) => updateControl(control.id, mutate, key)} />
    </aside>
  )
}

function ControlActions({ controlId }: { controlId: string }) {
  const { t } = useTranslation()
  const update = useEditor((s) => s.update)
  const select = useEditor((s) => s.select)
  return (
    <div className="control-actions">
      <button title={`${t('common.copy')} (Ctrl+C)`} onClick={() => copySelected()}>
        <Icon name="mdi:content-copy" />
      </button>
      <button title={`${t('common.duplicate')} (Ctrl+D)`} onClick={() => duplicateSelected()}>
        <Icon name="mdi:content-duplicate" />
      </button>
      <button
        className="danger"
        title={`${t('common.delete')} (Supr)`}
        onClick={() => {
          update((draft) => {
            const found = findControl(draft, controlId)
            if (found) found.page.controls = found.page.controls.filter((c) => c.id !== controlId)
          })
          select(null)
        }}
      >
        <Icon name="mdi:delete-outline" /> {t('common.delete')}
      </button>
    </div>
  )
}

function EmptyCellActions({ row, col }: { row: number; col: number }) {
  const { t } = useTranslation()
  const update = useEditor((s) => s.update)
  const select = useEditor((s) => s.select)
  const pageId = useEditor((s) => s.pageId)
  const clipboard = readClipboard()

  const add = (kind: ControlKind) => {
    const control = newControl(kind, row, col)
    update((draft) => {
      const page = draft.pages.find((p) => p.id === pageId)
      if (!page) return
      // Sliders default to 2 rows; shrink if they don't fit here.
      if (!fitsInGrid(control.position, draft.grid) || overlapping(page, control.position).length) control.position.rowSpan = 1
      page.controls.push(control)
    })
    select({ kind: 'control', id: control.id })
  }

  return (
    <div className="empty-cell-actions">
      <h3>{t('editor.emptyCell', { row: row + 1, col: col + 1 })}</h3>
      <button onClick={() => add('button')}>
        <Icon name="mdi:gesture-tap-button" /> {t('editor.addButton')}
      </button>
      <button onClick={() => add('slider')}>
        <Icon name="mdi:tune-vertical" /> {t('editor.addSlider')}
      </button>
      <button onClick={() => add('widget')}>
        <Icon name="mdi:chart-areaspline" /> {t('editor.addWidget')}
      </button>
      <button disabled={!clipboard} onClick={() => pasteAt(row, col)}>
        <Icon name="mdi:content-paste" /> {t('common.paste')}
      </button>
    </div>
  )
}

function ConflictBanner() {
  const { t } = useTranslation()
  const reload = useEditor((s) => s.reloadFromServer)
  const save = useEditor((s) => s.save)
  return (
    <div className="banner warning">
      {t('editor.conflict')}
      <button onClick={() => void reload()}>{t('editor.conflictReload')}</button>
      <button onClick={() => void save(true)}>{t('editor.conflictOverwrite')}</button>
    </div>
  )
}

interface DeletedProfile {
  id: string
  name: string
  backup: string
  deleted: string
}

/** Profiles deleted from the editor: their last version is kept and can be brought back. */
function DeletedProfiles({ onRestored }: { onRestored: () => void }) {
  const { t } = useTranslation()
  const [deleted, setDeleted] = useState<DeletedProfile[]>([])
  useEffect(() => {
    void api.get<DeletedProfile[]>('/api/profiles/deleted').then(setDeleted)
  }, [])
  if (deleted.length === 0) return null
  return (
    <>
      <h3>{t('editor.deletedProfiles')}</h3>
      <ul className="backup-list">
        {deleted.map((d) => (
          <li key={d.id}>
            <span>
              <strong>{d.name}</strong> <span className="muted small">{new Date(d.deleted).toLocaleString()}</span>
            </span>
            <button
              onClick={async () => {
                await api.post(`/api/profiles/${d.id}/backups/${d.backup}/restore`)
                await useEditor.getState().loadProfiles()
                await useEditor.getState().open(d.id)
                onRestored()
              }}
            >
              {t('editor.restore')}
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

function BackupsDialog({ profileId, onClose }: { profileId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [backups, setBackups] = useState<BackupInfo[] | null>(null)
  useEffect(() => {
    void api.get<BackupInfo[]>(`/api/profiles/${profileId}/backups`).then(setBackups)
  }, [profileId])
  return (
    <Modal title={t('editor.backups')} onClose={onClose}>
      <p className="muted small">{t('editor.backupsHint')}</p>
      {!backups ? (
        <p className="muted">{t('common.loading')}</p>
      ) : backups.length === 0 ? (
        <p className="muted">{t('editor.noBackups')}</p>
      ) : (
        <ul className="backup-list">
          {backups.map((b) => (
            <li key={b.name}>
              <span>
                {new Date(b.created).toLocaleString()} {b.name.endsWith('-deleted') && <em>({t('editor.deletedVersion')})</em>}
              </span>
              <button
                onClick={async () => {
                  if (useEditor.getState().dirty) await useEditor.getState().save()
                  await api.post(`/api/profiles/${profileId}/backups/${b.name}/restore`)
                  await useEditor.getState().reloadFromServer()
                  onClose()
                }}
              >
                {t('editor.restore')}
              </button>
            </li>
          ))}
        </ul>
      )}
      <DeletedProfiles onRestored={onClose} />
    </Modal>
  )
}

// ------------------------------------------------------------------ clipboard & shortcuts

function copySelected() {
  const { profile, selection } = useEditor.getState()
  if (selection?.kind !== 'control' || !profile) return
  const found = findControl(profile, selection.id)
  if (found) copyToClipboard(found.control)
}

function duplicateSelected() {
  const { profile, selection, update, select } = useEditor.getState()
  if (selection?.kind !== 'control' || !profile) return
  const found = findControl(profile, selection.id)
  if (!found) return
  const cell = firstFreeCell(found.page, profile.grid, found.control.position.rowSpan ?? 1, found.control.position.colSpan ?? 1)
  if (!cell) return
  const copy = cloneControl(found.control, cell.row, cell.col)
  update((draft) => void draft.pages.find((p) => p.id === found.page.id)!.controls.push(copy))
  select({ kind: 'control', id: copy.id })
}

function pasteAt(row: number, col: number) {
  const control = readClipboard()
  const { pageId, update, select } = useEditor.getState()
  if (!control) return
  const copy = cloneControl(control, row, col)
  update((draft) => {
    const page = draft.pages.find((p) => p.id === pageId)
    if (!page) return
    if (!fitsInGrid(copy.position, draft.grid) || overlapping(page, copy.position).length) copy.position = { ...copy.position, rowSpan: 1, colSpan: 1 }
    page.controls.push(copy)
  })
  select({ kind: 'control', id: copy.id })
}

function useKeyboardShortcuts() {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement
      if (target.closest('input, textarea, select, [contenteditable]')) return
      const state = useEditor.getState()
      const ctrl = e.ctrlKey || e.metaKey
      const key = e.key.toLowerCase()
      if (ctrl && key === 'z' && !e.shiftKey) state.undo()
      else if (ctrl && (key === 'y' || (key === 'z' && e.shiftKey))) state.redo()
      else if (ctrl && key === 'c') copySelected()
      else if (ctrl && key === 'x') {
        copySelected()
        deleteSelected()
      } else if (ctrl && key === 'v' && state.selection?.kind === 'cell') pasteAt(state.selection.row, state.selection.col)
      else if (ctrl && key === 'd') duplicateSelected()
      else if (e.key === 'Delete' || e.key === 'Backspace') deleteSelected()
      else if (e.key === 'Escape') state.select(null)
      else return
      e.preventDefault()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
}

function deleteSelected() {
  const { selection, update, select } = useEditor.getState()
  if (selection?.kind !== 'control') return
  update((draft) => {
    const found = findControl(draft, selection.id)
    if (found) found.page.controls = found.page.controls.filter((c) => c.id !== selection.id)
  })
  select(null)
}

export function GridSizeFields({ profile }: { profile: Profile }) {
  const { t } = useTranslation()
  const update = useEditor((s) => s.update)
  const lost = (rows: number, cols: number) =>
    profile.pages.reduce((n, p) => n + p.controls.filter((c) => c.position.row >= rows || c.position.col >= cols).length, 0)
  const setGrid = (rows: number, cols: number) => {
    const removed = lost(rows, cols)
    if (removed > 0 && !confirm(t('editor.gridShrinkConfirm', { count: removed }))) return
    update((draft) => {
      draft.grid = { rows, cols }
      for (const page of draft.pages) page.controls = page.controls.filter((c) => c.position.row < rows && c.position.col < cols)
    }, 'grid')
  }
  return (
    <div className="field-row">
      <Field label={t('editor.rows')}>
        <NumberInput value={profile.grid.rows} min={1} max={12} onChange={(v) => setGrid(Math.min(12, Math.max(1, v)), profile.grid.cols)} />
      </Field>
      <Field label={t('editor.cols')}>
        <NumberInput value={profile.grid.cols} min={1} max={16} onChange={(v) => setGrid(profile.grid.rows, Math.min(16, Math.max(1, v)))} />
      </Field>
    </div>
  )
}
