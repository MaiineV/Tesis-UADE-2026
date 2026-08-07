# Registro de bugs y pulido — staging local

> **WS5.** Entradas listas para copiar al sheet compartido y a su ventana
> correspondiente. IDs siguen la serie existente del repo (última referencia
> encontrada en código: BUG-018). Formato: ID · Severidad · Área · Repro ·
> Esperado vs Observado · Branch.

## Bugs

> **Nota de numeración**: el código ya usa BUG-019 (gating de free rolls del chain,
> `CombatHandoffService.cs:924`) — la serie interna va más adelante que lo que refleja
> el código commiteado. El bug del escudo acá se numera **BUG-021**.

### BUG-021 — Escudo reusa la tabla de daño de ataque (causa raíz "escudo trivial")
- **Severidad**: Alta (balance-breaking)
- **Área**: Combat / Effects
- **Repro**: combatir con el Warrior y resolver cualquier combo alto; observar el escudo otorgado por la fase escudo del chain.
- **Esperado**: escudo acotado (spec v2: `min(base_escudo × multi, 8)`).
- **Observado**: escudo = `BaseDamage` del combo de ATAQUE × multiplier (`EffAddShield.cs:78-79`). Generala → 90 de escudo ≈ 45 turnos de inmunidad vs Melee (Attack 2).
- **Branch**: `feature/damage-formula-v2` (pre-fix). **Estado**: **FIXED 09/07** — `PlayerComboShield` (min(tabla × multi, 8)) + rewire de `EffAddShield` + tabla seedeada en CH_Warrior. 1743/1743 tests EditMode verdes, incl. regresión `ComboValue_UsesShieldTable_IgnoresAttackBaseDamage`. Sin commitear aún.
- **Nota 2026-08-07 (Spec Escudo v3)**: la fórmula separada y el cap 8 se revirtieron por decisión de diseño — el escudo ahora usa la fórmula completa de daño (sin cap; el anti-inmunidad es el reset por turno de `ShieldResetHandler` + daño enemigo ×10). La parte del fix que sigue vigente y blindada por tests es la causa raíz: el escudo jamás lee la tabla/BaseDamage de ATAQUE, su base sale de la `ShieldBaseTable`.

### ~~BUG-020 — Fase escudo hereda el ComboResult del ataque~~ INVALIDADO 09/07
**Falso positivo del análisis estático.** Verificado en código: el chain hace roll POR FASE
— tras la fase de daño, `PrepareNextChainPhase` → `RollViaThrow` (CNF-008: el jugador tira
de nuevo para la defensa, rerolls gateados por free rolls sobrantes del ataque) y
`ExecuteChainPhase` re-detecta el combo sobre los dados de ESA tirada
(`CombatHandoffService.cs:868-877`). La "lectura A" (tirada propia de escudo) **ya es el
diseño implementado** — la spec de Bocco se cumple sin cambios en el chain.

## Pulido / mejoras

### PUL-001 — `ED_Boss.asset` (100 HP / Atk 2) aparenta ser placeholder
Los tres bosses nominales (Sunken_Grand, Security_Boss, GeneralDirector) tienen 200 HP. Confirmar si `ED_Boss` se referencia en algún layout; si no, eliminarlo para evitar que un pool lo agarre por accidente.

### PUL-002 — `ExtraTiers` vacío en Ranged, Healer y los 3 bosses
Solo `ED_MeleeCardEnemy` define T2. El sistema de tiers está construido pero desaprovechado — es la perilla natural para la escalada de pisos 2-3 (ver `docs/planning/balance-modelo-3-pisos.md` §3.2).

### PUL-003 — HP de bosses plano entre pisos (200/200/200)
La escalada entre bosses es solo por Attack. Con upgrades de dados del jugador, el boss de piso 3 puede caer más rápido que el de piso 1. Revisar curva en sesión de balance.

### PUL-004 — Nada comunica que el escudo se gana atacando
Origen del planteo del profesor (ver `docs/design/pas-defensa-pura.md`): la regla "defensa solo tras atacar" es invisible para el jugador. Fix barato: tooltip/onboarding con la regla explícita.

