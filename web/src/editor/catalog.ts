import { useEffect, useState } from 'react'
import { create } from 'zustand'
import { api } from '../api'
import type { ActionDescriptor, OptionItem } from '../protocol'

interface CatalogState {
  actions: ActionDescriptor[]
  byId: Record<string, ActionDescriptor>
  keys: string[]
  loaded: boolean
  load: () => Promise<void>
}

/** Server-provided catalog of actions (with their parameter schemas) and key names. */
export const useCatalog = create<CatalogState>((set, get) => ({
  actions: [],
  byId: {},
  keys: [],
  loaded: false,
  load: async () => {
    if (get().loaded) return
    const [actions, keys] = await Promise.all([api.get<ActionDescriptor[]>('/api/actions'), api.get<string[]>('/api/keys')])
    set({ actions, keys, byId: Object.fromEntries(actions.map((a) => [a.id, a])), loaded: true })
  },
}))

/** Dynamic select options (OBS scenes, audio devices...), refreshed each time a form opens. */
export function useOptions(source: string | null | undefined): OptionItem[] {
  const [options, setOptions] = useState<OptionItem[]>([])
  useEffect(() => {
    if (!source) return
    let cancelled = false
    api
      .get<OptionItem[]>(`/api/options/${source}`)
      .then((items) => !cancelled && setOptions(items))
      .catch(() => !cancelled && setOptions([]))
    return () => {
      cancelled = true
    }
  }, [source])
  return options
}

/** Names of the variables currently known by the server, for autocompletion. */
export function useVariableNames(): string[] {
  const [names, setNames] = useState<string[]>([])
  useEffect(() => {
    api
      .get<Record<string, unknown>>('/api/variables')
      .then((vars) => setNames(Object.keys(vars)))
      .catch(() => setNames([]))
  }, [])
  return names
}
