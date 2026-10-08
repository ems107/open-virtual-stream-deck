# Problemas frecuentes

## El móvil no conecta o no carga la página

1. **Misma red**: el móvil tiene que estar en la misma WiFi que el PC. No funciona con datos móviles ni en la red de invitados si aísla a los dispositivos.
2. **Firewall**: en el editor, **Ajustes → Este PC → Conexiones desde tu red** tiene que estar en verde. Si no, pulsa **Permitir conexiones** y acepta el aviso de Windows.
3. **Red pública**: si Windows marcó tu WiFi como *pública*, bloquea las conexiones entrantes. *Ajustes → Este PC* lo detecta y ofrece **Marcar como red privada** (hazlo solo en redes de confianza, como la de casa).
4. **Dirección correcta**: el PC puede tener varias direcciones (VPN, máquinas virtuales…). En *Conectar dispositivo* elige en el desplegable la que esté en la misma red que el móvil (normalmente `192.168.x.x`).
5. ¿OVSD está abierto? Busca su icono en la bandeja.

## Dice «Dispositivo no emparejado»

El dispositivo se desvinculó o borraste los datos del navegador. Vuelve a emparejarlo: *Conectar dispositivo* en el PC e introduce el PIN.

## La pantalla del móvil se apaga

Menú **⋮ → Pantalla siempre encendida** tiene que estar activado. Al abrir el deck hay que **tocar la pantalla una vez** para que el navegador lo permita. En algunos móviles, el ahorro de batería extremo lo impide: desactívalo para el navegador.

## Las teclas no llegan a un programa o juego

- **Programas como administrador**: Windows no deja que un programa normal envíe teclas a uno que se ejecuta como administrador. Ejecuta ese programa sin permisos de administrador.
- **Juegos**: algunos juegos ignoran las pulsaciones simuladas en ciertas teclas. Prueba con atajos `F13`–`F24` asignados dentro del juego, o con combinaciones con `Ctrl`/`Alt`.
- **Pulsación demasiado corta**: si un juego no detecta la tecla, usa *Mantener teclas pulsadas*, *Esperar* 50 ms y *Soltar teclas*.

## Un botón tarda en responder

Si la tecla tiene acciones de **Mantener pulsado** o **Doble toque**, el toque normal espera para distinguirlos. Quita esos gestos o reduce los tiempos en *Ajustes → Deck*.

## No aparece la carátula o el título de la canción

Solo funciona con reproductores que se integran con los controles multimedia de Windows (los que aparecen en el panel de volumen de Windows). Spotify, navegadores, Apple Music y la mayoría lo hacen.

## OBS, MQTT o Discord en «Error»

Pasa el ratón o mira el mensaje junto al indicador en *Ajustes*. Lo habitual: contraseña incorrecta, el programa no está abierto, o la dirección/puerto no son correctos. OVSD reintenta la conexión solo.

## La temperatura de la CPU sale vacía

Actívala en *Ajustes → Este PC → Temperatura de CPU*. Si dice que falta el driver o el servicio está detenido, desactívala y vuelve a activarla.

## Otro programa usa el puerto 7341

OVSD avisa al arrancar. Cambia `Port` en el archivo `appsettings.json` que está junto a `OVSD.exe` y vuelve a emparejar los dispositivos con la nueva dirección.

## Dónde mirar si algo falla

Clic derecho en el icono de la bandeja → **Abrir carpeta de datos** → carpeta `logs`. El archivo `ovsd.log` registra errores de acciones e integraciones.
