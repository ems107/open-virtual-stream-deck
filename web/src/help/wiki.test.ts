import { describe, expect, it } from 'vitest'
import { allPages, generatedPages, pageSource, pageTitle, search } from './wiki'

const written = allPages.filter((id) => !generatedPages.has(id))

describe('built-in guide', () => {
  it.each(['es', 'en'])('has every page in %s with a title', (lang) => {
    for (const id of written) {
      const source = pageSource(id, lang)
      expect(source, `${lang}/${id}.md`).toBeTruthy()
      expect(source!.startsWith('# '), `${lang}/${id}.md starts with a title`).toBe(true)
      expect(pageTitle(id, lang, '')).not.toBe('')
    }
  })

  it('only links to pages that exist', () => {
    for (const lang of ['es', 'en']) {
      for (const id of written) {
        for (const [, target] of pageSource(id, lang)!.matchAll(/\]\(#([^)]+)\)/g)) {
          expect(allPages, `${lang}/${id}.md links to #${target}`).toContain(target)
        }
      }
    }
  })

  it('finds pages ignoring case and accents', () => {
    expect(search('TEMPERATURA cpu', 'es').map((h) => h.id)).toContain('windows')
    expect(search('electrico inexistente xyz', 'es')).toEqual([])
    expect(search('obs websocket', 'en')[0].id).toBe('obs')
  })
})
