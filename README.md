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

## Uso

1. Ejecuta `OVSD.exe` (ver *Publicar*). Aparece un icono en la bandeja del sistema.
2. La primera vez, Windows pregunta por el firewall: **permite el acceso en redes privadas**.
3. Menú de la bandeja → **Conectar dispositivo** y escanea el QR con el móvil o la tablet (misma WiFi).
4. Menú de la bandeja → **Abrir editor** (o doble clic en el icono) para diseñar los paneles.

En el móvil, el botón ⋮ de la esquina abre el menú del deck: perfiles, pantalla completa, mantener la pantalla encendida, modo edición… Para una experiencia tipo app, usa **Añadir a la pantalla de inicio** desde el navegador.

Los datos se guardan en `%APPDATA%\OVSD` (perfiles, imágenes, copias de seguridad, ajustes y logs).

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

## Tests

```sh
dotnet test server/OVSD.sln     # núcleo, runtime, almacenamiento, integraciones (servidor OBS falso, broker MQTT en proceso)
cd web && npm test               # gestos, deslizador, modelo del editor, teclas
cd web && npm run build && npm run e2e   # Playwright en Edge contra un servidor real en modo DryRun
```

## Publicar

```powershell
.\scripts\publish.ps1            # genera dist\OVSD.exe autocontenido (no requiere instalar .NET)
```

Copia `OVSD.exe` (y opcionalmente `appsettings.json`) donde quieras. Puedes activar el arranque con Windows desde el menú de la bandeja o en Ajustes.

## Notas

- **Seguridad**: el PC (localhost) tiene acceso total. Los demás dispositivos necesitan emparejarse y se pueden desvincular desde *Dispositivos*. Al ejecutar comandos o enviar teclas, trata los dispositivos emparejados como de confianza.
- **HTTP en la LAN**: no se usa HTTPS, así que el navegador no ofrece «instalar app». La pantalla se mantiene encendida con un vídeo invisible (NoSleep) y la pantalla completa se activa desde el menú del deck.
- **Ventanas de administrador**: Windows impide enviar teclas a programas elevados salvo que OVSD también se ejecute como administrador.
- **Temperatura de CPU**: requiere ejecutar como administrador y el driver PawnIO. Los sensores de GPU funcionan sin permisos especiales.
- **Discord**: para silenciar o ensordecer con el estado real hace falta una aplicación propia en discord.com/developers (Client ID y secret, redirección `http://localhost`). Sin ella, usa acciones de atajo de teclado con los atajos de Discord.