### PUL-005 — `ItemSO` vs `ShopItemDef`: doble autoría de la misma identidad
- **Área**: Items / Shop
- Un item vendible se autora **dos veces**: `ItemSO` declara el efecto, `ShopItemDef` la identidad vendible, y los dos repiten `ItemId` / `DisplayName` / `Description` / `Icon`. Visible hoy en `Item_HealingPotion.asset` + `ShopItem_HealingPotion.asset` (mismo `potion.healing`). Solo `ShopItemDef` puede entrar a `ShopPool.asset`.
- **Riesgo**: editar uno y olvidar el otro deja la tienda mostrando datos viejos. El `Tools/Item Editor` (§26.13) solo edita el `ItemSO`, así que no lo detecta.
- **Resolución prevista**: `ShopItemDef` está documentado como placeholder MVP — muere cuando llegue `RewardEntrySO` (§19). **No arreglar antes**; el fix real es esa migración, no un parche de sincronización.
- **Estado**: abierto. Detectado al mapear el sistema para Feature#0032 (PR #48).

### PUL-006 — 3 `ShopItemDef` huérfanos comparten `ItemId="Item01"`
- **Área**: Items / Shop
- `D20Die.asset`, `D20DieEnchantmentPlus.asset` y `D20DieEnhancent.asset` (en `Assets/Rollgeon/Rooms/Shop/Items/`) son tres assets distintos con el **mismo** `ItemId="Item01"`, mismo `DisplayName`, mismo ícono y `Description` vacía. No son tres items: son restos de experimentación.
- **Hoy es inofensivo**: verificado por dependencias — ninguno está referenciado en `ShopPool.asset` ni en `SP_Tutorial.asset`, así que no aparecen en la tienda. Mismo riesgo que **PUL-001** — si alguien los carga a un pool por error, dos entries con el mismo id rompen la resolución por `GetById` (el `ValidateNoDuplicateIds` de `BaseCatalogSO` lo avisa solo si entran a un catálogo, y estos no están en ninguno).
- **Fix**: quedarse con uno, o borrar los tres si el D20 ya no es un item vendible.
- **Estado**: abierto. Detectado al mapear el sistema para Feature#0032 (PR #48).

### PUL-007 — `InventoryService` sin cobertura de activación ni de save/restore
- **Área**: Items / Tests
- `Rollgeon.Items.Tests` existe desde Feature#0032 (PR #48) pero cubre **solo** el bind/unbind de hooks pasivos (9 tests, ver §18.2.1). Siguen sin cobertura:
  - `ActivateItem` — la ruta de action economy (`ConsumesAction` → `ActionDefinitionSO` transitoria → `TurnManager.CanExecute`), el cooldown, y el `ConsumedOnUse` que remueve el slot **por índice**.
  - `CaptureState` / `RestoreState` (`SaveKey = "run.inventory"`) — rehidratación via `_catalog.GetById`, incluido el caso de un item cuyo asset ya no existe.
  - `ApplyPersistentModifiers` / `RemovePersistentModifiers`.
- **Por qué importa ahora**: hasta el PR #48 había **1 solo item autorado**, así que estas rutas casi no corrían. Con las tools de autoría (§26.13/§26.14) el contenido va a crecer y estas rutas pasan a ser calientes.
- **Estado**: abierto. Anotado al cerrar Feature#0032 (PR #48).

### PUL-008 — El D3 se ve idéntico al D4 (el sheet de dados no trae su fila)
- **Área**: UI / Dice / Arte
- `Assets/Art/UI/Dices/Dices.png` trae 6 filas (D4, D6, D8, D10, D12, D20). El **D3** es el 7º valor de `DiceType` — llegó con el pack de Encantamientos, después de que se pintara el sheet. Para que el catálogo valide, `DiceShapeCatalogAuthoring` le asigna la fila del **D4** (decisión explícita, ver `TypeRows`).
- **Riesgo**: un jugador con D3 y D4 en la bolsa no los distingue salvo por el número. `Validate`/`ValidateRoles` chequean **tipo** duplicado, no **sprite** duplicado, así que nada lo atrapa automáticamente.
- **Fix**: pedirle al artista la fila del D3 (5 columnas: frontal, 2 laterales, hover, selected). Al llegar, solo se re-slicea el sheet y se corre `Tools/Rollgeon/Dice/Author Shape Catalog From Sheet` — cero código.
- **Estado**: abierto. Decidido al implementar Feature#0033.

