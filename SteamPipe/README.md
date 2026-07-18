# SteamPipe — subir builds a Steam (App 4889850)

Scripts de subida para Rollgeon. La build del player se genera aparte
(ver [`docs/setup/windows-build.md`](../docs/setup/windows-build.md)); acá empieza
todo cuando ya existe `Build/Windows64/Rollgeon.exe`.

## ¿Hace falta aprobación de Valve?

**No.** El review de Valve (3-5 días hábiles) se dispara solo al apretar
**Mark as ready for review** para pedir el release. Mientras la app esté sin lanzar,
subir builds y probarlas es libre y sin límite.

Lo que sí hay que tener configurado del lado del partner site está en el checklist
de acá abajo. **El depot por sí solo no hace la app lanzable.**

## Antes de la primera corrida

- [ ] Bajar el [Steamworks SDK](https://partner.steamgames.com/downloads/steamworks_sdk.zip)
      y ubicar `sdk/tools/ContentBuilder/builder/steamcmd.exe`.
- [x] Depot ID: **4889851** (leído de partner site → App 4889850 → SteamPipe → Depots,
      2026-07-18). Ya está cableado en los dos vdf.
- [ ] El depósito 4889851 tiene que estar en el **Dev Comp Package** (Asociaciones de
      paquetes → Dev Comp). Si no figura ahí, subís archivos y la app no aparece
      instalable en tu biblioteca, sin ningún error que lo explique.
- [ ] **Launch Options** configuradas y publicadas: Instalación → Instalación general,
      ejecutable `Rollgeon.exe`, OS Windows. Sin esto Steam instala el juego y no sabe
      qué correr. Ojo: hay que apretar **Publicar cambios**, es un paso aparte.
- [ ] Login interactivo una sola vez para satisfacer Steam Guard y cachear el sentry:
      `steamcmd +login <cuenta>`

Las credenciales nunca van al repo.

## Correr

```
steamcmd +login <cuenta> +run_app_build "C:\ruta\al\repo\SteamPipe\app_4889850.vdf" +quit
```

`run_app_build` necesita **ruta absoluta** en las versiones actuales de steamcmd.

### Preview primero

`app_4889850.vdf` está commiteado con `"preview" "1"`, así que una corrida accidental
**no puede subir nada**. El preview genera los manifests en `Build/SteamPipeOutput/`
sin tocar los servidores de Steam.

Verificar en el manifest generado que **`steam_appid.txt` NO aparece** en la lista de
archivos. Ese archivo hace falta al lado del `.exe` para probar la build fuera de Steam,
pero en el depot rompe el relaunch-vía-Steam.

Recién cuando el preview salga limpio, pasar `"preview"` a `"0"` y volver a correr.
Conviene dejarlo de vuelta en `"1"` al commitear.

### Poner la build live

`"setlive"` está vacío a propósito: la build se sube pero no queda en ninguna branch.
La promoción se hace a mano desde partner site → App 4889850 → **Builds**, eligiendo
la branch destino.

Para testear, usar una **branch beta con password**, no `default`. La branch `default`
ni siquiera se puede poner live automáticamente — es una restricción de Steam.

## Por build

Bumpear `"desc"` en `app_4889850.vdf`: es el texto que identifica la build en la lista
del partner site. Con todas diciendo lo mismo no se distinguen.
