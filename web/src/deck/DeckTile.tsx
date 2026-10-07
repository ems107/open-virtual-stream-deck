import { useEffect, useRef, useState } from 'react'
import type { Theme, TileState } from '../protocol'
import { deckSocket } from '../state/connection'
import { useDeck } from '../state/deck'
import { GestureRecognizer, sliderValueAt } from './gestures'
import { TileView } from './TileView'

const SLIDER_SEND_INTERVAL_MS = 33

interface DeckTileProps {
  tile: TileState
  theme: Theme
  editMode: boolean
  onEdit: (tileId: string) => void
}

/** A tile on the deck: TileView plus gesture handling (or slider dragging). */
export function DeckTile({ tile, theme, editMode, onEdit }: DeckTileProps) {
  const settings = useDeck((s) => s.settings)
  const [pressed, setPressed] = useState(false)
  const [dragValue, setDragValue] = useState<number | null>(null)
  const recognizer = useRef<GestureRecognizer | null>(null)
  const lastSlide = useRef(0)

  useEffect(() => {
    recognizer.current = new GestureRecognizer((gesture) => deckSocket.input(tile.id, gesture))
    return () => recognizer.current?.dispose()
  }, [tile.id])

  const options = {
    longPressMs: settings.longPressMs,
    doubleTapMs: settings.doubleTapMs,
    hasLongPress: tile.hasLongPress,
    hasDoubleTap: tile.hasDoubleTap,
  }

  const slide = (event: React.PointerEvent, final: boolean) => {
    const slider = tile.slider!
    const rect = event.currentTarget.getBoundingClientRect()
    const value = sliderValueAt(rect, event.clientX, event.clientY, slider.orientation === 'vertical', slider.min, slider.max, slider.step)
    setDragValue(value)
    const now = performance.now()
    if (final || now - lastSlide.current >= SLIDER_SEND_INTERVAL_MS) {
      lastSlide.current = now
      deckSocket.input(tile.id, 'change', value)
    }
  }

  const onPointerDown = (event: React.PointerEvent) => {
    if (event.button !== 0) return
    event.currentTarget.setPointerCapture(event.pointerId)
    if (editMode) return
    setPressed(true)
    if (settings.haptics) navigator.vibrate?.(10)
    if (tile.kind === 'slider' && tile.slider) slide(event, false)
    else recognizer.current?.down(options)
  }

  const onPointerMove = (event: React.PointerEvent) => {
    if (!editMode && pressed && tile.kind === 'slider' && tile.slider) slide(event, false)
  }

  const onPointerUp = (event: React.PointerEvent) => {
    if (editMode) {
      onEdit(tile.id)
      return
    }
    setPressed(false)
    if (tile.kind === 'slider' && tile.slider) {
      slide(event, true)
      // Keep showing the dragged value until the server echoes it back.
      setTimeout(() => setDragValue(null), 400)
    } else recognizer.current?.up(options)
  }

  const onPointerCancel = () => {
    setPressed(false)
    setDragValue(null)
    recognizer.current?.cancel()
  }

  return (
    <div
      data-tile={tile.id}
      className={`deck-cell${editMode ? ' editing' : ''}`}
      style={{
        gridRow: `${tile.row + 1} / span ${tile.rowSpan}`,
        gridColumn: `${tile.col + 1} / span ${tile.colSpan}`,
      }}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerCancel}
      onLostPointerCapture={() => pressed && onPointerCancel()}
      onContextMenu={(e) => e.preventDefault()}
    >
      <TileView tile={tile} theme={theme} pressed={pressed} dragValue={dragValue} />
    </div>
  )
}
