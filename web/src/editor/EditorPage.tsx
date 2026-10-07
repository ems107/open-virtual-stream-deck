import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Route, Routes } from 'react-router'
import { StatusBadge } from '../StatusBadge'
import { Toasts } from '../components/Toasts'
import { Icon } from '../icons'
import { DevicesPanel } from './DevicesPanel'
import { HelpPanel } from './HelpPanel'
import { ProfileEditor } from './ProfileEditor'
import { SettingsPanel } from './SettingsPanel'
import { VariablesPanel } from './VariablesPanel'

export function EditorPage() {
  const { t } = useTranslation()
  useEffect(() => {
    document.title = 'OVSD · Editor'
  }, [])

  const links = [
    { to: '/editor', icon: 'mdi:view-dashboard-edit-outline', label: t('nav.profiles'), end: true },
    { to: '/editor/devices', icon: 'mdi:tablet-cellphone', label: t('nav.devices') },
    { to: '/editor/settings', icon: 'mdi:cog-outline', label: t('nav.settings') },
    { to: '/editor/variables', icon: 'mdi:variable', label: t('nav.variables') },
    { to: '/editor/help', icon: 'mdi:help-circle-outline', label: t('nav.help') },
  ]

  return (
    <div className="editor-page">
      <header className="editor-header">
        <a className="brand" href="/editor">
          <Icon name="mdi:view-grid-plus" /> OVSD
        </a>
        <nav>
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} end={l.end}>
              <Icon name={l.icon} />
              <span>{l.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="header-right">
          <StatusBadge />
          <a className="button-link" href="/" title={t('nav.openDeck')}>
            <Icon name="mdi:play-box-outline" />
          </a>
        </div>
      </header>
      <main className="editor-main">
        <Routes>
          <Route index element={<ProfileEditor />} />
          <Route path="devices" element={<DevicesPanel />} />
          <Route path="settings" element={<SettingsPanel />} />
          <Route path="variables" element={<VariablesPanel />} />
          <Route path="help" element={<HelpPanel />} />
        </Routes>
      </main>
      <Toasts />
    </div>
  )
}
