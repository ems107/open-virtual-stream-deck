# HTTP and webhooks

With HTTP, OVSD can talk to almost any service: Home Assistant, Philips Hue, 3D printers, your own server, IFTTT, n8n…

## Making requests: the "HTTP request" action

| Option | Example |
|---|---|
| **Method** | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| **URL** | `http://192.168.1.50:8123/api/services/light/toggle` |
| **Headers** | One per line: `Authorization: Bearer abc123` |
| **Body** and **content type** | `{"entity_id": "light.living"}` · `application/json` |
| **Store response in variable** | `user.response` |

Every field accepts [variables](#variables): `{"brightness": {{value}}}` on a slider sends its value.

If you store the result in a variable, the response body goes in it and the HTTP status code in `<variable>.status` (for example `user.response.status` = `200`). If the response is JSON, extract what you need:

```
{{json(user.weather, 'current.temperature_2m')}} °C
```

### Example: Home Assistant over HTTP

1. In Home Assistant: your profile (bottom left) → **Security → Long-lived access tokens → Create token**.
2. A button in OVSD with *HTTP request*:

```
Method:  POST
URL:     http://192.168.1.50:8123/api/services/light/toggle
Headers: Authorization: Bearer THE_TOKEN
Body:    {"entity_id": "light.living"}
Type:    application/json
```

### Example: current weather on every tap

```
GET https://api.open-meteo.com/v1/forecast?latitude=51.5&longitude=-0.1&current=temperature_2m
Store in: user.weather
Key text: {{json(user.weather, 'current.temperature_2m')}} °C
```

## Receiving data: incoming webhooks

Other apps can **send data to OVSD** by calling an address. In **Settings → Incoming webhooks** you'll see yours, with a secret key:

```
http://PC-IP:7341/api/hook/NAME?key=KEY&value=VALUE
```

- Replace `NAME` with anything (`door`, `tasks`, `alert`…).
- The value can go in `value=` (GET) or as the body of a POST.
- OVSD stores it in `webhook.NAME` and the time in `webhook.NAME.time`.

For example, a Home Assistant automation can tell OVSD when the door opens, and a key with the text `Door: {{webhook.door}}` shows it instantly.

> Treat the key like a password. If you share it by mistake, press **Regenerate key**.
