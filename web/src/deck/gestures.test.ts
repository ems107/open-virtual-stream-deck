import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Gesture } from '../protocol'
import { GestureRecognizer, sliderValueAt, type GestureOptions } from './gestures'

const base: GestureOptions = { longPressMs: 500, doubleTapMs: 250, hasLongPress: false, hasDoubleTap: false }

describe('GestureRecognizer', () => {
  let events: Gesture[]
  let recognizer: GestureRecognizer

  beforeEach(() => {
    vi.useFakeTimers()
    events = []
    recognizer = new GestureRecognizer((g) => events.push(g))
  })
  afterEach(() => vi.useRealTimers())

  it('emits press, release and an immediate tap without extra bindings', () => {
    recognizer.down(base)
    recognizer.up(base)
    expect(events).toEqual(['press', 'release', 'tap'])
  })

  it('emits long press instead of tap when held', () => {
    const options = { ...base, hasLongPress: true }
    recognizer.down(options)
    vi.advanceTimersByTime(600)
    recognizer.up(options)
    expect(events).toEqual(['press', 'longPress', 'release'])
  })

  it('short press with a long-press binding is still a tap', () => {
    const options = { ...base, hasLongPress: true }
    recognizer.down(options)
    vi.advanceTimersByTime(100)
    recognizer.up(options)
    expect(events).toEqual(['press', 'release', 'tap'])
  })

  it('detects double tap and delays single taps', () => {
    const options = { ...base, hasDoubleTap: true }
    recognizer.down(options)
    recognizer.up(options)
    expect(events).not.toContain('tap')
    vi.advanceTimersByTime(100)
    recognizer.down(options)
    recognizer.up(options)
    expect(events.filter((e) => e === 'doubleTap')).toHaveLength(1)
    expect(events).not.toContain('tap')

    recognizer.down(options)
    recognizer.up(options)
    vi.advanceTimersByTime(300)
    expect(events.at(-1)).toBe('tap')
  })

  it('cancel releases without tapping', () => {
    recognizer.down(base)
    recognizer.cancel()
    expect(events).toEqual(['press', 'release'])
  })
})

describe('sliderValueAt', () => {
  const rect = { left: 0, top: 0, width: 100, height: 200 }
  it('maps vertical position with bottom as minimum and snaps to step', () => {
    expect(sliderValueAt(rect, 50, 200, true, 0, 100, 1)).toBe(0)
    expect(sliderValueAt(rect, 50, 0, true, 0, 100, 1)).toBe(100)
    expect(sliderValueAt(rect, 50, 101, true, 0, 100, 5)).toBe(50)
  })
  it('maps horizontal position and clamps outside the track', () => {
    expect(sliderValueAt(rect, 25, 0, false, 0, 10, 0.5)).toBe(2.5)
    expect(sliderValueAt(rect, -50, 0, false, 0, 10, 1)).toBe(0)
    expect(sliderValueAt(rect, 500, 0, false, 0, 10, 1)).toBe(10)
  })
})
