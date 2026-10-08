# Open Virtual Stream Deck (OVSD)

Stream deck virtual y muy personalizable. Un servidor en Windows ejecuta las acciones y sirve una web que hace de deck en cualquier móvil, tablet o navegador de la misma red.

- **Rejilla libre** de filas × columnas con botones, deslizadores y widgets multicelda (texto, gráfica o indicador)
- **Gestos**: pulsar, mantener, doble toque, presionar y soltar (pulsar-para-hablar), arrastrar deslizadores
- **Macros** con pasos, esperas, condiciones `si… entonces`, variables y bucles
- **Textos dinámicos** con `{{plantillas}}` y expresiones (`{{round(sys.cpu)}}%`, `{{obs.recording ? '● REC' : ''}}`)
- **Estados visuales**: interruptores o estados calculados a partir de expresiones
- **Perfiles por dispositivo** y cambio automático según la aplicación activa del PC
- **Integraciones**: teclado (atajos, texto, mantener teclas), aplicaciones/URLs/comandos, multimedia de Windows con carátula, volumen (general, micrófono, por aplicación), dispositivo de audio, métricas (CPU, RAM, GPU, red), OBS Studio, HTTP, MQTT/Home Assistant, webhooks entrantes y Discord
- **Editor** web completo en el PC y edición rápida desde el propio dispositivo, con deshacer/rehacer, copiar/pegar, arrastrar, exportar/importar `.zip` y versiones anteriores
- **Emparejamiento** por QR o PIN: nadie más en tu red puede pulsar botones

## Instalación

Requisitos: Windows 10 (versión 2004 o posterior) u 11 de 64 bits. El móvil o la tablet solo necesitan un navegador; no se instala nada en ellos.

