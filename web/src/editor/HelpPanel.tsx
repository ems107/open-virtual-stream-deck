import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api'

export function HelpPanel() {
  const { t } = useTranslation()
  const [functions, setFunctions] = useState<string[]>([])
  useEffect(() => {
    void api.get<string[]>('/api/functions').then(setFunctions)
  }, [])

  return (
    <div className="panel help">
      <h1>{t('help.title')}</h1>

      <section className="card">
        <h2>{t('help.templatesTitle')}</h2>
        <p>{t('help.templates')}</p>
        <pre>{`CPU {{round(sys.cpu)}}%
{{media.title}} — {{media.artist}}
{{obs.recording ? '● REC' : ''}}
{{[mqtt.home/salon/temperatura]}} °C`}</pre>
      </section>

      <section className="card">
        <h2>{t('help.expressionsTitle')}</h2>
        <p>{t('help.expressions')}</p>
        <pre>{`obs.scene == 'Juego' && !audio.mic.muted
sys.cpu > 80 ? 'alto' : 'ok'
default(user.contador, 0) + 1
json(user.tiempo, 'current.temp')`}</pre>
        <h3>{t('help.functions')}</h3>
        <div className="function-list">
          {functions.map((f) => (
            <code key={f}>{f}</code>
          ))}
        </div>
      </section>

      <section className="card">
        <h2>{t('help.variablesTitle')}</h2>
        <table className="variables">
          <tbody>
            {[
              ['time.*', 'hhmm, hhmmss, date, weekday, day, month, year'],
              ['sys.*', 'cpu, ram, ram.used, gpu, gpu.temp, cpu.temp, net.down, net.up'],
              ['audio.*', 'volume, muted, mic.volume, mic.muted, device'],
              ['media.*', 'title, artist, album, art, playing, app'],
              ['app.*', 'active, title'],
              ['obs.*', 'connected, scene, streaming, recording, replay, mute.<input>, visible.<scene>.<source>'],
              ['mqtt.<topic>', t('help.mqttVar')],
              ['webhook.<name>', t('help.webhookVar')],
              ['discord.*', 'connected, mute, deaf'],
              ['user.*', t('help.userVar')],
              ['toggle.<id>', t('help.toggleVar')],
              ['value', t('help.valueVar')],
            ].map(([name, description]) => (
              <tr key={name}>
                <td className="mono">{name}</td>
                <td>{description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="card">
        <h2>{t('help.gesturesTitle')}</h2>
        <p>{t('help.gestures')}</p>
      </section>
    </div>
  )
}