### PUL-009 — El throw 2D/3D no cicla los sprites laterales del set
- **Área**: UI / Dice
- `DiceThrowDieView.SetDiceType` (`:44-49`) toma el **frontal** del set vía el default de `GetShape` y nada más: en modo throw el dado rota de verdad en vuelo (`DiceThrow2DPresenter.cs:935`), así que el ciclado 0-1-0-2 del modo Classic no aplica tal cual.
- **Consecuencia**: el hover/selected/laterales del arte nuevo solo se ven en el HUD Classic. Si el juego shippea en modo throw, media inversión del sheet no se usa.
- **Fix**: decidir con arte/diseño si el dado volador debe cambiar de sprite según su rotación (leer el ángulo y mapearlo a frontal/lateral) o quedarse con el frontal.
- **Estado**: abierto. Alcance excluido a propósito en Feature#0033, no olvido.

### PUL-010 — El último tick del spin nunca dispara (off-by-one)
- **Área**: UI / Dice / Anim
- Con el tuning shippeado `TickCount(0.5, 0.06)` = **8**, pero `TickTime(8, 8, …)` devuelve exactamente `0.5` = la duración, y el loop de `DiceSlotAnimator.SpinRoutine` corre `while (elapsed < plan.Duration)`. El tick 8 nunca entra: se ven **7 de 8** cambios de cara.
- **Hoy es benigno**: 7 u 8 parpadeos de número random en 0.5s es imperceptible, y el ciclado de sprites de Feature#0033 está construido para aterrizar bien igual (el tick 7 es impar ⇒ frontal, y `LandFromSpin` suelta el rol pase lo que pase).
- **Cuidado si se toca**: `DiceAnimChoreographerTests.SpinRole_LastReachableTick_IsFront_WithShippedTuning` documenta y asierta este comportamiento — arreglar el off-by-one cambia cuál es el último tick y hay que revisar en qué rol aterriza.
- **Estado**: abierto. Detectado al implementar Feature#0033.

### PUL-011 — La generación conecta salas sin validar que el prefab tenga puerta en esa dirección
- **Área**: Dungeon / Generación
- `DungeonManager.GenerateFloor` cablea `RoomInstance.Connections` por pura adyacencia de grilla (N/S/E/W del vecino que exista) y elige prefab por `RoomType`, sin ningún paso que garantice que el prefab asignado tenga un `DoorController` para cada dirección conectada. El minimapa/fog lee ese grafo lógico; el paso físico lee las puertas autoradas del prefab. Si divergen, hay conexión en el mapa pero no puerta cruzable.
- **Cómo se manifestó**: `Start_Room01.prefab` era la única sala sin puerta Este (arreglado en `Fix#0034`, commit `d5c036a0`). Al estar en la celda (0,0), la topología podía poner una sala al Este del spawn → conexión visible, sala inalcanzable.
- **Hoy es benigno**: tras el fix, **las 21 salas de piso tienen las 4 puertas** `[N,S,E,O]`, así que ninguna divergencia es posible con el contenido actual. El runtime ya **detecta** el mismatch (`DungeonManager.cs` loguea "tiene Connection al {dir} pero el prefab no tiene DoorSlotRef" desde ambos lados de la reciprocidad) pero **solo avisa, no repara**.
- **Riesgo a futuro**: cualquier sala nueva autorada sin las 4 puertas reintroduce el bug en silencio (solo un warning en consola). El usuario declinó el guard de generación en `Fix#0034` a propósito (alcance).
- **Fix**: un paso de validación/reparación en generación (o un test de assets que recorra `Assets/Prefabs/Rooms/**` y falle si un `RoomLayout` no tiene los 4 `DoorSlots`). El tool `Rollgeon/Tools/Diagnose Room Doors` ya hace el diagnóstico manual.
- **Estado**: **cerrado** en `Fix#0034`. Se agregó el guard en generación (`DoorTopologyGuard` + `DungeonManager.PruneDoorlessConnections`, paso 5c de `GenerateFloor`): poda las conexiones cuya dirección no tiene puerta autorada (o cuyo vecino no tiene la recíproca) de ambos lados, con `LogError` por poda, y reporta con `LogError` cualquier sala que quede inalcanzable tras podar. Con las 21 salas actuales (4 puertas c/u) es no-op. Cubierto por `DoorTopologyGuardTests` (7 tests de lógica pura).