1. Descarga `OVSD-Setup-x.y.z.exe` de la página de [*Releases*](https://github.com/ems107/open-virtual-stream-deck/releases/latest) y ejecútalo. Se instala para tu usuario (`%LOCALAPPDATA%\Programs\OVSD`) y OVSD funciona siempre con tus permisos normales.
2. En las opciones de instalación hay dos permisos del sistema. Si marcas alguno, Windows pide confirmación **una sola vez** para ambos:
   - **Permitir que tus móviles y tablets se conecten** (marcado por defecto). Crea la regla del firewall de Windows, solo para redes privadas, así que Windows nunca mostrará su aviso de «¿Permitir acceso?».
   - **Temperatura de CPU** (opcional; ver [Temperatura de CPU](#temperatura-de-cpu)).
3. Al terminar se abre OVSD: aparece un icono en la bandeja del sistema (junto al reloj) y el editor en tu navegador.

Todo esto se puede activar o desactivar más tarde en el editor: **Ajustes → Este PC**.

¿Prefieres no instalar nada? Descarga `OVSD-portable-vx.y.z.zip`, descomprímelo donde quieras y ejecuta `OVSD.exe`. La primera vez el editor te mostrará el botón **Permitir conexiones**. Mientras no lo pulses, OVSD solo escucha en el propio PC, así que Windows no muestra ningún aviso.

Para desinstalar, ve a *Configuración de Windows → Aplicaciones*. Al desinstalar, OVSD pregunta si quieres borrar también tus perfiles.

## Uso

1. **Conectar el móvil o la tablet** (misma WiFi que el PC): clic derecho en el icono de la bandeja → **Conectar dispositivo (QR)** y escanea el código con la cámara. También puedes abrir la dirección que aparece en el navegador del móvil e introducir el PIN.
2. **Diseñar los paneles**: doble clic en el icono de la bandeja (o **Abrir editor**). Los cambios se ven al instante en el móvil.
3. En el móvil, el botón ⋮ de la esquina abre el menú del deck: perfiles, pantalla completa (gira la pantalla según la forma de tu rejilla y se recuerda), mantener la pantalla encendida, modo edición…
4. Para abrirlo como una app, usa **Añadir a la pantalla de inicio** desde el menú del navegador.

OVSD se queda en la bandeja mientras está en marcha; para cerrarlo, haz clic derecho en el icono → **Salir**. Desde ese mismo menú puedes activar **Iniciar con Windows**.

Los datos se guardan en `%APPDATA%\OVSD` (perfiles, imágenes, copias de seguridad, ajustes y logs). Para hacer copia de seguridad o pasarlos a otro PC, copia esa carpeta o exporta los perfiles en `.zip` desde el editor.

## Temperatura de CPU

Windows solo deja leer los sensores internos del procesador a un driver del sistema. OVSD usa [PawnIO](https://pawnio.eu), el driver firmado que usa LibreHardwareMonitor, y solo un proceso con permisos de administrador puede hablar con él.

Para no ejecutar OVSD entero como administrador, la lectura se separa:

- **Servicio `OVSD Sensors`** (Windows, cuenta del sistema). Es una copia de `OVSD.exe` en `C:\Program Files\OVSD Sensors`, una carpeta que solo un administrador puede modificar. Lo único que hace es leer temperatura, consumo y frecuencia de la CPU cada segundo y publicarlos por una tubería con nombre (*named pipe*) de solo lectura. No abre puertos de red, no ejecuta acciones y no acepta órdenes.
- **OVSD** (tu usuario, sin permisos especiales). Lee esa tubería y publica `sys.cpu.temp`, `sys.cpu.power` y `sys.cpu.clock`. Las teclas, comandos y demás acciones siguen ejecutándose con tu usuario.

Se activa en el instalador o en *Ajustes → Este PC → Temperatura de CPU*, con una única confirmación de Windows. Si PawnIO no está instalado, se descarga de su versión oficial y se comprueba su huella SHA-256 antes de instalarlo. Al desactivarlo o desinstalar OVSD se elimina el servicio. PawnIO queda instalado, porque otros programas pueden usarlo, y se puede quitar desde *Aplicaciones*.

Los sensores de GPU (`sys.gpu`, `sys.gpu.temp`) funcionan sin nada de esto.

## Estructura

| Carpeta | Contenido |
|---|---|
| `server/src/OVSD.Core` | Modelo, almacenamiento, lenguaje de expresiones, variables, motor de macros, runtime del deck (multiplataforma) |
| `server/src/OVSD.Platform.Windows` | SendInput, controles multimedia (SMTC), Core Audio, ventana activa, métricas |
| `server/src/OVSD.Integrations` | OBS (obs-websocket v5), HTTP, MQTT, Discord RPC |
| `server/src/OVSD.Host` | ASP.NET Core: API REST, WebSocket, emparejamiento, bandeja; incluye la web embebida |
| `web/` | Cliente React + TypeScript + Vite: deck, editor y emparejamiento |
| `scripts/` | Publicación (`publish.ps1`) y generación del icono (`make-icon.ps1`) |

## Desarrollo

Requisitos: .NET SDK 10 y Node 22+.

```sh
# Cliente con recarga en caliente en http://localhost:5173 (reenvía /api, /ws y /media al servidor)
cd web && npm install && npm run dev

# Servidor (puerto 7341). --no-tray: sin icono de bandeja. DryRun: registra las acciones sin ejecutarlas.
dotnet run --project server/src/OVSD.Host -- --no-tray --Ovsd:DataDir=./.devdata --Ovsd:DryRun=true
```

La web se embebe en el ejecutable al compilar el servidor: tras cambiarla, ejecuta `npm run build` en `web/` y vuelve a compilar el servidor (o usa `npm run dev`).

Si cambias los DTO del protocolo o del modelo en C#, regenera los tipos TypeScript con `npm run gen-types`.

Opciones (`appsettings.json` o `--Ovsd:Clave=valor`):

| Opción | Por defecto | |
|---|---|---|
| `Ovsd:Port` | `7341` | Puerto HTTP/WebSocket en todas las interfaces |
| `Ovsd:DataDir` | `%APPDATA%\OVSD` | Carpeta de datos |
| `Ovsd:DryRun` | `false` | No toca el sistema: las acciones solo se registran en la variable `dryrun.last` |
| `Ovsd:ServerName` | `OVSD` | Nombre que ven los dispositivos |
| `Ovsd:Network` | `auto` | `auto`: escucha en la red cuando el firewall lo permite (si no, solo en este PC, sin avisos de Windows) · `lan`: siempre · `local`: solo este PC |

## Tests

```sh
dotnet test server/OVSD.sln     # núcleo, runtime, almacenamiento, integraciones (servidor OBS falso, broker MQTT en proceso)
cd web && npm test               # gestos, deslizador, modelo del editor, teclas
cd web && npm run build && npm run e2e   # Playwright en Edge contra un servidor real en modo DryRun
```

## Publicar

```powershell
.\scripts\publish.ps1            # dist\OVSD.exe autocontenido (no requiere instalar .NET)
.\scripts\build-installer.ps1    # lo anterior + dist\OVSD-Setup-<versión>.exe (Inno Setup; si no está instalado, descarga el compilador portátil en .tools\)
```

La versión se define en `<Version>` de `server/src/OVSD.Host/OVSD.Host.csproj`. Para publicar una versión en GitHub, sube una etiqueta con la misma versión (`git tag v1.0.0 && git push --tags`). El workflow `.github/workflows/release.yml` pasa los tests, compila el instalador y la versión portable, y crea la *Release*.

## Notas

- **Seguridad**: el PC (localhost) tiene acceso total. Los demás dispositivos necesitan emparejarse y se pueden desvincular desde *Dispositivos*. Al ejecutar comandos o enviar teclas, trata los dispositivos emparejados como de confianza.
- **HTTP en la LAN**: no se usa HTTPS, así que el navegador no ofrece «instalar app». La pantalla se mantiene encendida con un vídeo invisible (NoSleep) y la pantalla completa se activa desde el menú del deck.
- **Ventanas de administrador**: Windows impide enviar teclas a programas elevados salvo que OVSD también se ejecute como administrador.
- **Red y firewall**: OVSD solo acepta conexiones de otros dispositivos en redes **privadas**. Si Windows marcó tu WiFi como pública, *Ajustes → Este PC* lo detecta y ofrece marcarla como privada. La opción `Ovsd:Network` acepta tres valores: `auto` (por defecto: escucha en la red cuando el firewall lo permite), `lan` (siempre) y `local` (solo este PC).
- **MQTT / Home Assistant**: en *Ajustes → MQTT*, indica la dirección del broker (en Home Assistant, el complemento *Mosquitto broker*, normalmente `IP-de-HA:1883`) y un usuario y contraseña de Home Assistant. Los mensajes de los temas suscritos aparecen como variables `mqtt.<tema>`, y la acción *MQTT: publicar* envía mensajes.
- **Discord**: para silenciar o ensordecer con el estado real hace falta una aplicación propia en discord.com/developers (Client ID y secret, redirección `http://localhost`). Sin ella, usa acciones de atajo de teclado con los atajos de Discord.

## Licencia

[MIT](LICENSE). Componentes de terceros: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
