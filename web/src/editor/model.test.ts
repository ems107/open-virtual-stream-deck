import { describe, expect, it } from 'vitest'
import type { Control, Profile } from '../protocol'
import { applyTilePatch } from '../state/deck'
import { chordFromEvent, keyName } from './inspector/HotkeyInput'
import { isVisible } from './inspector/ActionParamsForm'
import { cloneControl, deletePage, firstFreeCell, moveToPage, moveWithinPage, newControl, pageTree } from './model'

function control(id: string, row: number, col: number, rowSpan = 1, colSpan = 1): Control {
  return { ...newControl('button', row, col), id, position: { row, col, rowSpan, colSpan } }
}

function profile(...controls: Control[]): Profile {
  return {
    id: 'p',
    name: 'P',
    revision: 1,
    grid: { rows: 2, cols: 3 },
    homePageId: 'home',
    pages: [
      { id: 'home', name: 'Home', parentId: null, controls },
      { id: 'folder', name: 'Folder', parentId: 'home', controls: [] },
      { id: 'sub', name: 'Sub', parentId: 'folder', controls: [] },
    ],
    theme: { background: '#000', tileBackground: '#111', textColor: '#fff', gap: 8, radius: 8 },
    matchRules: [],
  }
}

describe('editor model', () => {
  it('moves into a free cell', () => {
    const p = profile(control('a', 0, 0))
    expect(moveWithinPage(p, p.pages[0], 'a', 1, 2)).toBe(true)
    expect(p.pages[0].controls[0].position).toMatchObject({ row: 1, col: 2 })
  })

  it('swaps two single tiles', () => {
    const p = profile(control('a', 0, 0), control('b', 0, 1))
    expect(moveWithinPage(p, p.pages[0], 'a', 0, 1)).toBe(true)
    const [a, b] = p.pages[0].controls
    expect(a.position).toMatchObject({ row: 0, col: 1 })
    expect(b.position).toMatchObject({ row: 0, col: 0 })
  })

  it('refuses moves outside the grid or that cannot swap', () => {
    const p = profile(control('wide', 0, 0, 1, 2), control('b', 1, 0), control('c', 1, 1))
    expect(moveWithinPage(p, p.pages[0], 'wide', 0, 2)).toBe(false)
    expect(moveWithinPage(p, p.pages[0], 'wide', 1, 0)).toBe(false)
    expect(p.pages[0].controls[0].position).toMatchObject({ row: 0, col: 0 })
  })

  it('first free cell skips the back tile slot of folders', () => {
    const p = profile()
    expect(firstFreeCell(p.pages[1], p.grid)).toEqual({ row: 0, col: 1 })
    expect(firstFreeCell(p.pages[0], p.grid)).toEqual({ row: 0, col: 0 })
  })

  it('moves a control to another page', () => {
    const p = profile(control('a', 1, 2))
    expect(moveToPage(p, 'a', 'folder')).toBe(true)
    expect(p.pages[0].controls).toHaveLength(0)
    expect(p.pages[1].controls[0].position).toMatchObject({ row: 0, col: 1 })
  })

  it('deleting a page removes nested folders but never home', () => {
    const p = profile()
    expect(deletePage(p, 'home')).toBe(false)
    expect(deletePage(p, 'folder')).toBe(true)
    expect(p.pages.map((x) => x.id)).toEqual(['home'])
  })

  it('pageTree orders pages depth-first', () => {
    expect(pageTree(profile()).map((x) => `${x.page.id}:${x.depth}`)).toEqual(['home:0', 'folder:1', 'sub:2'])
  })

  it('clones with a new id and deep copies', () => {
    const original = control('a', 0, 0)
    original.bindings.tap = [{ type: 'delay', ms: 5 }]
    const copy = cloneControl(original, 1, 1)
    expect(copy.id).not.toBe('a')
    copy.bindings.tap!.push({ type: 'delay', ms: 1 })
    expect(original.bindings.tap).toHaveLength(1)
  })
})

describe('tiles patch', () => {
  const tile = (id: string, text: string) => ({ id, row: 0, col: 0, rowSpan: 1, colSpan: 1, kind: 'button' as const, text, hasLongPress: false, hasDoubleTap: false })
  it('reset replaces, otherwise merges and removes', () => {
    let state = applyTilePatch({}, { type: 'tiles', tiles: [tile('a', '1'), tile('b', '1')], removed: [], reset: true })
    state = applyTilePatch(state, { type: 'tiles', tiles: [tile('a', '2')], removed: ['b'], reset: false })
    expect(Object.keys(state)).toEqual(['a'])
    expect(state.a.text).toBe('2')
    state = applyTilePatch(state, { type: 'tiles', tiles: [tile('c', '1')], removed: [], reset: true })
    expect(Object.keys(state)).toEqual(['c'])
  })
})

describe('hotkeys', () => {
  it('maps physical key codes to server key names', () => {
    expect(keyName('KeyA')).toBe('A')
    expect(keyName('Digit7')).toBe('7')
    expect(keyName('F13')).toBe('F13')
    expect(keyName('Numpad5')).toBe('Num5')
    expect(keyName('ArrowLeft')).toBe('Left')
    expect(keyName('F25')).toBeNull()
  })

  it('builds chords and waits for a non-modifier key', () => {
    const e = { code: 'KeyM', ctrlKey: true, shiftKey: true, altKey: false, metaKey: false }
    expect(chordFromEvent(e)).toBe('Ctrl+Shift+M')
    expect(chordFromEvent({ ...e, code: 'ShiftLeft' })).toBeNull()
  })
})

describe('showIf', () => {
  const descriptor = {
    id: 'x',
    category: 'c',
    name: 'X',
    params: [
      { name: 'target', label: 'T', type: 'select' as const, required: false, default: 'master', allowCustom: false },
      { name: 'app', label: 'A', type: 'text' as const, required: false, allowCustom: false, showIf: 'target=app' },
      { name: 'body', label: 'B', type: 'text' as const, required: false, allowCustom: false, showIf: 'target=app,mic' },
    ],
  }
  it('uses the other parameter value or its default', () => {
    expect(isVisible(descriptor.params[1], {}, descriptor)).toBe(false)
    expect(isVisible(descriptor.params[1], { target: 'app' }, descriptor)).toBe(true)
    expect(isVisible(descriptor.params[2], { target: 'mic' }, descriptor)).toBe(true)
  })
})
