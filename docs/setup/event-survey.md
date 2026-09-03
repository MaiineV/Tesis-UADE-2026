# Cuestionario in-game para builds de evento (Feature#0074)

Al limpiar el piso configurado (reclamar la recompensa del boss) se abre un
cuestionario **adentro del juego**: el jugador lo llena con mouse y teclado, puede
dejar su email para un sorteo de keys, y la respuesta cae como fila en un Google
Sheet, **una pestaña por evento**. Offline-first: cada respuesta se guarda en disco
antes de intentar el envío, y lo pendiente se reintenta solo en el próximo arranque.

## Qué hay en código

| Pieza | Ruta | Rol |
|---|---|---|
| Config | `Assets/Rollgeon/SurveyConfig.asset` (`SurveyConfigSO`) | Evento, piso, endpoint, preguntas ES/EN. En `SettingsAssets` del bootstrap |
| Servicio | `Assets/Scripts/Rollgeon/Survey/SurveyService.cs` | Gating por piso (una vez por run, nunca en tutorial), `Submit` disco→red, `FlushPending` |
| Bootstrap | `SurveyServiceBootstrap` (Priority 20) | Entry en `ServiceBootstrap.asset → ExtraServices`; registra siempre, avisa si falta config |
| Store | `FileSurveyStore` | `persistentDataPath/survey/pending/*.json` → `sent/` al confirmar. Write tmp+rename |
| Transporte | `UnityWebRequestSurveyTransport` + `AppsScriptSurveySink` | POST `text/plain`, sigue el 302 de Apps Script, acepta solo `{"ok":true}` |
| Overlay | `Assets/Scripts/Rollgeon/UI/Screens/SurveyOverlay.cs` + `SurveyQuestionRow.cs` | Escucha `OnFloorCleared`, overlay no destructivo + `PhaseOverlay.Pause`, filas por prefab |
| Prefabs | `Assets/Prefabs/UI/Canvas/Canvas_Survey.prefab`, `Assets/Prefabs/UI/Survey/SurveyRow_{Rating,Choice,Text}.prefab` | Instancia bajo el `ScreenHost` de `02_Gameplay` (ya cableada) |
| Hotkey | `PauseHotkeyRule` | Escape se ignora mientras la encuesta está abierta (un solo slot de overlay de fase) |
| Build | `Rollgeon → Build → Windows 64 (Evento)` | Sin Steam + define `ROLLGEON_EVENT_BUILD` → la encuesta sale aunque `Enabled` esté apagado |
| Editor | `Rollgeon → Survey → Setup Survey / Validate Config / Open Responses Folder` | Crea+cablea la config, upsertea keys `survey.*` (tabla `UI`), valida, abre la carpeta de JSON |
| Consola | `survey status\|show\|pending\|flush\|test\|reset` | Solo editor / dev build |
| Backend | `tools/survey/apps-script.gs` | `doPost` → pestaña `event_id`, columnas `q_<id>` dinámicas, dedupe por `response_id` |
| Tests | `Assets/Scripts/Rollgeon/Survey/Tests/`, `UI/Tests/Survey*`, `DevConsole/Tests/SurveyCommandTests` | EditMode |

## Setup de la planilla + Apps Script (una vez por planilla)

1. Crear un Google Sheet (ej. *Rollgeon — Encuestas de evento*). Las pestañas las
   crea el script solo, una por `EventId`.
2. **Extensiones → Apps Script**. Borrar el contenido y pegar
   `tools/survey/apps-script.gs`. Si querés un secreto, poner el mismo string en
   `SHARED_SECRET` del script y en `SharedSecret` de `SurveyConfig.asset`.
3. **Implementar → Nueva implementación** → tipo **Aplicación web** →
   *Ejecutar como:* **Yo** → *Quién tiene acceso:* **Cualquier persona** →
   Implementar → autorizar la cuenta → copiar la URL que termina en `/exec`.
4. Pegar esa URL en `SurveyConfig.asset → EndpointUrl`.
5. Sanity check desde el navegador: abrir la URL `/exec` debe mostrar
   `{"ok":true,"ping":true}`. Y con curl:

   ```
   curl -L -X POST -H "Content-Type: text/plain" --data "{\"event_id\":\"smoke\",\"response_id\":\"x1\",\"answers\":[{\"id\":\"fun\",\"value\":\"5\"}]}" "URL_EXEC"
   ```

   Respuesta esperada `{"ok":true}` y una pestaña `smoke` con la fila.

Cambios posteriores al script: **Implementar → Administrar implementaciones →
editar → Versión: Nueva** (la URL se mantiene). Si solo guardás sin nueva versión,
el `/exec` sigue corriendo el código viejo.

## Setup en Unity

