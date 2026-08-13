# Steamworks — Setup y validación (Feature#0019)

Integración de Steamworks.NET (SDK 1.63) con init automático al arrancar y
logros data-driven. App ID del equipo: **4889850**.

## Qué hay en código

| Pieza | Ruta | Rol |
|---|---|---|
| Package UPM | `Packages/manifest.json` → `com.rlabrecque.steamworks.net#2025.163.0` | Wrapper C# + `steam_api64.dll` |
| `ISteamService` | `Assets/Scripts/Rollgeon/Achievements/ISteamService.cs` | Fachada sin tipos de Steamworks |
| `SteamConfigSO` | `Assets/Scripts/Rollgeon/Achievements/SteamConfigSO.cs` | AppId + mapeo key→API name→trigger |
| `AchievementService` | `Assets/Scripts/Rollgeon/Achievements/AchievementService.cs` | Eventos del juego → unlocks |
| `Rollgeon.Steam` | `Assets/Scripts/Rollgeon/Steam/` | Init/Shutdown, pump de callbacks, impl real |
| Comando consola | `steam status` / `steam ach list\|unlock\|clear <key>` | Solo editor/dev-build (`` ` `` o F1) |
| Editor setup | menú **Rollgeon → Steam → Setup Steam Integration** | Crea y cablea los assets (idempotente) |

## Setup en un editor nuevo (checklist)

1. Pull de la rama → Unity resuelve el package solo (necesita red la primera vez).
2. Verificar que exista `steam_appid.txt` en la **raíz del proyecto** con `4889850`
   (está commiteado; el package lo regenera con 480 si falta — corregirlo).
3. Correr **Rollgeon → Steam → Setup Steam Integration** si `SteamConfig.asset`
   no está cableado (en la rama ya viene cableado — el paso es no-op).
4. Cliente de Steam **abierto y logueado** con una cuenta que tenga la app
   4889850 en su biblioteca.

## Logros en el partner site (pendiente — no hay logros publicados aún)

1. [partner.steamgames.com](https://partner.steamgames.com) → App 4889850 →
   **Stats & Achievements → Achievements → New Achievement**.
2. Definir **API Name** (ej. `ACH_WIN_FIRST_RUN`), nombre visible, descripción
   e íconos (locked/unlocked).
3. **Save → Publish** (Publish to Steam). Sin publicar, `SetAchievement`
   devuelve `false` — es el error más común.
4. Mapear en `Assets/Rollgeon/SteamConfig.asset`: agregar entry con `Key`
   interna, el `SteamApiName` exacto y el `Trigger` (RunVictory, RunDefeat,
   BossDefeated, FloorCleared+IntParam, ComboCounterReached+StringParam/IntParam,
   o Manual).

## Smoke test (editor)

1. Steam corriendo → **Play** desde `00_Bootstrap` → consola:
   `[Steam] Init OK — AppId 4889850, usuario '<nombre>'.`
2. `` ` `` → `steam status` → `Steam OK — usuario '...', AppId 4889850.`
3. Toast de prueba **mientras 4889850 no tenga logros publicados**: cambiar
   `steam_appid.txt` y el `AppId` del `SteamConfig.asset` a **480** (Spacewar,
   funciona con cualquier cuenta), re-entrar a Play → `steam ach unlock test`
   → notificación del cliente Steam (abajo a la derecha; en editor NO hay
   overlay in-game, eso requiere build lanzada desde Steam) → `steam ach clear
   test` → **revertir a 4889850**.
4. Unlock por evento: ganar una run (o simular la victoria) → `first_win`
   salta solo vía `OnRunVictory`.
5. Negativo: cerrar Steam → Play → un solo warning `[Steam] SteamAPI.Init()
   falló…` y el juego sigue normal; `steam status` → `Steam no disponible`.

## Builds

Desde Feature#0036 el pipeline está automatizado:

- **Generar el player** → [`windows-build.md`](./windows-build.md).
  Menú **Rollgeon → Build → Windows 64**. El post-build copia `steam_appid.txt`
  al lado del `.exe` solo (hace falta para correrlo fuera del cliente de Steam).
- **Subir a Steam** → [`../../SteamPipe/README.md`](../../SteamPipe/README.md).
  El depot **excluye** `steam_appid.txt` por vdf: incluirlo rompe el
  relaunch-vía-Steam y delata builds de dev. `RestartAppIfNecessary` solo corre
  en builds (`#if !UNITY_EDITOR`).

**No hace falta aprobación de Valve** para subir y testear builds. El review solo
se dispara al pedir el release ("Mark as ready for review").

## Troubleshooting

| Síntoma | Causa probable |
|---|---|
| `SetAchievement` devuelve false | Logro sin **Publish** en el partner site, o API name con typo |
| `Init()` false con Steam abierto | Cuenta sin la app 4889850 en biblioteca, o `steam_appid.txt` ausente/incorrecto |
| `DllNotFoundException` | Package a medio importar — reimport / revisar `Packages/packages-lock.json` |
| Perfil de Steam muestra "jugando Spacewar" | Quedó el AppId 480 del paso 3 del smoke test — revertir |
| Logro no re-desbloquea al probar | De-dupe de sesión — usar `steam ach clear <key>` y reintentar |
