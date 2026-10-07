import { create } from 'zustand'
import { ApiError, api } from '../api'
import type { Control, Profile, ProfileListItem } from '../protocol'
import { findControl } from './model'

const HISTORY_LIMIT = 100
const COALESCE_MS = 1000
const SAVE_DELAY_MS = 600

export type Selection = { kind: 'control'; id: string } | { kind: 'cell'; row: number; col: number } | null

interface EditorState {
  profiles: ProfileListItem[]
  profile: Profile | null
  /** Server revision our edits are based on (sent back for optimistic concurrency). */
  baseRevision: number
  pageId: string | null
  selection: Selection
  past: Profile[]
  future: Profile[]
  dirty: boolean
  saving: boolean
  error: string | null
  /** Someone else saved this profile meanwhile; holds their version. */
  conflict: Profile | null

  loadProfiles: () => Promise<void>
  open: (profileId: string) => Promise<void>
  /** Applies a mutation to a copy of the profile, records undo history and schedules a save. */
  update: (mutate: (draft: Profile) => void, coalesceKey?: string) => void
  updateControl: (controlId: string, mutate: (control: Control) => void, coalesceKey?: string) => void
  undo: () => void
  redo: () => void
  save: (force?: boolean) => Promise<void>
  reloadFromServer: () => Promise<void>
  selectPage: (pageId: string) => void
  select: (selection: Selection) => void
}

let saveTimer: number | undefined
let lastKey: string | undefined
let lastKeyTime = 0

export const useEditor = create<EditorState>((set, get) => ({
  profiles: [],
  profile: null,
  baseRevision: 0,
  pageId: null,
  selection: null,
  past: [],
  future: [],
  dirty: false,
  saving: false,
  error: null,
  conflict: null,

  loadProfiles: async () => {
    set({ profiles: await api.get<ProfileListItem[]>('/api/profiles') })
  },

  open: async (profileId) => {
    if (get().dirty) await get().save()
    const profile = await api.get<Profile>(`/api/profiles/${profileId}`)
    set({
      profile,
      baseRevision: profile.revision,
      pageId: profile.homePageId,
      selection: null,
      past: [],
      future: [],
      dirty: false,
      conflict: null,
      error: null,
    })
    try {
      localStorage.setItem('ovsd.editor.profile', profileId)
    } catch {
      // ignore
    }
  },

  update: (mutate, coalesceKey) => {
    const { profile, past } = get()
    if (!profile) return
    const draft = structuredClone(profile)
    mutate(draft)

    const now = Date.now()
    const coalesce = coalesceKey !== undefined && coalesceKey === lastKey && now - lastKeyTime < COALESCE_MS
    lastKey = coalesceKey
    lastKeyTime = now

    set({
      profile: draft,
      past: coalesce ? past : [...past.slice(-HISTORY_LIMIT + 1), profile],
      future: [],
      dirty: true,
    })
    scheduleSave()
  },

  updateControl: (controlId, mutate, coalesceKey) =>
    get().update((draft) => {
      const found = findControl(draft, controlId)
      if (found) mutate(found.control)
    }, coalesceKey ?? `control:${controlId}`),

  undo: () => {
    const { past, profile, future } = get()
    if (!profile || past.length === 0) return
    lastKey = undefined
    set({ profile: past[past.length - 1], past: past.slice(0, -1), future: [profile, ...future], dirty: true })
    fixPage()
    scheduleSave()
  },

  redo: () => {
    const { past, profile, future } = get()
    if (!profile || future.length === 0) return
    lastKey = undefined
    set({ profile: future[0], past: [...past, profile], future: future.slice(1), dirty: true })
    fixPage()
    scheduleSave()
  },

  save: async (force = false) => {
    window.clearTimeout(saveTimer)
    const { profile, baseRevision, saving } = get()
    if (!profile || saving) return
    set({ saving: true, error: null })
    try {
      const saved = await api.put<Profile>(
        `/api/profiles/${profile.id}${force ? '?force=true' : ''}`,
        { ...profile, revision: baseRevision },
      )
      // Edits made while the request was in flight stay dirty and get saved next.
      const stillSame = get().profile === profile
      set({ baseRevision: saved.revision, dirty: !stillSame, conflict: null })
      if (!stillSame) scheduleSave()
      void get().loadProfiles()
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) set({ conflict: e.body as Profile })
      else set({ error: (e as Error).message })
    } finally {
      set({ saving: false })
    }
  },

  reloadFromServer: async () => {
    const { profile } = get()
    if (!profile) return
    const fresh = await api.get<Profile>(`/api/profiles/${profile.id}`)
    set({ profile: fresh, baseRevision: fresh.revision, dirty: false, conflict: null, past: [], future: [] })
    fixPage()
  },

  selectPage: (pageId) => set({ pageId, selection: null }),
  select: (selection) => set({ selection }),
}))

function scheduleSave() {
  window.clearTimeout(saveTimer)
  saveTimer = window.setTimeout(() => void useEditor.getState().save(), SAVE_DELAY_MS)
}

/** After undo/redo the current page or selection may no longer exist. */
function fixPage() {
  const { profile, pageId, selection } = useEditor.getState()
  if (!profile) return
  const page = profile.pages.find((p) => p.id === pageId) ?? profile.pages.find((p) => p.id === profile.homePageId)
  const selectionValid = selection?.kind !== 'control' || page?.controls.some((c) => c.id === selection.id)
  useEditor.setState({ pageId: page?.id ?? null, selection: selectionValid ? selection : null })
}

/** Current page of the profile being edited. */
export function useCurrentPage() {
  return useEditor((s) => s.profile?.pages.find((p) => p.id === s.pageId) ?? null)
}

/** The selected control, if any. */
export function useSelectedControl() {
  return useEditor((s) => {
    if (s.selection?.kind !== 'control' || !s.profile) return null
    return findControl(s.profile, s.selection.id)?.control ?? null
  })
}

// ------------------------------------------------------------------ clipboard (survives profile switches)

const CLIPBOARD_KEY = 'ovsd.clipboard'

export function copyToClipboard(control: Control) {
  try {
    localStorage.setItem(CLIPBOARD_KEY, JSON.stringify(control))
  } catch {
    // ignore
  }
}

export function readClipboard(): Control | null {
  try {
    const raw = localStorage.getItem(CLIPBOARD_KEY)
    return raw ? (JSON.parse(raw) as Control) : null
  } catch {
    return null
  }
}
