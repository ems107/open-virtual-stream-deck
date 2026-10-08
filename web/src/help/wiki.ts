// The built-in guide: Markdown pages bundled with the app (docs/<lang>/<id>.md), grouped in sections.
// Pages link to each other with [text](#page-id). Reference pages are generated from live data.

const sources = import.meta.glob('./docs/*/*.md', { query: '?raw', import: 'default', eager: true }) as Record<string, string>

export interface WikiSection {
  id: string
  pages: string[]
}

/** Table of contents. Ids are shared by every language; "reference-*" pages are React components. */
export const sections: WikiSection[] = [
  { id: 'start', pages: ['welcome', 'getting-started', 'on-the-phone'] },
  { id: 'build', pages: ['editor', 'tiles', 'actions', 'states', 'variables', 'profiles'] },
  { id: 'integrations', pages: ['windows', 'obs', 'discord', 'home-assistant', 'http-webhooks'] },
  { id: 'more', pages: ['recipes', 'troubleshooting', 'backups'] },
  { id: 'reference', pages: ['reference-actions', 'reference-functions', 'reference-keys'] },
]

export const generatedPages = new Set(['reference-actions', 'reference-functions', 'reference-keys'])

export const allPages = sections.flatMap((s) => s.pages)

function language(lang: string): 'es' | 'en' {
  return lang.startsWith('es') ? 'es' : 'en'
}

/** Markdown of a page in the UI language (falls back to English, then Spanish). */
export function pageSource(id: string, lang: string): string | null {
  return sources[`./docs/${language(lang)}/${id}.md`] ?? sources[`./docs/en/${id}.md`] ?? sources[`./docs/es/${id}.md`] ?? null
}

/** Page title: its first "# " heading. */
export function pageTitle(id: string, lang: string, fallback: string): string {
  const source = pageSource(id, lang)
  const heading = source?.match(/^# (.+)$/m)
  return heading ? heading[1].trim() : fallback
}

export function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
}

export interface SearchHit {
  id: string
  title: string
  snippet: string
}

/** Pages containing every word of the query; title matches first. */
export function search(query: string, lang: string): SearchHit[] {
  const words = normalize(query).split(/\s+/).filter((w) => w.length > 1)
  if (words.length === 0) return []
  const hits: (SearchHit & { score: number })[] = []
  for (const id of allPages) {
    const source = pageSource(id, lang)
    if (!source) continue
    const title = pageTitle(id, lang, id)
    // Search the readable text, not the Markdown syntax.
    const plain = source.replace(/[#*`>|_[\]]/g, ' ').replace(/\(#[^)]*\)/g, ' ')
    const haystack = normalize(plain)
    if (!words.every((w) => haystack.includes(w))) continue
    const normalizedTitle = normalize(title)
    const score = words.filter((w) => normalizedTitle.includes(w)).length * 100 + words.reduce((n, w) => n + haystack.split(w).length - 1, 0)
    const at = haystack.indexOf(words[0])
    // Start the snippet at a word boundary a little before the first match.
    let start = Math.max(0, at - 60)
    if (start > 0) start = plain.indexOf(' ', start) + 1 || start
    const snippet = (start > 0 ? '…' : '') + plain.slice(start, at + 120).replace(/\s+/g, ' ').trim() + '…'
    hits.push({ id, title, snippet, score })
  }
  return hits.sort((a, b) => b.score - a.score)
}
