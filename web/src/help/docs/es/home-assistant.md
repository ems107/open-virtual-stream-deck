# Home Assistant y MQTT

## ¿Qué es MQTT?

MQTT es un sistema de mensajería muy usado en domótica. Hay un servidor central, el **broker**, que funciona como un tablón de anuncios:

- Los aparatos **publican** mensajes en **temas** (*topics*), por ejemplo `casa/salon/temperatura` → `21.5`.
- Quien esté **suscrito** a ese tema recibe cada mensaje nuevo.

OVSD puede hacer ambas cosas: **publicar** desde un botón (encender una luz) y **suscribirse** para mostrar datos en las teclas (temperatura, si la puerta está abierta…).

## Conectar con Home Assistant

Home Assistant trae su propio broker, el complemento **Mosquitto broker**:

1. En Home Assistant: **Ajustes → Complementos → Tienda** → instala **Mosquitto broker** y arráncalo.
2. **Ajustes → Dispositivos y servicios**: Home Assistant detecta MQTT; pulsa **Configurar**.
3. Crea (o usa) un usuario de Home Assistant para OVSD: **Ajustes → Personas → Usuarios**.
4. En OVSD: **Ajustes → MQTT / Home Assistant**:
   - **Activado**.
   - **Servidor**: la IP de Home Assistant (por ejemplo `192.168.1.50`). **Puerto**: `1883`.
   - **Usuario** y **contraseña**: los de ese usuario.
   - **Suscripciones**: un tema por línea (ver abajo).
5. **Guardar**. El indicador pasa a **Conectado**.

## Recibir datos: suscripciones

Cada mensaje de un tema suscrito queda en la variable `mqtt.<tema>`. Admite comodines: `+` (un nivel) y `#` (todo lo que cuelga).

```
casa/salon/temperatura
zigbee2mqtt/+
homeassistant/sensor/#
```

En las expresiones, como los temas llevan `/`, se escriben entre corchetes:

```
{{[mqtt.casa/salon/temperatura]}} °C
```

Si el mensaje es JSON, extrae un campo con `json()`:

```
{{json([mqtt.zigbee2mqtt/sensor_salon], 'temperature')}} °C
```

## Enviar órdenes: publicar

La acción **Publicar en MQTT** envía un mensaje: tema, contenido y si se retiene (*retain*). Por ejemplo, con Zigbee2MQTT:

```
Topic:     zigbee2mqtt/lampara_salon/set
Contenido: {"state": "TOGGLE"}
```

> ¿Prefieres no usar MQTT? Home Assistant también tiene una API HTTP: mira [HTTP y webhooks](#http-webhooks) para llamar a sus servicios con un *token* de acceso.

## Ejemplo completo: lámpara con estado

```
Suscripción: zigbee2mqtt/lampara_salon
Texto:  Salón
Icono:  mdi:lightbulb
Pulsar: Publicar en MQTT · zigbee2mqtt/lampara_salon/set · {"state": "TOGGLE"}
Estado: Según expresión · json([mqtt.zigbee2mqtt/lampara_salon], 'state') == 'ON'
        (on: icono lightbulb-on, color amarillo)
```