### PUL-012 — El `.exe` no tiene metadatos de producto (Windows lo muestra como "rollgeon.exe")
- **Severidad**: Baja (cosmético, pero visible al jugador antes de abrir el juego)
- **Área**: Build / Steam
- **Repro**: primera ejecución de `Build/Windows64/Rollgeon.exe` → el diálogo de permisos de red del firewall de Windows muestra **`rollgeon.exe`** en minúscula.
- **Esperado**: "Rollgeon", o al menos "Rollgeon.exe" respetando mayúsculas.
- **Observado**: `(Get-Item Rollgeon.exe).VersionInfo` devuelve `ProductName`, `FileDescription` y `CompanyName` **vacíos**; `ProductVersion` es `6000.3.11f1` (la de Unity, no `bundleVersion`). Sin `FileDescription`, Windows cae al nombre del archivo, y la regla de firewall guarda el path entero en minúscula (`...\build\windows64\rollgeon.exe`).
- **Causa**: no es misconfiguración. El ejecutable del player de Windows es **pre-compilado** y Unity no le estampa los valores de PlayerSettings — el manual documenta que hay que modificar los metadatos uno mismo. Afecta también a Task Manager y a las heurísticas de antivirus (binario sin firmar y sin metadatos).
- **Fix propuesto**: paso post-build en `RollgeonBuild.CopySteamAppId`-style que estampe el version info con `rcedit` (dependencia externa, ~1 MB). Alternativa: firmar el ejecutable, que resuelve esto y el warning de SmartScreen a la vez.
- **Branch**: detectado en `Feature#0036_SteamBuild`. **Estado**: abierto, no bloquea la primera subida a Steam.

### PUL-013 — El ataque telegrafiado del Sunken Grand no anima

- **Severidad**: Baja (cosmético — el daño y el telegraph funcionan bien)
- **Área**: Combat / Enemies / Anim
- El árbol de `ED_Boss_Sunken_Grand` pega por **dos** caminos: el `AINode_Behavior` con la acción `Ranged` (que en `Feature#0038` quedó sincronizada al frame de impacto, eligiendo `Attack_Melee` o `Attack_Range` según la distancia Manhattan al target) y el par `AINode_TelegraphMark` → `AINode_ExecuteTelegraph`, que resuelve el daño por su cuenta **sin pasar por ningún `EffectData`**.
- **Consecuencia**: cuando el boss cobra una marca telegrafiada, el modelo se queda en Idle y el daño aparece sin windup. Contrasta feo con el otro camino, que ahora sí anticipa el golpe.
- **Estado**: **cerrado** en `Feature#0038_SunkedGrandAnimSync`. `AINode_ExecuteTelegraph` ganó dos campos autorables (`WindupFeedbackId` + `ImpactEventKey`) y un `TickCoroutine` que corre ese feedback **antes** de resolver el daño, bloqueando en el Animation Event — el mismo gate que usa la acción autorada. El `Tick` síncrono (EditMode) queda igual que antes, sin windup, porque ahí no hay dónde esperar el evento. El Sunken Grand quedó autorado con `anim.enemy.sunken_grand.range` / `hit`; el General Director y el Security Boss quedan vacíos, o sea sin cambio de comportamiento hasta que se les autore su propia entry.

### PUL-014 — Un nodo de movimiento que devuelve `Failed` congela el turno entero del enemigo

