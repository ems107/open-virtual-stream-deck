import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { TileView } from '../deck/TileView'
import { Icon } from '../icons'
import type { Page, Profile } from '../protocol'
import { useEditor } from './editorStore'
import { controlAt, controlToTile, moveWithinPage, span } from './model'
import { usePreview } from './usePreview'

export const DRAG_TYPE = 'application/x-ovsd-control'

/** Editable grid: select tiles or empty cells, drag tiles to move/swap them. */
export function EditorGrid({ profile, page }: { profile: Profile; page: Page }) {
  const { t } = useTranslation()
  const selection = useEditor((s) => s.selection)
  const select = useEditor((s) => s.select)
  const update = useEditor((s) => s.update)
  const preview = usePreview(page.controls)
  const [dropCell, setDropCell] = useState<string | null>(null)
  const { rows, cols } = profile.grid
  const theme = profile.theme
  const reservedBack = page.parentId && !controlAt(page, 0, 0)

  const cells: { row: number; col: number }[] = []
  for (let row = 0; row < rows; row++) for (let col = 0; col < cols; col++) if (!controlAt(page, row, col)) cells.push({ row, col })

  const drop = (event: React.DragEvent, row: number, col: number) => {
    event.preventDefault()
    setDropCell(null)
    const id = event.dataTransfer.getData(DRAG_TYPE)
    if (!id) return
    update((draft) => {
      const target = draft.pages.find((p) => p.id === page.id)
      if (target) moveWithinPage(draft, target, id, row, col)
    })
  }

  const dropProps = (row: number, col: number) => ({
    onDragOver: (e: React.DragEvent) => {
      if (!e.dataTransfer.types.includes(DRAG_TYPE)) return
      e.preventDefault()
      setDropCell(`${row}:${col}`)
    },
    onDragLeave: () => setDropCell(null),
    onDrop: (e: React.DragEvent) => drop(e, row, col),
  })

  return (
    <div
      className="editor-grid"
      style={{
        gridTemplateRows: `repeat(${rows}, minmax(0, 1fr))`,
        gridTemplateColumns: `repeat(${cols}, minmax(0, 1fr))`,
        gap: theme.gap,
        padding: theme.gap,
        background: theme.background,
        aspectRatio: `${cols} / ${rows}`,
      }}
    >
      {cells.map(({ row, col }) => {
        const isBack = reservedBack && row === 0 && col === 0
        const selected = selection?.kind === 'cell' && selection.row === row && selection.col === col
        return (
          <button
            key={`${row}:${col}`}
            type="button"
            className={`editor-cell${selected ? ' selected' : ''}${dropCell === `${row}:${col}` ? ' drop' : ''}${isBack ? ' back' : ''}`}
            style={{ gridRow: row + 1, gridColumn: col + 1, borderRadius: theme.radius }}
            onClick={() => select({ kind: 'cell', row, col })}
            title={isBack ? t('editor.backTile') : undefined}
            {...dropProps(row, col)}
          >
            <Icon name={isBack ? 'mdi:arrow-left' : 'mdi:plus'} />
          </button>
        )
      })}
      {page.controls.map((control) => {
        const tile = preview[control.id] ?? controlToTile(control)
        const { rowSpan, colSpan } = span(control.position)
        const selected = selection?.kind === 'control' && selection.id === control.id
        return (
          <div
            key={control.id}
            className={`editor-tile${dropCell === `${control.position.row}:${control.position.col}` ? ' drop' : ''}`}
            style={{ gridRow: `${control.position.row + 1} / span ${rowSpan}`, gridColumn: `${control.position.col + 1} / span ${colSpan}` }}
            draggable
            onDragStart={(e) => {
              e.dataTransfer.setData(DRAG_TYPE, control.id)
              e.dataTransfer.effectAllowed = 'move'
              select({ kind: 'control', id: control.id })
            }}
            onClick={() => select({ kind: 'control', id: control.id })}
            {...dropProps(control.position.row, control.position.col)}
          >
            <TileView tile={{ ...tile, ...control.position, rowSpan, colSpan }} theme={theme} selected={selected} />
          </div>
        )
      })}
    </div>
  )
}
