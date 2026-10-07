import { useEffect, useRef, useState } from 'react'
import { api } from '../api'
import type { Control, TileState } from '../protocol'

const REFRESH_MS = 1000

/**
 * Asks the server to render the page's controls with the live variables (templates, states, values),
 * refreshing every second and right after edits.
 */
export function usePreview(controls: Control[] | undefined): Record<string, TileState> {
  const [tiles, setTiles] = useState<Record<string, TileState>>({})
  const latest = useRef(controls)
  useEffect(() => {
    latest.current = controls
  }, [controls])

  useEffect(() => {
    let cancelled = false
    let timer: number | undefined

    const refresh = async () => {
      const current = latest.current
      if (current && current.length > 0) {
        try {
          const rendered = await api.post<TileState[]>('/api/preview', current)
          if (!cancelled) setTiles(Object.fromEntries(rendered.map((t) => [t.id, t])))
        } catch {
          // Keep the last preview; the local fallback covers new controls.
        }
      }
      if (!cancelled) timer = window.setTimeout(refresh, REFRESH_MS)
    }

    const debounce = window.setTimeout(refresh, 150)
    return () => {
      cancelled = true
      window.clearTimeout(debounce)
      window.clearTimeout(timer)
    }
  }, [controls])

  return tiles
}
