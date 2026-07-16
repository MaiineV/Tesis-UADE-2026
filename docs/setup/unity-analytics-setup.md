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

### Descripciones de eventos (para pegar en el Event Manager)

| Evento | Descripción |
|---|---|
| `run_started` | Arranca una run (o se reanuda desde Continue). Base del funnel de runs. |
| `run_ended` | Termina una run con outcome (victory/defeat/abandon) y los agregados de toda la run. Una sola vez por run por sesión. |
| `floor_reached` | El jugador entra a un piso nuevo, con HP y oro al momento de entrar. Curva de dificultad y economía por piso. |
| `combat_ended` | Cierra un combate con sus agregados (turnos, daño, rerolls, energía, combos). El evento central de balance. |
| `player_death` | El jugador murió: piso, tipo de sala, turnos que aguantó y fase del boss. |
| `combo_matched` | El jugador matcheó un combo al resolver una tirada. Pick-rate y daño por combo. |
| `shop_purchase` | Compra en la tienda: qué ítem, a qué precio y con cuánto oro quedó. |
| `item_obtained` | El jugador obtuvo un ítem (pickup, recompensa o compra). |
| `active_item_used` | El jugador usó un ítem activo. |
| `unlock_achieved` | Se cumplió un unlock de meta-progresión. |

### Descripciones de parámetros (se crean una vez, globales)

| Parámetro | Tipo | Descripción |
|---|---|---|
| `run_id` | STRING | Guid de la run (formato "N", sin guiones). Agrupa todos los eventos de una misma run; en resumes se repite entre sesiones. |
| `is_editor` | BOOLEAN | true si el evento salió del editor de Unity. Filtrar false para datos de playtest. |
| `app_version` | STRING | Application.version de la build que envió el evento. |
| `hero_id` | STRING | Id canónico de la clase héroe (ej. "hero.warrior"). |
| `ruleset_id` | STRING | Ruleset de la run; "default" si no se especificó. |
| `is_continue` | BOOLEAN | true si la run se reanudó desde el botón Continue (agregados parciales). |
| `seed` | INT | Seed de generación del dungeon (hash del run_id). Para reproducir una run. |
| `floor_index` | INT | Piso actual (0-based) donde ocurrió el evento. |
| `outcome` | STRING | Resultado: victory/defeat/abandon en run_ended; Victory/Defeat/Aborted en combat_ended. |
| `floors_cleared` | INT | Pisos completados en la run (segmento de sesión si was_resumed). |
| `duration_sec` | FLOAT | Duración en segundos (de la run o del combate según el evento). |
| `combats_won` | INT | Combates ganados en la run. |
| `gold_earned` | INT | Oro total ganado en la run. |
| `gold_spent` | INT | Oro total gastado en la run. |
| `combos_matched` | INT | Combos matcheados en la run. |
| `was_resumed` | BOOLEAN | true si la run vino de un save: los agregados cubren solo el último segmento (cota inferior). |
| `hp_at_entry` | INT | HP del jugador al entrar al piso. |
| `gold_at_entry` | INT | Oro del jugador al entrar al piso. |
| `room_type` | STRING | Tipo de sala (Start/Combat/Boss/Shop/Potion/Enchantment). |
| `turn_count` | INT | Turnos del jugador en el combate. |
| `damage_dealt` | INT | Daño infligido por el jugador en el combate (incluye lo absorbido por escudos). |
| `damage_taken` | INT | Daño recibido por el jugador en el combate (incluye lo absorbido por escudos). |
| `rerolls_used` | INT | Rerolls usados por el jugador en el combate. |
| `energy_spent` | INT | Energía gastada por el jugador en el combate (solo decrementos; refills no cuentan). |
| `hp_remaining` | INT | HP del jugador al terminar el combate. |
| `top_combos` | STRING | Combos del combate como "id:count,id:count" (desc por uso, máx 100 chars). |
| `boss_phase_reached` | INT | Fase máxima de boss vista en el combate (1-based). 0 = sin boss. |
| `boss_phase` | INT | Fase del boss al morir el jugador. 0 = sin boss. |
| `combo_id` | STRING | Id del combo matcheado (clave del catálogo de combos). |
| `base_damage` | INT | Daño base del combo antes de mitigaciones/multiplicadores. |
| `multiplier` | FLOAT | Multiplicador de daño por calidad de dados (1.0 = sin cálculo). |
| `item_id` | STRING | Id del ítem (rewardId en compras). |
| `price` | INT | Precio pagado en oro. |
| `gold_remaining` | INT | Oro restante tras la compra. |
| `source` | STRING | Origen del ítem: tipo de la sala en curso (proxy). |
| `unlock_id` | STRING | Id de la definición de unlock cumplida. |
| `category` | STRING | Categoría del elemento desbloqueado. |
| `during_run` | BOOLEAN | true si el unlock se logró a mitad de run; false al cierre. |

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
