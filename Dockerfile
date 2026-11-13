# syntax=docker/dockerfile:1
# Imagen multi-arquitectura (linux/amd64 y linux/arm64) del demo de monitoreo de tanques.
# Dos destinos: "api" (API + trabajos + simulador) y "web" (dashboard Blazor).
#   docker buildx build --platform linux/amd64,linux/arm64 --target web -t tank-monitoring-web:demo .
#   docker buildx build --platform linux/amd64,linux/arm64 --target api -t tank-monitoring-api:demo .

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restauracion primero (capa cacheable): solo los .csproj de lo que se publica.
COPY Domain/Domain.csproj Domain/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY API/API.csproj API/
COPY Web/Web.csproj Web/
RUN case "$TARGETARCH" in \
      amd64) RID=linux-musl-x64 ;; \
      arm64) RID=linux-musl-arm64 ;; \
      *) echo "Arquitectura no soportada: $TARGETARCH" >&2; exit 1 ;; \
    esac \
 && dotnet restore API/API.csproj -r "$RID" \
 && dotnet restore Web/Web.csproj -r "$RID"

COPY Domain/ Domain/
COPY Application/ Application/
COPY Infrastructure/ Infrastructure/
COPY API/ API/
COPY Web/ Web/
RUN case "$TARGETARCH" in \
      amd64) RID=linux-musl-x64 ;; \
      arm64) RID=linux-musl-arm64 ;; \
    esac \
 && dotnet publish API/API.csproj -c Release -r "$RID" --self-contained false --no-restore -o /out/api /p:UseAppHost=false \
 && dotnet publish Web/Web.csproj -c Release -r "$RID" --self-contained false --no-restore -o /out/web /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime-base
# ICU y zona horaria para fechas y numeros; los contenedores corren sin privilegios.
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    DOTNET_TieredPGO=0
WORKDIR /app
USER $APP_UID
EXPOSE 8080

FROM runtime-base AS api
COPY --from=build /out/api .
ENTRYPOINT ["dotnet", "API.dll"]

FROM runtime-base AS web
COPY --from=build /out/web .
ENTRYPOINT ["dotnet", "Web.dll"]