- **Severidad**: Alta cuando pega (el enemigo deja de jugar), pero depende de cómo esté armado cada árbol
- **Área**: Combat / AI
- Los nodos de movimiento devuelven `AIResult.Failed` en el caso **benigno** de "no hay nada que hacer" — `AINode_KeepDistance` cuando ya está a distancia ideal o no encuentra un tile mejor, `AINode_Move` cuando no hay path. Es lo que manda el contrato de `AIActionNode` ("`Failed` si no se ejecutó, ej. fuera de rango"), pero **`AINode_Sequence` aborta al primer `Failed`**: todo lo que venga después en el sequence no corre.
- **Cómo se manifestó**: el Sunken Grand tenía `KeepDistance` en el índice 1 de 5, antes del ataque, con `IdealDistance = 5`. Parándose a 5+ casillas del boss, el nodo devolvía `Failed` y se llevaba puesto el buff de fase, el ataque/telegraph y el rotate block: **el boss se quedaba literalmente quieto**. Arreglado en `Feature#0038` envolviéndolo en `Selector(KeepDistance, Wait)`, el idiom de "intentá esto, no importa si falla".
- **Sigue latente en otros lados**: `ED_RangedEnemy` tiene `Sequence[Move, If[...Attack]]` — si `AINode_Move` no encuentra path, el ataque de esa rama no corre. `ED_RangedEnemy` y `ED_Healer` esquivan el caso de `KeepDistance` poniéndolo **último** en su sequence, así que hoy no se nota, pero es por acomodo, no por diseño.
- **Fix de fondo a evaluar**: separar "no había nada que hacer" de "falló de verdad" (un `AIResult.Skipped`, o que estos nodos devuelvan `Succeeded` cuando el no-op es benigno). Ojo: cambiar la semántica ripplea a los `While` del ranged y del healer, que hoy cortan su loop con el `Failed` del body. Por eso en `Feature#0038` se arregló el árbol y no el nodo.
- **Mitigación puesta**: `<remarks>` de advertencia en `AINode_KeepDistance` con el idiom del `Selector`.
- **Branch**: detectado en playtest durante `Feature#0038_SunkedGrandAnimSync`. **Estado**: el caso del Sunken Grand está cerrado; la deuda de fondo (semántica de los nodos de movimiento) queda abierta.

### PUL-015 — Un `MMF_PositionSpring` mal tuneado escupe NaN y apaga todo el canvas de dados

- **Severidad**: Crítica cuando pega (bloquea el playtest entero: los boards no se dibujan)
- **Área**: UI / Juice / Feel
- **Repro**: entrar a la zona de dados, tirar una vez y **dejarla quieta ~1.6 s** sin volver a tirar. Reproducible al 100% grabando con Unity Recorder; intermitente en playtest normal.
- **Observado**: `Invalid worldAABB. Object is too large or too far away from the origin.` + `transform.localPosition assign attempt for 'DiceZoneView' is not valid. Input localPosition is { 0, NaN, 0 }` desde `MMF_PositionSpring.ApplyValue`. Los dice boards no aparecen.
- **Causa**: el feedback `Zone Roll Shake` (`Canvas_ActionRoll.prefab`, target `DiceZoneView`) estaba autorado con **`DampingY 0.55` / `FrequencyY 14`**. `MMMaths.Spring` (`Assets/Feel/MMTools/Core/MMHelpers/MMMaths.cs:44`) integra con Euler semi-implícito y **clampea el sub-step a 1/60 fijo**. A 14 Hz eso da h·ω = 1.47, fuera de la región de estabilidad para ζ = 0.55: el autovalor dominante queda en **|z| ≈ 2.06**, o sea la amplitud se duplica en cada sub-step. A los ~97 sub-steps llega a `Inf`, y el `Inf - Inf` del paso siguiente da **NaN**. El NaN entra en el `anchoredPosition3D` de `DiceZoneView` y contamina la matriz de toda la subjerarquía → el `Invalid worldAABB` es el síntoma, no un bug aparte.
- **Por qué "solo con el Recorder"**: el spring necesita correr ~1.6 s sin interrupción, y `MmfJuice.Replay` reinicia el reloj en cada roll (`StopFeedbacks` + `RestoreInitialValues`), igual que `Rest()` en el `OnDisable` de la zona. El Recorder fija `Time.captureDeltaTime` al frame rate objetivo, así que cada frame alimenta un múltiplo **exacto** de 1/60 y pega justo en la resonancia patológica. Con jitter real depende: dt exacto 1/60 → NaN al frame 97; jitter 14-20 ms → NaN al 123; jitter 17-33 ms → se estabiliza. **No era exclusivo del Recorder**: una máquina a 60 fps parejos también lo dispara.
- **Estado**: **cerrado**. `FrequencyY: 14 → 10` (amplitud 0.57 px → 0.8 px, dentro del "≤2 px" que pide el tooltip; el resto de los parámetros sin tocar). Validado por simulación del integrador en dt = 1/60, 1/50, 1/30, 1/24, 0.02, 0.025, 0.05, 0.1, 0.2 y 0.333: converge en todos.
- **Restricción que queda para autoría**: con el sub-step de 1/60 que clampea Feel, **una frecuencia ≥ 11 Hz con damping ≥ 0.4 diverge**. Los otros springs del proyecto (0.45/12 y 0.50/10) quedan por debajo del umbral, pero el de 12 Hz está al borde. Barrido de todos los `.prefab`/`.unity`/`.asset` fuera de `Assets/Feel`: tras el fix, **0 springs divergentes**.

