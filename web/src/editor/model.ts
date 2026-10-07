import type { Cell, Control, ControlKind, Page, Profile, Step, TileState } from '../protocol'

export function newId(): string {
  const alphabet = 'abcdefghijklmnopqrstuvwxyz0123456789'
  const bytes = crypto.getRandomValues(new Uint8Array(10))
  return Array.from(bytes, (b) => alphabet[b % alphabet.length]).join('')
}

export function newControl(kind: ControlKind, row: number, col: number): Control {
  const base: Control = {
    id: newId(),
    kind,
    position: { row, col, rowSpan: 1, colSpan: 1 },
    appearance: {},
    bindings: {},
    concurrency: 'parallel',
  }
  if (kind === 'slider')
    return {
      ...base,
      position: { ...base.position, rowSpan: 2 },
      appearance: { icon: 'mdi:volume-high', text: '{{round(value)}}' },
      slider: { min: 0, max: 100, step: 1, orientation: 'vertical' },
    }
  if (kind === 'widget')
    return {
      ...base,
      appearance: { text: '{{time.hhmm}}' },
      widget: { type: 'text', min: 0, max: 100, history: 60 },
    }
  return { ...base, appearance: { icon: 'mdi:gesture-tap-button', text: '' } }
}

export function newPage(name: string, parentId: string | null = null): Page {
  return { id: newId(), name, parentId, controls: [] }
}

export function findControl(profile: Profile, controlId: string): { page: Page; control: Control } | null {
  for (const page of profile.pages) {
    const control = page.controls.find((c) => c.id === controlId)
    if (control) return { page, control }
  }
  return null
}

export function span(cell: Cell): { rowSpan: number; colSpan: number } {
  return { rowSpan: cell.rowSpan ?? 1, colSpan: cell.colSpan ?? 1 }
}

export function covers(cell: Cell, row: number, col: number): boolean {
  const { rowSpan, colSpan } = span(cell)
  return row >= cell.row && row < cell.row + rowSpan && col >= cell.col && col < cell.col + colSpan
}

/** Controls (other than `exceptId`) overlapping the given rectangle. */
export function overlapping(page: Page, rect: Cell, exceptId?: string): Control[] {
  const { rowSpan, colSpan } = span(rect)
  return page.controls.filter((c) => {
    if (c.id === exceptId) return false
    const s = span(c.position)
    return (
      c.position.row < rect.row + rowSpan &&
      rect.row < c.position.row + s.rowSpan &&
      c.position.col < rect.col + colSpan &&
      rect.col < c.position.col + s.colSpan
    )
  })
}

export function fitsInGrid(rect: Cell, grid: Profile['grid']): boolean {
  const { rowSpan, colSpan } = span(rect)
  return rect.row >= 0 && rect.col >= 0 && rect.row + rowSpan <= grid.rows && rect.col + colSpan <= grid.cols
}

export function controlAt(page: Page, row: number, col: number): Control | undefined {
  return page.controls.find((c) => covers(c.position, row, col))
}

/** First free cell for a control of the given size, scanning row by row. */
export function firstFreeCell(page: Page, grid: Profile['grid'], rowSpan = 1, colSpan = 1): { row: number; col: number } | null {
  for (let row = 0; row < grid.rows; row++)
    for (let col = 0; col < grid.cols; col++) {
      const rect = { row, col, rowSpan, colSpan }
      if (fitsInGrid(rect, grid) && overlapping(page, rect).length === 0 && !(page.parentId && row === 0 && col === 0))
        return { row, col }
    }
  return null
}

/**
 * Moves a control to a cell of the same page. If exactly one control occupies the target and the two can
 * trade places, they are swapped. Returns false (and changes nothing) when the move is impossible.
 */
export function moveWithinPage(profile: Profile, page: Page, controlId: string, row: number, col: number): boolean {
  const control = page.controls.find((c) => c.id === controlId)
  if (!control) return false
  const target: Cell = { ...control.position, row, col }
  if (!fitsInGrid(target, profile.grid)) return false

  const blockers = overlapping(page, target, controlId)
  if (blockers.length === 0) {
    control.position = target
    return true
  }
  if (blockers.length === 1) {
    const other = blockers[0]
    const swapped: Cell = { ...other.position, row: control.position.row, col: control.position.col }
    const original = control.position
    control.position = target
    const fits = fitsInGrid(swapped, profile.grid) && overlapping(page, swapped, other.id).length === 0
    if (fits) {
      other.position = swapped
      return true
    }
    control.position = original
  }
  return false
}

