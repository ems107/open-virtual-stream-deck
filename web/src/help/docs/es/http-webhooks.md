# HTTP y webhooks

Con HTTP, OVSD puede hablar con casi cualquier servicio: Home Assistant, Philips Hue, impresoras 3D, tu propio servidor, IFTTT, n8n…

## Hacer peticiones: la acción «Petición HTTP»

| Opción | Ejemplo |
|---|---|
| **Método** | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| **URL** | `http://192.168.1.50:8123/api/services/light/toggle` |
| **Cabeceras** | Una por línea: `Authorization: Bearer abc123` |
| **Cuerpo** y **tipo de contenido** | `{"entity_id": "light.salon"}` · `application/json` |
| **Guardar resultado en la variable** | `user.respuesta` |

Todos los campos admiten [variables](#variables): `{"brillo": {{value}}}` en un deslizador envía su valor.

Si guardas el resultado en una variable, la respuesta queda en ella y el código HTTP en `<variable>.status` (por ejemplo `user.respuesta.status` = `200`). Si la respuesta es JSON, extrae lo que te interese:

```
{{json(user.tiempo, 'current.temperature_2m')}} °C
```

### Ejemplo: Home Assistant por HTTP

1. En Home Assistant: tu perfil (abajo a la izquierda) → **Seguridad → Tokens de acceso de larga duración → Crear token**.
2. Botón en OVSD con *Petición HTTP*:

```
Método:   POST
URL:      http://192.168.1.50:8123/api/services/light/toggle
Cabeceras: Authorization: Bearer EL_TOKEN
Cuerpo:   {"entity_id": "light.salon"}
Tipo:     application/json
```

### Ejemplo: tiempo actual cada vez que pulsas

```
GET https://api.open-meteo.com/v1/forecast?latitude=40.4&longitude=-3.7&current=temperature_2m
Guardar en: user.tiempo
Texto de la tecla: {{json(user.tiempo, 'current.temperature_2m')}} °C
```

## Recibir datos: webhooks entrantes

Otras aplicaciones pueden **enviar datos a OVSD** llamando a una dirección. En **Ajustes → Webhooks entrantes** verás tu dirección, con una clave secreta:

```
http://IP-DEL-PC:7341/api/hook/NOMBRE?key=CLAVE&value=VALOR
```

- Cambia `NOMBRE` por lo que quieras (`puerta`, `tareas`, `alerta`…).
- El valor puede ir en `value=` (GET) o como cuerpo de un POST.
- OVSD lo guarda en `webhook.NOMBRE` y la hora en `webhook.NOMBRE.time`.

Por ejemplo, una automatización de Home Assistant puede avisar a OVSD cuando se abre la puerta, y una tecla con el texto `Puerta: {{webhook.puerta}}` lo mostrará al instante.

> Trata la clave como una contraseña. Si la compartes por error, pulsa **Regenerar clave**.
