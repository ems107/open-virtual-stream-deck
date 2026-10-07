# Open Virtual Stream Deck (OVSD)

Stream deck virtual: un servidor en Windows ejecuta las acciones y sirve una web que hace de deck en móviles y tablets de la misma red.

- `server/`: .NET 10 (ASP.NET Core y app de bandeja). Proyectos `OVSD.Core`, `OVSD.Platform.Windows`, `OVSD.Integrations` y `OVSD.Host`
- `web/`: cliente React + TypeScript + Vite (deck, editor y emparejamiento)

## Desarrollo

```sh
# Servidor (puerto 7341, escucha en toda la LAN). --no-tray para no crear el icono de bandeja.
dotnet run --project server/src/OVSD.Host -- --no-tray

# Cliente con recarga en caliente (http://localhost:5173, hace de proxy de /api y /ws al servidor)
cd web && npm run dev

# Regenerar los tipos TS del protocolo tras cambiar los DTO de OVSD.Core/Protocol
cd web && npm run gen-types

# Compilar el cliente dentro de server/src/OVSD.Host/wwwroot, que es lo que sirve el servidor
cd web && npm run build
```

Rutas: `/` (deck), `/editor` y `/pair` (QR para conectar un dispositivo).
