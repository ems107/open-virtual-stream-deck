import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import './i18n'
import './styles/base.css'
import './styles/deck.css'
import './styles/editor.css'
import { deckSocket } from './state/connection'

// The editor and the pairing page only need change notifications; everything else is a deck.
const isEditor = location.pathname.startsWith('/editor') || location.pathname.startsWith('/pair')
deckSocket.start(isEditor ? 'editor' : 'deck')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
