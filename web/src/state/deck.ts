import { create } from 'zustand'
import type { DeckSettings, LayoutMessage, NotifyLevel, TileState, TilesMessage } from '../protocol'

export interface Toast {
  id: number
  level: NotifyLevel
  message: string
}

interface DeckState {
  layout: LayoutMessage | null
  tiles: Record<string, TileState>
  settings: DeckSettings
  toasts: Toast[]
  setLayout: (layout: LayoutMessage) => void
  applyTiles: (message: TilesMessage) => void
  toast: (level: NotifyLevel, message: string) => void
  dismiss: (id: number) => void
}

let toastId = 0

export const DEFAULT_DECK_SETTINGS: DeckSettings = { longPressMs: 500, doubleTapMs: 250, haptics: true }

export const useDeck = create<DeckState>((set) => ({
  layout: null,
  tiles: {},
  settings: DEFAULT_DECK_SETTINGS,
  toasts: [],
  setLayout: (layout) => set({ layout }),
  applyTiles: (message) => set((state) => ({ tiles: applyTilePatch(state.tiles, message) })),
  toast: (level, message) => {
    const id = ++toastId
    set((state) => ({ toasts: [...state.toasts.slice(-3), { id, level, message }] }))
    setTimeout(() => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })), level === 'error' ? 5000 : 3000)
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })),
}))

/** Pure reducer for a tiles message (exported for tests). */
export function applyTilePatch(current: Record<string, TileState>, message: TilesMessage): Record<string, TileState> {
  const next: Record<string, TileState> = message.reset ? {} : { ...current }
  for (const id of message.removed) delete next[id]
  for (const tile of message.tiles) next[tile.id] = tile
  return next
}
