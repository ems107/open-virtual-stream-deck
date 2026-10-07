import NoSleep from 'nosleep.js'

/**
 * Keeps the screen on. Uses the Wake Lock API when available (HTTPS/localhost) and otherwise
 * NoSleep's hidden looping video, which works over plain HTTP on the LAN.
 * Must be enabled from a user gesture.
 */
const noSleep = new NoSleep()
const STORAGE_KEY = 'ovsd.keepAwake'

export function keepAwakePreferred(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) !== 'false'
  } catch {
    return true
  }
}

export function setKeepAwakePreferred(enabled: boolean) {
  try {
    localStorage.setItem(STORAGE_KEY, String(enabled))
  } catch {
    // ignore
  }
  if (enabled) enableKeepAwake()
  else noSleep.disable()
}

export function enableKeepAwake() {
  if (!keepAwakePreferred() || noSleep.isEnabled) return
  noSleep.enable().catch(() => {
    // Autoplay refused; the next tap will try again.
  })
}

export function isKeepAwakeActive(): boolean {
  return noSleep.isEnabled
}
