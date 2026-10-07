import { getIconData, iconToSVG, replaceIDs } from '@iconify/utils'
import type { IconifyJSON } from '@iconify/types'
import { useEffect, useState } from 'react'
import lucideUrl from '@iconify-json/lucide/icons.json?url'
import mdiUrl from '@iconify-json/mdi/icons.json?url'

/**
 * Icons are rendered offline from bundled Iconify sets (fetched once, then cached by the browser),
 * so decks work without internet access.
 */
const SET_URLS: Record<string, string> = { mdi: mdiUrl, lucide: lucideUrl }
export const ICON_SETS = Object.keys(SET_URLS)

const sets = new Map<string, IconifyJSON>()
const loading = new Map<string, Promise<IconifyJSON>>()
const listeners = new Set<() => void>()

export function loadIconSet(prefix: string): Promise<IconifyJSON> | null {
  const url = SET_URLS[prefix]
  if (!url) return null
  let promise = loading.get(prefix)
  if (!promise) {
    promise = fetch(url)
      .then((r) => r.json() as Promise<IconifyJSON>)
      .then((json) => {
        sets.set(prefix, json)
        for (const notify of listeners) notify()
        return json
      })
    loading.set(prefix, promise)
  }
  return promise
}

export function getLoadedSet(prefix: string): IconifyJSON | undefined {
  return sets.get(prefix)
}

function useIconSet(prefix: string): IconifyJSON | undefined {
  const [, force] = useState(0)
  useEffect(() => {
    if (sets.has(prefix)) return
    const notify = () => force((n) => n + 1)
    listeners.add(notify)
    void loadIconSet(prefix)
    return () => {
      listeners.delete(notify)
    }
  }, [prefix])
  return sets.get(prefix)
}

const svgCache = new Map<string, { body: string; viewBox: string } | null>()

function renderIcon(set: IconifyJSON, name: string) {
  const key = `${set.prefix}:${name}`
  if (!svgCache.has(key)) {
    const data = getIconData(set, name)
    if (!data) svgCache.set(key, null)
    else {
      const svg = iconToSVG(data, { height: '1em' })
      svgCache.set(key, { body: replaceIDs(svg.body), viewBox: svg.attributes.viewBox })
    }
  }
  return svgCache.get(key)
}

interface IconProps {
  /** "mdi:play" style name. */
  name: string
  className?: string
  style?: React.CSSProperties
}

export function Icon({ name, className, style }: IconProps) {
  const [prefix, iconName] = name.includes(':') ? name.split(':', 2) : ['mdi', name]
  const set = useIconSet(prefix)
  const svg = set ? renderIcon(set, iconName) : undefined
  if (!svg) return <span className={className} style={style} />
  return (
    <svg
      className={className}
      style={style}
      viewBox={svg.viewBox}
      width="1em"
      height="1em"
      aria-hidden="true"
      dangerouslySetInnerHTML={{ __html: svg.body }}
    />
  )
}

/** Icon names of a loaded set matching a search, for the icon picker. */
export function searchIcons(prefix: string, query: string, limit: number): string[] {
  const set = sets.get(prefix)
  if (!set) return []
  const words = query.toLowerCase().split(/\s+/).filter(Boolean)
  const names = [...Object.keys(set.icons), ...Object.keys(set.aliases ?? {})]
  const result: string[] = []
  for (const name of names) {
    if (words.every((w) => name.includes(w))) {
      result.push(`${prefix}:${name}`)
      if (result.length >= limit) break
    }
  }
  return result
}