### PUL-016 — El prompt "… Roll (1E)" queda pegado y reaparece sobre el roll de ataque siguiente

- **Severidad**: Media (cosmético, pero engañoso — el jugador lee que le van a cobrar 1E por una tirada que es gratis)
- **Área**: UI / Combat / Chain
- **Repro**: entrar a una fase de chain **paga** (defensa post-ataque sin free rolls sobrantes pero con energía > 0) → aparece el prompt central `"{fase} Roll (1E)"`, que es correcto → **no** pagarlo → el roll de ataque siguiente arranca con el prompt todavía prendido.
- **Causa**: el GO `ChainRollPrompt` arranca inactivo y **solo** lo prende `ChainRollPromptView.Show()`, llamado desde un único lugar (`CombatHandoffService.cs:1071`). `Hide()` se llamaba en dos: `:800` (el jugador paga y tira) y `:1149` (`FinishChain`). Pero `ResetCombatPhaseState()` (`:280-310`) apagaba el flag `_awaitingChainPaidRoll` **sin** esconder el prompt, con este comentario: *"el prompt del board se auto-esconde al desactivarse la zona"*. Es falso — desactivar un canvas ancestro no limpia el `m_IsActive` propio del hijo, y `ChainRollPrompt` vive dentro de `Canvas_ActionRoll`, un canvas persistente de la escena. `ResetCombatPhaseState` corre en `OnCombatEnd` y al arranque de cada combate, así que un combate que cerraba con la entrada paga pendiente se lo filtraba al siguiente.
- **Cómo se llega a ese estado**: el comentario de `:230-236` ya lo describía — el enemigo muere antes de que el jugador consuma todas las fases del chain. Con el gate de feedback la secuencia de muerte se difiere, así que `ContinueChainPhase` alcanza a mostrar el prompt pago y recién después llega `OnCombatEnd`.
- **Lo que quedó sin probar**: el usuario también reportó el bug saliendo por **Pass del chain**. Ese path (`:837-852`) pasa por `FinishChain`, que sí esconde — no se pudo reproducir el leak leyendo el código. Por eso el fix se hizo independiente del camino en vez de tapar un call site puntual: si el de Pass tenía otra causa raíz, queda cubierto igual.
- **Estado**: **cerrado** en `Fix#0039_ChainRollPromptStuck`. Dos capas: (1) `ChainRollPromptView` se apaga solo con `OnChainCompleted` / `OnCombatEnd` — se suscribe en `Show()` y se suelta en `Hide()`, así la ventana de escucha es exactamente "el prompt está arriba" (no se usó OnEnable/OnDisable porque en EditMode no corren; `OnDisable` queda solo como guard de teardown); (2) `ResetCombatPhaseState` resuelve el hud vía `_screenManager.Current` y esconde el prompt en la misma operación que resetea el flag, para que el servicio sea correcto por sí mismo. Cubierto por 4 tests nuevos en `ChainRollPromptViewTests` y 2 en `CombatHandoffServiceTests`.
- **Restricción que queda**: el prompt y `_awaitingChainPaidRoll` son el mismo estado. Cualquier camino de salida nuevo que apague el flag tiene que apagar el prompt — y si se olvida, la suscripción de la view lo tapa.

---

**Pendiente del usuario**: link/columnas del sheet compartido y qué ventana
corresponde a cada categoría, para migrar estas entradas y las futuras.