1. `Rollgeon → Survey → Setup Survey` (idempotente). Crea `SurveyConfig.asset` con
   cuatro preguntas default, lo agrega a `ServiceBootstrap.asset` y escribe las 12
   keys `survey.*` en la tabla `UI`. Ya está corrido en el repo.
2. Editar `SurveyConfig.asset`: `EventId` (nombre de la pestaña, sin `[ ] : * ? / \`),
   `TriggerFloorIndex` (0 = al terminar el primer piso), `EndpointUrl`, preguntas.
   Tipos: `Rating1to5` (guarda "1".."5"), `SingleChoice` (guarda el **índice** de
   la opción, así ES/EN caen en la misma columna), `FreeText` (texto trimmed,
   `MaxLength`).
3. `Rollgeon → Survey → Validate Config` antes de buildear. Sin `EndpointUrl` es
   warning, no error: la encuesta aparece igual y guarda en disco.
4. Para verla en editor sin build de evento: tildar `Enabled` en la config, o
   abrirla a mano con `survey show` desde la consola (`` ` `` / F1).

## Build de evento

`Rollgeon → Build → Windows 64 (Evento)` → `Build/Windows64Event/Rollgeon.exe`.
Es la receta "sin Steam" (`DISABLESTEAMWORKS`) más `ROLLGEON_EVENT_BUILD`, ambos
por `extraScriptingDefines`: no tocan `ProjectSettings`. El script borra del output
`steam_appid.txt` y `Rollgeon_Data/Plugins/x86_64/steam_api64.dll`. Al zipear,
excluir `Rollgeon_BurstDebugInformation_DoNotShip`.

Con el define, la encuesta sale **siempre** en esa build, sin importar el tick
`Enabled`. Las builds Release/Development normales quedan inertes salvo que el tick
esté prendido — el default es apagado.

## Operación en el stand

- Las respuestas quedan en
  `%userprofile%\AppData\LocalLow\3AM Games\Rollgeon\survey\` (`pending/` y
  `sent/`, un JSON por respuesta; `Rollgeon → Survey → Open Responses Folder`).
- **Sin wifi** no se pierde nada: quedan en `pending/` y se suben solas al
  próximo arranque con red, o al empezar la siguiente run. Si la PC nunca tuvo red,
  copiar `survey/pending/` al `persistentDataPath` de otra PC con la build y abrir
  el juego, o subir los JSON a mano.
- Misma PC, muchos jugadores: el disparo es **una vez por run**, no por máquina.
  `device_id` identifica la PC del stand, no a la persona.
- Sorteo: pestaña del evento → filas con `raffle_opt_in = TRUE` → elegir una al
  azar (`=INDEX(K:K; RANDBETWEEN(2; COUNTA(K:K)))` sobre la columna `email`, o
  exportar CSV).

## Verificación

- Tests: EditMode de `Rollgeon.Survey.Tests`, `Rollgeon.UI.Tests`,
  `Rollgeon.DevConsole.Tests`.
- Editor: Play desde `00_Bootstrap`, consola `survey status` (activa, evento, piso,
  pendientes) → `survey show` → llenar → Enviar. Aparece un JSON en `survey/sent`
  (con endpoint) o en `survey/pending` (sin endpoint), y la fila en la pestaña.
- Trigger real: consola `floor`/`boss` para llegar al boss del piso configurado,
  reclamar la recompensa y ver la encuesta salir sola. Escape no abre la pausa
  encima. `survey reset` vuelve a armar el disparo en la misma run.

## Troubleshooting

| Síntoma | Causa | Fix |
|---|---|---|
| `survey status` dice "SIN CONFIG" | `SurveyConfig.asset` no está en `SettingsAssets` | `Rollgeon → Survey → Setup Survey` |
| `PushOverlay 'SurveyOverlay' no está registrada` | `Canvas_Survey` no cuelga del `ScreenHost` | Instanciar el prefab bajo el `ScreenHost` de `02_Gameplay` |
| Todo queda en `pending/` con endpoint seteado | URL sin `/exec`, deploy no es "Cualquier persona" (devuelve HTML de login), o `ok:false` por `secret` distinto | Probar la URL con el curl de arriba; revisar `SHARED_SECRET` |
| La pestaña no aparece | `EventId` con caracteres inválidos | `Validate Config` |
| Una pregunta nueva no tiene columna | Nada: el script agrega `q_<id>` al final la primera vez que la recibe | — |
| Fila duplicada | No debería: dedupe por `response_id` en el script | Verificar que el script desplegado sea la última versión |
| En editor tipear `p` en el email abre la consola | El atajo `P` de la DevConsole no mira el foco | Solo editor/dev-build; en la build Evento la consola no existe |
