import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '../icons'
import type { Profile } from '../protocol'
import { DRAG_TYPE } from './EditorGrid'
import { useEditor } from './editorStore'
import { deletePage, moveToPage, newPage, pageTree } from './model'

export function PagesPanel({ profile }: { profile: Profile }) {
  const { t } = useTranslation()
  const pageId = useEditor((s) => s.pageId)
  const selectPage = useEditor((s) => s.selectPage)
  const update = useEditor((s) => s.update)
  const [renaming, setRenaming] = useState<string | null>(null)
  const [dropTarget, setDropTarget] = useState<string | null>(null)

  const addPage = (parentId: string | null) => {
    const page = newPage(parentId ? t('editor.newFolder') : t('editor.newPage'), parentId)
    update((draft) => void draft.pages.push(page))
    selectPage(page.id)
    setRenaming(page.id)
  }

  return (
    <aside className="pages-panel">
      <header>
        <h3>{t('editor.pages')}</h3>
        <button className="icon-button" title={t('editor.addPage')} onClick={() => addPage(null)}>
          <Icon name="mdi:file-plus-outline" />
        </button>
      </header>
      <ul>
        {pageTree(profile).map(({ page, depth }) => {
          const isHome = page.id === profile.homePageId
          return (
            <li
              key={page.id}
              className={`${page.id === pageId ? 'active' : ''}${dropTarget === page.id ? ' drop' : ''}`}
              style={{ paddingLeft: 8 + depth * 14 }}
              onClick={() => selectPage(page.id)}
              onDragOver={(e) => {
                if (!e.dataTransfer.types.includes(DRAG_TYPE) || page.id === pageId) return
                e.preventDefault()
                setDropTarget(page.id)
              }}
              onDragLeave={() => setDropTarget(null)}
              onDrop={(e) => {
                e.preventDefault()
                setDropTarget(null)
                const id = e.dataTransfer.getData(DRAG_TYPE)
                if (id) update((draft) => void moveToPage(draft, id, page.id))
              }}
            >
              <Icon name={isHome ? 'mdi:home' : page.parentId ? 'mdi:folder-outline' : 'mdi:file-outline'} />
              {renaming === page.id ? (
                <input
                  autoFocus
                  defaultValue={page.name}
                  onClick={(e) => e.stopPropagation()}
                  onBlur={(e) => {
                    const name = e.target.value.trim()
                    if (name) update((draft) => void (draft.pages.find((p) => p.id === page.id)!.name = name))
                    setRenaming(null)
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
                    if (e.key === 'Escape') setRenaming(null)
                  }}
                />
              ) : (
                <span className="page-name" onDoubleClick={() => setRenaming(page.id)}>
                  {page.name}
                </span>
              )}
              <span className="page-tools" onClick={(e) => e.stopPropagation()}>
                <button className="icon-button" title={t('editor.addFolder')} onClick={() => addPage(page.id)}>
                  <Icon name="mdi:folder-plus-outline" />
                </button>
                <button className="icon-button" title={t('common.rename')} onClick={() => setRenaming(page.id)}>
                  <Icon name="mdi:pencil-outline" />
                </button>
                {!isHome && (
                  <>
                    <button
                      className="icon-button"
                      title={t('editor.setHome')}
                      onClick={() => update((draft) => void (draft.homePageId = page.id))}
                    >
                      <Icon name="mdi:home-outline" />
                    </button>
                    <button
                      className="icon-button danger"
                      title={t('common.delete')}
                      onClick={() => {
                        if (!confirm(t('editor.deletePageConfirm', { name: page.name }))) return
                        update((draft) => void deletePage(draft, page.id))
                        if (page.id === pageId) selectPage(profile.homePageId)
                      }}
                    >
                      <Icon name="mdi:delete-outline" />
                    </button>
                  </>
                )}
              </span>
            </li>
          )
        })}
      </ul>
      <p className="muted small">{t('editor.pagesHint')}</p>
    </aside>
  )
}
