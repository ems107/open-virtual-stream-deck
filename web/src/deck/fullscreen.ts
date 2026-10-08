import { useEffect, useState } from 'react'

/**
 * Fullscreen deck. The choice is remembered per device: after a reload or reopening the browser the
 * deck offers to go back to fullscreen (browsers only allow entering it from a tap).
 */
const STORAGE_KEY = 'ovsd.fullscreen'

export function fullscreenPreferred(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'true'
  } catch {
    return false
  }
}

export function setFullscreenPreferred(enabled: boolean) {
  try {
    localStorage.setItem(STORAGE_KEY, String(enabled))
  } catch {
    // ignore
  }
}

export function fullscreenSupported(): boolean {
  return typeof document.documentElement.requestFullscreen === 'function'
}

/** Enters fullscreen and, on Android, turns the screen to match the grid (a 3x5 deck wants landscape). */
export async function enterFullscreen(grid?: { rows: number; cols: number }) {
  await document.documentElement.requestFullscreen({ navigationUI: 'hide' })
  if (grid && grid.rows !== grid.cols) {
    const orientation = screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> }
    await orientation.lock?.(grid.cols > grid.rows ? 'landscape' : 'portrait').catch(() => {})
  }
}

export function useIsFullscreen(): boolean {
  const [fullscreen, setFullscreen] = useState(!!document.fullscreenElement)
  useEffect(() => {
    const update = () => setFullscreen(!!document.fullscreenElement)
    document.addEventListener('fullscreenchange', update)
    return () => document.removeEventListener('fullscreenchange', update)
  }, [])
  return fullscreen
}
