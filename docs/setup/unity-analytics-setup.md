# Unity Analytics (UGS) — Setup y validación (Feature#0029)

Telemetría de balance para playtests con `com.unity.services.analytics` 6.3.0,
consentimiento **opt-in universal** (GDPR) y cero cambios en gameplay: todo se
intercepta desde el event bus.

## Qué hay en código

| Pieza | Ruta | Rol |
|---|---|---|
| Package UPM | `Packages/manifest.json` → `com.unity.services.analytics@6.3.0` | SDK UGS (Core se resuelve como dependencia) |
| Contratos | `Assets/Scripts/Rollgeon/Analytics/IAnalytics{Sink,Gateway,ConsentService}.cs` | Fachadas sin tipos del SDK (asm `Rollgeon`) |
| `AnalyticsPrefs` | `Assets/Scripts/Rollgeon/Analytics/AnalyticsPrefs.cs` | Consent en PlayerPrefs `rollgeon.analytics.*` — sobrevive al "Borrar partida"; **prohibido `PlayerPrefs.DeleteAll`** |
| `AnalyticsEvents` | `Assets/Scripts/Rollgeon/Analytics/AnalyticsEvents.cs` | Schema en código, espejo 1:1 del Event Manager |
| `AnalyticsTrackerService` | `Assets/Scripts/Rollgeon/Analytics/AnalyticsTrackerService.cs` | Bus → eventos custom (Priority 96, Global) + registra el consent service |
| Aggregators | `RunAggregator.cs` / `CombatAggregator.cs` | Acumuladores per-run/per-combat (siempre corren, con o sin consent) |
| Capa UGS | `Assets/Scripts/Rollgeon/Analytics/Ugs/` (asm propio) | `UgsAnalyticsSink` + `UgsAnalyticsBootstrap` (Priority 15, init async fire-and-forget, env `development` en editor / `production` en build) |
| UI consent | `AnalyticsConsentOverlay` + toggle en `MainMenuScreen` | Popup first-run (overlay no-destructivo) + toggle "Telemetría: ON/OFF" |
| Bootstrap | `Assets/Rollgeon/ServiceBootstrap.asset` → ExtraServices | Entries `UgsAnalyticsBootstrap` + `AnalyticsTrackerService` (ya cableadas) |
| Comando consola | `analytics status\|opt-in\|opt-out\|reset\|delete\|test` | Solo editor/dev-build (`` ` `` o F1) |
| Editor setup | menú **Rollgeon → Analytics → Setup Localization Entries** | 7 keys `menu.analytics_*` ES/EN en tabla `UI` (idempotente) |
| Tests | `Assets/Scripts/Rollgeon/Analytics/Tests/` (44 tests EditMode) | Traducción, agregación, consent gating, degradación |

## Setup en Unity Cloud (manual, una sola vez)

1. **Linkear el proyecto**: Unity → Edit → Project Settings → **Services** →
   sign-in con la cuenta del equipo → seleccionar organización → **Create/Link
   project ID**. Esto llena `cloudProjectId` en `ProjectSettings.asset`.
2. En [cloud.unity.com](https://cloud.unity.com) → el proyecto → **Environments**:
   verificar que exista `production` (default) y **crear `development`** —
   el editor manda ahí para no contaminar los datos de playtest.
3. **Event Manager** (Analytics → Event Manager): declarar los 10 eventos de la
   tabla de abajo con sus parámetros tipados. Evento o parámetro no declarado →
   el evento llega como *invalid* y no entra a los charts.

## Schema de custom events (Event Manager)

Parámetros comunes a **todos** los eventos: `run_id` STRING (Guid), `is_editor`
BOOLEAN, `app_version` STRING.

| Evento | Parámetros propios (tipo) |
|---|---|
| `run_started` | `hero_id` S, `ruleset_id` S, `is_continue` B, `seed` INT, `floor_index` INT |
| `run_ended` | `outcome` S (`victory`\|`defeat`\|`abandon`), `hero_id` S, `floors_cleared` INT, `duration_sec` FLOAT, `combats_won` INT, `gold_earned` INT, `gold_spent` INT, `combos_matched` INT, `was_resumed` B, `floor_index` INT |
| `floor_reached` | `floor_index` INT, `hp_at_entry` INT, `gold_at_entry` INT |
| `combat_ended` | `floor_index` INT, `room_type` S, `outcome` S, `turn_count` INT, `duration_sec` FLOAT, `damage_dealt` INT, `damage_taken` INT, `rerolls_used` INT, `energy_spent` INT, `hp_remaining` INT, `top_combos` S, `boss_phase_reached` INT |
| `player_death` | `floor_index` INT, `room_type` S, `turn_count` INT, `boss_phase` INT |
| `combo_matched` | `combo_id` S, `base_damage` INT, `multiplier` FLOAT, `floor_index` INT |
| `shop_purchase` | `item_id` S, `price` INT, `gold_remaining` INT, `floor_index` INT |
| `item_obtained` | `item_id` S, `source` S, `floor_index` INT |
| `active_item_used` | `item_id` S, `floor_index` INT |
| `unlock_achieved` | `unlock_id` S, `category` S, `during_run` B |

Notas de semántica:

- **Outcome derivado**: `OnRunEnd` no trae outcome (args[1]=null). `run_ended`
  se envía *eager* en victoria/derrota (cubre cerrar el juego en la pantalla
  final) y `OnRunEnd` sin marker previo = `abandon` (quit desde pausa).
- **Continue/resume**: cada resume re-emite `run_started` con el mismo `run_id`
  e `is_continue=true`; los agregados de una fila `was_resumed=true` cubren solo
  el último segmento de sesión (cota inferior). Analizar por `run_id` distinct.
- **Tutorial**: no trackea nada (gate por `PendingRunRequest.IsTutorial`).
- **Editor vs build**: separación primaria por environment (`development` /
  `production`); cinturón extra: filtrar `is_editor=false` en dashboards.

## Smoke test (editor)

1. **Play** desde `00_Bootstrap` → consola: `[Analytics] UGS init OK
   (env=development, consent=sin decidir).` (sin link a Unity Cloud: warning
   único `Init de UGS falló` y el juego sigue — es la degradación esperada).
2. Primera ejecución (sin decisión previa) → popup "Datos de juego anónimos"
   sobre el menú. **Aceptar** → el toggle del menú pasa a `Telemetría: ON`.
3. `` ` `` → `analytics status` → `UGS init: OK | sink ready: True | consent:
   granted | eventos dropeados: 0`.
4. `analytics test` → evento `debug_ping` en el **Event Browser** del dashboard
   (~minutos; aparece como inválido a propósito — es solo conectividad).
5. Run corta: victoria, otra con derrota, otra abandonada desde pausa → en el
   Event Browser: `run_started`, `floor_reached`, `combat_ended`,
   `combo_matched`, `run_ended` con los 3 outcomes, todos **válidos**.
6. Negativo: `analytics reset` → volver al menú → popup re-pregunta →
   **Rechazar** → jugar → cero eventos nuevos en el Event Browser.

## Troubleshooting

| Síntoma | Causa probable |
|---|---|
| `Init de UGS falló … UnityProjectNotLinkedException` | Proyecto sin linkear (Project Settings → Services) |
| `Init de UGS falló` con proyecto linkeado | Environment `development` no existe en Unity Cloud, o sin red |
| Eventos llegan como *invalid* | Evento/parámetro no declarado en el Event Manager, o tipo distinto al de la tabla |
| Popup no aparece | Ya hay decisión guardada — `analytics reset` para re-preguntar |
| `sink ready: False` con init OK | Consentimiento denied o sin decidir (`analytics opt-in`) |
| Eventos no aparecen en dashboards agregados | Delay normal (~horas). El Event Browser es la fuente rápida (~minutos) |
| Datos de editor mezclados con playtest | Filtrar por environment `production` + `is_editor=false` |

## Privacidad (GDPR)

- Opt-in universal: sin "Aceptar" explícito no se envía **nada** (el SDK queda
  dormido; la vía moderna es `EndUserConsent`, `StartDataCollection` está
  obsoleto).
- Revocable en cualquier momento: toggle "Telemetría" del menú u `opt-out`.
- Borrado de datos (Right to be Forgotten): `analytics delete` en la consola —
  roadmap: botón en la UI del popup.
- El consent (propio y del SDK) vive en PlayerPrefs → **nunca** usar
  `PlayerPrefs.DeleteAll()` en el proyecto.
