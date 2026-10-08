import { Marked } from 'marked'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import { Icon } from '../icons'
import { ReferenceActions, ReferenceFunctions, ReferenceKeys } from './Reference'
import { allPages, generatedPages, pageSource, pageTitle, search, sections } from './wiki'

const escapeHtml = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

// Code blocks get a copy button; everything else is plain Markdown. The pages are part of the app, not
// user content, so rendering their HTML is safe.
const markdown = new Marked({
  gfm: true,
  renderer: {
    code({ text }) {
      return `<div class="code-block"><button type="button" class="copy-code" data-copy>${escapeHtml('⧉')}</button><pre><code>${escapeHtml(text)}</code></pre></div>`
    },
  },
})

/** Editor "Help" tab: the built-in guide, with search, and live reference pages. */
export function HelpPanel() {
  const { t, i18n } = useTranslation()
  const params = useParams()
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const pageId = params.page && allPages.includes(params.page) ? params.page : 'welcome'
  const lang = i18n.language

  const title = (id: string) => (generatedPages.has(id) ? t(`wiki.pages.${id}`) : pageTitle(id, lang, id))
  const html = useMemo(() => {
    if (generatedPages.has(pageId)) return null
    return markdown.parse(pageSource(pageId, lang) ?? '', { async: false })
  }, [pageId, lang])
  const results = useMemo(() => search(query, lang), [query, lang])

  useEffect(() => {
    document.querySelector('.help-content')?.scrollTo({ top: 0 })
  }, [pageId])

  const open = (id: string) => {
    setQuery('')
    navigate(`/editor/help/${id}`)
  }

  // Links between pages are "#page-id"; external links open in a new tab; code blocks copy.
  const onArticleClick = (event: React.MouseEvent) => {
    const target = event.target as HTMLElement
    const copy = target.closest<HTMLElement>('[data-copy]')
    if (copy) {
      const code = copy.parentElement?.querySelector('code')?.textContent ?? ''
      void navigator.clipboard?.writeText(code)
      copy.classList.add('copied')
      setTimeout(() => copy.classList.remove('copied'), 1200)
      return
    }
    const link = target.closest('a')
    const href = link?.getAttribute('href')
    if (!link || !href) return
    event.preventDefault()
    if (href.startsWith('#')) open(href.slice(1))
    else if (href.startsWith('/')) navigate(href)
    else window.open(href, '_blank', 'noopener')
  }

  const index = allPages.indexOf(pageId)
  const previous = index > 0 ? allPages[index - 1] : null
  const next = index < allPages.length - 1 ? allPages[index + 1] : null

  return (
    <div className="help-layout">
      <aside className="help-sidebar">
        <div className="help-search">
          <Icon name="mdi:magnify" />
          <input value={query} placeholder={t('wiki.search')} onChange={(e) => setQuery(e.target.value)} />
          {query && (
            <button className="icon-button" onClick={() => setQuery('')} title={t('common.clear')}>
              <Icon name="mdi:close" />
            </button>
          )}
        </div>
        {query ? (
          <ul className="help-results">
            {results.length === 0 && <li className="muted small">{t('wiki.noResults')}</li>}
            {results.map((hit) => (
              <li key={hit.id}>
                <button onClick={() => open(hit.id)}>
                  <strong>{hit.title}</strong>
                  <span className="muted small">{hit.snippet}</span>
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <nav>
            {sections.map((section) => (
              <div key={section.id} className="help-section">
                <h3>{t(`wiki.sections.${section.id}`)}</h3>
                {section.pages.map((id) => (
                  <button key={id} className={id === pageId ? 'active' : undefined} onClick={() => open(id)}>
                    {title(id)}
                  </button>
                ))}
              </div>
            ))}
          </nav>
        )}
      </aside>

      <div className="help-content">
        <article className="wiki" onClick={onArticleClick}>
          {pageId === 'reference-actions' && <ReferenceActions />}
          {pageId === 'reference-functions' && <ReferenceFunctions />}
          {pageId === 'reference-keys' && <ReferenceKeys />}
          {html !== null && <div dangerouslySetInnerHTML={{ __html: html }} />}
        </article>
        <footer className="wiki-pager">
          {previous ? (
            <button onClick={() => open(previous)}>
              <Icon name="mdi:chevron-left" /> {title(previous)}
            </button>
          ) : (
            <span />
          )}
          {next && (
            <button onClick={() => open(next)}>
              {title(next)} <Icon name="mdi:chevron-right" />
            </button>
          )}
        </footer>
      </div>
    </div>
  )
}