/** Moves a control to another page, at the first free cell that fits it. */
export function moveToPage(profile: Profile, controlId: string, targetPageId: string): boolean {
  const found = findControl(profile, controlId)
  const target = profile.pages.find((p) => p.id === targetPageId)
  if (!found || !target || found.page.id === targetPageId) return false
  const { rowSpan, colSpan } = span(found.control.position)
  const cell = firstFreeCell(target, profile.grid, rowSpan, colSpan)
  if (!cell) return false
  found.page.controls = found.page.controls.filter((c) => c.id !== controlId)
  target.controls.push({ ...found.control, position: { ...found.control.position, ...cell } })
  return true
}

/** Deep copy of a control with a fresh id (for duplicate / paste). */
export function cloneControl(control: Control, row: number, col: number): Control {
  const copy = structuredClone(control)
  copy.id = newId()
  copy.position = { ...copy.position, row, col }
  return copy
}

/** Deletes a page and all folders below it. The home page cannot be deleted. */
export function deletePage(profile: Profile, pageId: string): boolean {
  if (pageId === profile.homePageId) return false
  const doomed = new Set([pageId])
  let grew = true
  while (grew) {
    grew = false
    for (const p of profile.pages)
      if (p.parentId && doomed.has(p.parentId) && !doomed.has(p.id)) {
        doomed.add(p.id)
        grew = true
      }
  }
  profile.pages = profile.pages.filter((p) => !doomed.has(p.id))
  return true
}

/** Pages ordered as a tree (depth-first) with their depth, for the pages panel. */
export function pageTree(profile: Profile): { page: Page; depth: number }[] {
  const result: { page: Page; depth: number }[] = []
  const visit = (parentId: string | null, depth: number, seen: Set<string>) => {
    for (const page of profile.pages.filter((p) => (p.parentId ?? null) === parentId)) {
      if (seen.has(page.id)) continue
      seen.add(page.id)
      result.push({ page, depth })
      visit(page.id, depth + 1, seen)
    }
  }
  const seen = new Set<string>()
  visit(null, 0, seen)
  // Orphans (broken parent links) still need to be reachable.
  for (const page of profile.pages) if (!seen.has(page.id)) result.push({ page, depth: 0 })
  return result
}

/** Local approximation of a rendered tile, used before the server preview arrives. */
export function controlToTile(control: Control): TileState {
  const look = control.appearance
  return {
    id: control.id,
    kind: control.kind,
    row: control.position.row,
    col: control.position.col,
    rowSpan: control.position.rowSpan ?? 1,
    colSpan: control.position.colSpan ?? 1,
    background: look.background ?? undefined,
    icon: look.icon ?? undefined,
    iconColor: look.iconColor ?? undefined,
    image: look.image && !look.image.includes('{{') ? look.image : undefined,
    imageFit: look.imageFit ?? undefined,
    text: look.text ?? undefined,
    textColor: look.textColor ?? undefined,
    fontSize: look.fontSize ?? undefined,
    textPosition: look.textPosition ?? undefined,
    hasLongPress: false,
    hasDoubleTap: false,
    slider: control.slider
      ? {
          value: control.slider.min,
          min: control.slider.min,
          max: control.slider.max,
          step: control.slider.step,
          orientation: control.slider.orientation,
          color: control.slider.color ?? null,
        }
      : undefined,
    widget: control.widget
      ? { type: control.widget.type, value: null, min: control.widget.min, max: control.widget.max, series: [], color: control.widget.color ?? null }
      : undefined,
  }
}

/** Short human summary of a step for collapsed macro rows. */
export function stepSummary(step: Step, actionName: (id: string) => string): string {
  switch (step.type) {
    case 'action': {
      const first = Object.values(step.params ?? {}).find((v) => v)
      return first ? `${actionName(step.action)}: ${first}` : actionName(step.action)
    }
    case 'delay':
      return `${step.ms} ms`
    case 'if':
      return step.condition
    case 'set':
      return `${step.name} = ${step.value}`
    case 'repeat':
      return `× ${step.count}`
  }
}
