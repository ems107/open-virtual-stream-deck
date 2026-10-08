# Copias de seguridad

## Dónde están tus datos

Todo se guarda en `%APPDATA%\OVSD` (clic derecho en el icono de la bandeja → **Abrir carpeta de datos**):

| Carpeta / archivo | Contenido |
|---|---|
| `profiles` | Tus perfiles, uno por archivo |
| `media` | Las imágenes que has subido |
| `backups` | Versiones anteriores de cada perfil |
| `config.json` | Ajustes, dispositivos emparejados e integraciones |
| `variables.json` | Tus variables `user.*` |
| `logs` | Registro de actividad y errores |

Para una copia completa o para pasar OVSD a otro PC, **copia esa carpeta entera** (con OVSD cerrado) y pégala en el mismo sitio del otro PC.

## Versiones anteriores

Cada vez que se guarda un perfil, OVSD conserva la versión anterior (hasta 50). Si te equivocas o borras algo:

**Perfiles → menú ⋯ → Versiones anteriores → Restaurar**.

También se guarda una copia al **eliminar** un perfil: en ese mismo diálogo, la sección **Perfiles eliminados** te deja recuperarlo.

## Exportar e importar perfiles

**Perfiles → menú ⋯ → Exportar (.zip)** crea un archivo con el perfil y todas sus imágenes. Sirve para:

- Guardar una copia de un perfil concreto.
- Compartirlo con otra persona.
- Pasarlo a otro PC con OVSD.

**Importar (.zip)** lo añade como un perfil nuevo; no sobrescribe ninguno de los tuyos.

> Los perfiles exportados no incluyen tus ajustes ni contraseñas (OBS, MQTT, Discord), solo el panel.
