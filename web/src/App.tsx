import { BrowserRouter, Route, Routes } from 'react-router'
import { DeckPage } from './deck/DeckPage'
import { EditorPage } from './editor/EditorPage'
import { PairPage } from './pair/PairPage'

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<DeckPage />} />
        <Route path="/editor/*" element={<EditorPage />} />
        <Route path="/pair" element={<PairPage />} />
      </Routes>
    </BrowserRouter>
  )
}
