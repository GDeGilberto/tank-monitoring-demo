# Monitoreo de tanques (demo)

Sistema web que monitorea en tiempo real tanques con líquidos. Lee el hardware de medición por puerto serial, guarda inventarios y descargas, y los muestra en un dashboard. El lector es una abstracción (`ITankReader`) y el protocolo es propio ([docs/protocolo.md](docs/protocolo.md)), así que sirve con cualquier sensor.

> **Los datos de este demo son simulados.** Un simulador genera consumo, descargas, temperatura y agua en 4 tanques (tipo A, tipo B y dos tipo C). El demo es anónimo y de solo lectura.

## Arquitectura

- **Domain / Application / Infrastructure**: Clean Architecture. `Application/Protocol` (checksum, tramas), parsers y casos de uso.
- **API** (ASP.NET Core, .NET 9): endpoints `Sitios`, `Inventarios`, `Descargas`, `Auth` (JWT), trabajos de Hangfire y el simulador.
- **Web** (Blazor Server): dashboard e historial de recargas (exportable a Excel).
- **PostgreSQL**: base de datos (EF Core con Npgsql; Hangfire.PostgreSql).

Servicios del compose: `web` (único puerto expuesto), `api` (incluye el simulador y los jobs) y `db`.

## Ejecutar con Docker (recomendado)

Requisitos: Docker con Compose v2.

```bash
cp .env.example .env      # completa los valores (ver abajo)
docker compose up -d --build
```

Abre `http://127.0.0.1:8080` (o el `WEB_PORT` que definas). En 1 a 2 minutos el dashboard muestra datos que cambian y aparecen nuevas recargas.

### Variables de entorno (`.env`)

| Variable | Obligatoria | Descripción |
|---|---|---|
| `POSTGRES_PASSWORD` | sí | Clave de PostgreSQL. Genera una aleatoria (`openssl rand -hex 24`) |
| `JWT_KEY` | sí | Clave de firma JWT de la API, 32 caracteres o más (`openssl rand -base64 48`) |
| `POSTGRES_DB`, `POSTGRES_USER` | no | Por defecto `monitoring` |
| `WEB_PORT` | no | Puerto del host para la web (8080 por defecto) |
| `WEB_BIND` | no | Dirección del host donde escucha (`127.0.0.1` por defecto) |
| `TZ` | no | Zona horaria de los contenedores (`UTC` por defecto) |

El archivo `.env` no se versiona. Sin `POSTGRES_PASSWORD` o `JWT_KEY` el compose se niega a arrancar, y la API termina con un mensaje que nombra la variable que falta (nunca imprime valores).

### Comprobar que funciona

```bash
docker compose ps                       # los 3 servicios en "healthy"
curl http://127.0.0.1:8080/health       # {"status":"ok"}
```

La API y la base de datos no publican puertos (solo existen en la red interna del compose).

## Despliegue en un homelab con túnel (Raspberry Pi)

Pensado para una Raspberry Pi de 4 núcleos y 8 GB (`linux/arm64`). Consumo medido en `amd64`: unos 180 MB en total entre los tres contenedores (límites: db 256 MB, api 256 MB, web 384 MB).

1. Construye las imágenes multi-arquitectura en un equipo con buildx (no en la Pi) y súbelas a tu registro:
   ```bash
   docker buildx build --platform linux/amd64,linux/arm64 --target web -t <registro>/tank-monitoring-web:demo --push .
   docker buildx build --platform linux/amd64,linux/arm64 --target api -t <registro>/tank-monitoring-api:demo --push .
   ```
   Alternativa: `docker compose up -d --build` directamente en la Pi (más lento).
2. Copia `docker-compose.yml` y `.env` (con valores nuevos) a la Pi. Si usas imágenes de un registro, cambia `build:` por `image:` en el compose.
3. `docker compose up -d`.
4. El túnel de Cloudflare (`cloudflared` en la misma Pi) apunta a `http://localhost:8080`. La web respeta `X-Forwarded-Proto` y `X-Forwarded-For`. Si el túnel corre como contenedor en otra red, conéctalo a la red del compose o usa `WEB_BIND=0.0.0.0` solo en una red de confianza.
5. Respaldos: el volumen `dbdata` contiene todo; los datos son simulados y se pueden recrear con `docker compose down -v`.

## Seguridad del demo

- `Demo:Enabled=true`: cualquier método distinto de GET/HEAD/OPTIONS responde 403; las consultas son anónimas; las rutas `/Account` de la web devuelven 404 (sin registro ni inicio de sesión).
- Limitador de peticiones por IP en la API (`RateLimit:PermitPerMinute`, 120 por defecto).
- Swagger apagado (`Swagger:Enabled=false`) y panel de Hangfire apagado (`Jobs:Dashboard=false`; si se activa solo responde a localhost).
- Contenedores sin privilegios y con límites de memoria.

## Desarrollo local

```bash
dotnet build
dotnet test                      # sin SQL Server ni puerto serial
```

Configuración: copia `appsettings.Example.json` a `appsettings.json` (ignorado por git) o usa User Secrets / variables de entorno. Fuera del modo Demo la API exige `Jwt__Key`, `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangfireConnection` y `ApiKeys__JobKey`.

Para correr sin Docker con PostgreSQL local y el simulador:

```bash
export ASPNETCORE_ENVIRONMENT=Demo
export ConnectionStrings__DefaultConnection="Host=localhost;Database=monitoring;Username=app;Password=<tu-clave>"
export ConnectionStrings__HangfireConnection="$ConnectionStrings__DefaultConnection"
export Jwt__Key="<32+ caracteres>"
dotnet run --project API       # y, en otra terminal: dotnet run --project Web
```

### Simulador

`Reader:Mode=Simulated` usa `SimulatedTankReader` en lugar del puerto serial (`Reader:Mode=Serial`, por defecto). Los parámetros están en `API/appsettings.Demo.json` (`Simulation:*`): semilla, escala de consumo, descargas y los 4 tanques. Con la misma semilla y el mismo reloj los datos son idénticos. Al reiniciar la API el simulador vuelve al estado inicial.

### Cobertura

`scripts/check-coverage.sh` ejecuta las pruebas con cobertura y falla si el protocolo (parsers, checksum, builder) baja de 90 % o los jobs y repositorios de 80 %.

## Cambio de comportamiento: descargas

El repositorio de descargas deduplica por sitio, tanque y fecha-hora de inicio (minuto), no por día completo. Así se guardan varias descargas el mismo día y se guardan todas las entregas que informa el lector.

## Aviso

Es un demo con datos simulados, pensado para mostrar la arquitectura y el protocolo. No es un producto terminado ni debe conectarse a equipos reales sin una revisión de seguridad propia. No requiere Internet en el navegador: Chart.js, Bootstrap y Bootstrap Icons viajan como copias locales en `Web/wwwroot/lib/` (todas MIT, con su aviso de licencia).

## Procedencia

El protocolo es propio y se explica con palabras propias en [docs/protocolo.md](docs/protocolo.md). El repositorio no incluye ni cita ningún manual de fabricante, ni marcas ni datos de terceros. El isotipo es un SVG propio.

## Licencia

MIT, ver [LICENSE](LICENSE). Las dependencias incluidas en `Web/wwwroot/lib/` conservan sus propias licencias MIT.
