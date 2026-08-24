# Build de WebGL — cómo generar la versión web

Primera build: 0.3.4 (2026-08-24). No hay entry point de menú todavía — se
buildea con `BuildPipeline.BuildPlayer` apuntando a `Build/WebGL` (carpeta,
no `.exe`), target `WebGL`, options `None`.

## Qué necesita que ya está configurado

- **`DISABLESTEAMWORKS` en los defines de WebGL** (PlayerSettings). Steamworks.NET
  se auto-apaga en WebGL con un define *por archivo*, así que sin el define
  proyecto-wide nuestro código (`using Steamworks;` en `SteamServiceBootstrap`)
  no compila. Con el define, todo el stack de Steam degrada al warning
  "Compilado con DISABLESTEAMWORKS".
- **Compresión: Disabled** (PlayerSettings → WebGL). La 0.3.4 salió primero con
  Brotli + decompression fallback y el load se colgaba al arrancar en itch, así
  que se apagó la compresión: archivos `.data`/`.wasm` planos, cero etapas de
  descompresión. El zip pesa casi lo mismo (el deflate del zip compensa) y el
  hosting no necesita ningún header especial. El decompression fallback quedó
  ON pero es inerte con la compresión apagada.
- **AOT de Odin**: WebGL es IL2CPP, y sin el dll de soporte
  (`Assets/Plugins/Sirenix/Odin AOT Support/Rollgeon.OdinAOTSupport.dll`)
  `ServiceBootstrap.asset` deserializa `ExtraServices` **vacío y sin error** —
  el juego arranca sin servicios. El dll está commiteado. Si cambian los tipos
  serializados por Odin, regenerarlo:
  `Sirenix.Serialization.Editor.AOTSupportUtilities.ScanProjectForSerializedTypes`
  + `GenerateDLL` (la 0.3.4 escaneó 300 tipos).

- **Quality: "Mobile" excluye WebGL** (QualitySettings). El player elige el
  *primer* quality level no excluido para la plataforma — el mapa
  `m_PerPlatformDefaultQuality` del YAML es legacy y no se consulta. Sin la
  exclusión, la web arrancaba en Mobile: renderScale 0.8 (mundo borroso, UI
  nítida) y un renderer sin `LightDataRendererFeature`, así que el cel-shader
  leía `_RollgeonLightData` en cero y las luces no se veían. Con la exclusión,
  la web usa el level "PC", igual que Standalone.
- **PC_Renderer está en Forward+**, que WebGL2 no soporta (no hay SSBOs) — URP
  cae solo a Forward con el límite de 4 luces por objeto del asset. Si algún
  día las luces se ven distintas en web que en desktop, mirar acá primero.

## Proceso

1. Cambiar el target a WebGL (Build Profiles). El primer switch reimporta todo
   (~20 min); los siguientes usan la caché de Library.
2. Ojo: el switch regenera `TemplateMenuItems.cs` de Amplify y recompila —
   esperar a que termine antes de buildear o `BuildPlayer` corta con
   "scripts are compiling".
3. Buildear a `Build/WebGL`. La 0.3.4 tardó 23,5 min (IL2CPP + Brotli) y pesó
   98 MB (90 MB de `.data`).
4. Addressables se construye solo (BuildWithPlayer) — verificar que
   `Build/WebGL/StreamingAssets/aa/WebGL/` tenga las bundles de localización
   english/spanish, igual que en Windows.
5. Zipear el contenido de `Build/WebGL/` y volver el target a Win64.

## Verificar

Servir la carpeta con cualquier estático (`python -m http.server`) y abrir
`index.html`. `file://` NO funciona — WebGL necesita HTTP. Revisar en la
consola del browser que no haya errores de deserialización de Odin y que el
menú principal salga localizado (misma trampa que en Windows: los fallbacks
de Opciones disimulan tablas ausentes).

## Deuda conocida

- Sin entry point en el menú Rollgeon → Build (la 0.3.4 salió por script).
- El save vive en IndexedDB del browser: se pierde al limpiar site data, y no
  se comparte con la versión de escritorio.
- Sin playtest en browser todavía — el primer smoke test manual está pendiente.
