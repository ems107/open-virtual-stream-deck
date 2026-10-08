# Home Assistant and MQTT

## What is MQTT?

MQTT is a messaging system widely used in home automation. There is a central server, the **broker**, which works like a notice board:

- Devices **publish** messages to **topics**, for example `home/living/temperature` → `21.5`.
- Anyone **subscribed** to that topic receives every new message.

OVSD can do both: **publish** from a button (turn on a light) and **subscribe** to show data on keys (temperature, whether the door is open…).

## Connecting to Home Assistant

Home Assistant has its own broker, the **Mosquitto broker** add-on:

1. In Home Assistant: **Settings → Add-ons → Add-on store** → install **Mosquitto broker** and start it.
2. **Settings → Devices & services**: Home Assistant discovers MQTT; press **Configure**.
3. Create (or reuse) a Home Assistant user for OVSD: **Settings → People → Users**.
4. In OVSD: **Settings → MQTT / Home Assistant**:
   - **Enabled**.
   - **Host**: Home Assistant's IP (for example `192.168.1.50`). **Port**: `1883`.
   - **Username** and **password**: those of that user.
   - **Subscriptions**: one topic per line (see below).
5. **Save**. The indicator turns **Connected**.

## Receiving data: subscriptions

Every message on a subscribed topic is stored in the `mqtt.<topic>` variable. Wildcards are allowed: `+` (one level) and `#` (everything below).

```
home/living/temperature
zigbee2mqtt/+
homeassistant/sensor/#
```

In expressions, since topics contain `/`, write them in brackets:

```
{{[mqtt.home/living/temperature]}} °C
```

If the message is JSON, extract a field with `json()`:

```
{{json([mqtt.zigbee2mqtt/living_sensor], 'temperature')}} °C
```

## Sending commands: publish

The **MQTT publish** action sends a message: topic, payload and whether it's retained. For example, with Zigbee2MQTT:

```
Topic:   zigbee2mqtt/living_lamp/set
Payload: {"state": "TOGGLE"}
```

> Rather not use MQTT? Home Assistant also has an HTTP API: see [HTTP and webhooks](#http-webhooks) to call its services with an access token.

## Full example: lamp with state

```
Subscription: zigbee2mqtt/living_lamp
Text:  Living room
Icon:  mdi:lightbulb
Tap:   MQTT publish · zigbee2mqtt/living_lamp/set · {"state": "TOGGLE"}
State: From expression · json([mqtt.zigbee2mqtt/living_lamp], 'state') == 'ON'
       (on: lightbulb-on icon, yellow color)
```
