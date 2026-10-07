import { useMemo } from 'react'
import { Icon } from '../icons'
import type { LayoutMessage } from '../protocol'
import { useDeck } from '../state/deck'
import { DeckTile } from './DeckTile'

interface DeckGridProps {
  layout: LayoutMessage
  editMode: boolean
  onEditTile: (controlId: string) => void
  onAddAt: (row: number, col: number) => void
}

export function DeckGrid({ layout, editMode, onEditTile, onAddAt }: DeckGridProps) {
  const tiles = useDeck((s) => s.tiles)
  const list = useMemo(() => Object.values(tiles), [tiles])

  // In edit mode, show "+" placeholders on free cells.
  const freeCells = useMemo(() => {
    if (!editMode) return []
    const covered = new Set<string>()
    for (const t of list)
      for (let r = t.row; r < t.row + t.rowSpan; r++)
        for (let c = t.col; c < t.col + t.colSpan; c++) covered.add(`${r}:${c}`)
    const free: [number, number][] = []
    for (let r = 0; r < layout.rows; r++) for (let c = 0; c < layout.cols; c++) if (!covered.has(`${r}:${c}`)) free.push([r, c])
    return free
  }, [editMode, list, layout.rows, layout.cols])

  return (
    <div
      className="deck-grid"
      style={{
        gridTemplateRows: `repeat(${layout.rows}, minmax(0, 1fr))`,
        gridTemplateColumns: `repeat(${layout.cols}, minmax(0, 1fr))`,
        gap: layout.theme.gap,
        padding: layout.theme.gap,
      }}
    >
      {list.map((tile) => (
        <DeckTile
          key={tile.id}
          tile={tile}
          theme={layout.theme}
          editMode={editMode && tile.id !== '__back'}
          onEdit={onEditTile}
        />
      ))}
      {freeCells.map(([row, col]) => (
        <button
          key={`free-${row}-${col}`}
          className="deck-free-cell"
          style={{ gridRow: row + 1, gridColumn: col + 1, borderRadius: layout.theme.radius }}
          onClick={() => onAddAt(row, col)}
        >
          <Icon name="mdi:plus" />
        </button>
      ))}
    </div>
  )
}
