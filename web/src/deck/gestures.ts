import type { Gesture } from '../protocol'

export interface GestureOptions {
  longPressMs: number
  doubleTapMs: number
  /** Only when the tile has a long-press binding do we hold back the tap. */
  hasLongPress: boolean
  /** Only when the tile has a double-tap binding do we wait for a second tap. */
  hasDoubleTap: boolean
}

/**
 * Turns pointer down/up into deck gestures. "press"/"release" are always emitted immediately
 * (push-to-talk); "tap" is delayed only as much as the tile's bindings require.
 */
export class GestureRecognizer {
  private longTimer: ReturnType<typeof setTimeout> | undefined
  private tapTimer: ReturnType<typeof setTimeout> | undefined
  private longFired = false
  private pressed = false
  private readonly emit: (gesture: Gesture) => void

  constructor(emit: (gesture: Gesture) => void) {
    this.emit = emit
  }

  down(options: GestureOptions) {
    if (this.pressed) return
    this.pressed = true
    this.longFired = false
    this.emit('press')
    if (options.hasLongPress) {
      this.longTimer = setTimeout(() => {
        this.longFired = true
        this.emit('longPress')
      }, options.longPressMs)
    }
  }

  up(options: GestureOptions) {
    if (!this.pressed) return
    this.pressed = false
    clearTimeout(this.longTimer)
    this.emit('release')
    if (this.longFired) return

    if (!options.hasDoubleTap) {
      this.emit('tap')
    } else if (this.tapTimer !== undefined) {
      clearTimeout(this.tapTimer)
      this.tapTimer = undefined
      this.emit('doubleTap')
    } else {
      this.tapTimer = setTimeout(() => {
        this.tapTimer = undefined
        this.emit('tap')
      }, options.doubleTapMs)
    }
  }

  /** Pointer left or was cancelled: release without tapping. */
  cancel() {
    if (!this.pressed) return
    this.pressed = false
    clearTimeout(this.longTimer)
    this.emit('release')
  }

  dispose() {
    clearTimeout(this.longTimer)
    clearTimeout(this.tapTimer)
  }
}

/** Maps a pointer position inside a slider track to a value (bottom/left = min). */
export function sliderValueAt(
  rect: { left: number; top: number; width: number; height: number },
  x: number,
  y: number,
  vertical: boolean,
  min: number,
  max: number,
  step: number,
): number {
  const ratio = vertical ? 1 - (y - rect.top) / rect.height : (x - rect.left) / rect.width
  const clamped = Math.min(1, Math.max(0, ratio))
  let value = min + clamped * (max - min)
  if (step > 0) value = min + Math.round((value - min) / step) * step
  return Math.min(max, Math.max(min, Number(value.toFixed(6))))
}
