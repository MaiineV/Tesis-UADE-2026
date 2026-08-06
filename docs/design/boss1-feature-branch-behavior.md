# Boss Piso 1 "The Sunken Grand" — diseño de las feature branches

> **Propósito.** Este documento captura, leyendo el código real, el diseño del
> boss de piso 1 tal como quedó en las ramas de feature
> `sprint04/feature/boss1-piso1-mecanicas` (PR #46) y su superset
> `sprint04/fix/boss-damage-zero-fix` (PR #55), **antes** del merge
> "develop-prioritized" que descarta los cambios de data/assets de estas ramas.
> El objetivo es poder **re-aplicar** estas mecánicas encima de la versión de
> `develop` después del merge, usando el editor de AI-tree y el Inspector de
> Unity. Nada debería perderse.

---

## ⚠️ AVISO — features del boss del lado de `develop` que hay que PRESERVAR

`develop` evolucionó el MISMO boss por su cuenta (PR #56 *SunkedGrandAnimSync* +
follow-ups). Tomamos el boss de `develop` como base, así que estas mejoras
**NO se deben pisar** al re-aplicar lo nuestro. Son cambios de animación/feel,
independientes de la lógica de árbol que reconstruimos abajo:

| Commit (origin/develop) | Qué preserva |
|---|---|
| `eb854f6f` | **pick the sunken grand attack anim by manhattan range** — selección de animación de ataque según la distancia Manhattan al jugador. |
| `312c5bec` | **sync the telegraphed boss attack to its windup animation** — el telegraph se ejecuta sincronizado con la animación de windup. |
| `ba27c37e` | **keep the threat overlay lit during the telegraph windup** — el overlay de amenaza queda encendido durante el windup. |
| `a313eb81` | **finish the attack animation and face the target before acting** — termina la anim y mira al target antes de actuar. |
| `e487e0e3` | **stop the sunken grand from freezing when the player is far** — fix del boss que se congelaba cuando el jugador estaba lejos. |
| `180779b7` | **point AnimCon_SunkedGrand states at their own clips** — animator controller apuntando a sus clips. |

**Regla de oro:** al re-aplicar, tocar **solo la lógica del árbol de AI y los
servicios/mecánicas** (abajo). No revertir animator, windup-sync, ni el fix de
freeze. Si el árbol nuestro entra en conflicto con el windup-sync, priorizar el
windup-sync de develop y adaptar el árbol.

---

## Índice de mecánicas

1. Estructura del árbol de AI (fases + rotación de ataque + chase)
2. Rain Hazard (lluvia de zonas dispersas)
3. Refuerzos + fix de barra de HP del minion
4. Dice-block (candadito) — PR #55
5. Historia de la regla "repeat-combo = 0 daño" (PR #46, REMOVIDA)
6. Threat shapes de soporte (`SquareAroundSelf`, `DirectionalBand`, `ScatteredSquares`)
7. Wiring de bootstrap/servicios + dev console

---

## Datos base del boss

Asset: `Assets/Rollgeon/Enemies/ED_Boss_Sunken_Grand.asset`
(Odin `SerializedScriptableObject`, tipo `Rollgeon.Entities.EnemyDataSO`).

| Campo | Valor |
|---|---|
| `EntityId` | `boss.sunken_grand` |
| `DisplayName` | The Sunken Grand |
| `BaseHP` | **200** |
| `BaseAttack` | 2 |
| `BaseHealStrength` | 3 |
| `BaseSpeed` | 5 |
| `MaxEnergy` | 3 |
| `BaseAttackRange` | 1 |
| `MinGoldDrop` / `MaxGoldDrop` | 15 / 23 |

Todos los umbrales de HP de abajo son **fracción de BaseHP** (200), evaluados por
`PcOwnerHpBelow.Percent`.

---

## 1. Estructura del árbol de AI

El `AIRoot` es un **`AINode_Sequence`** con **5 hijos**, evaluados en orden cada
turno del boss. Los 3 primeros son gates de fase envueltos en
`If(PcOwnerHpBelow) → Once(...)` con rama `Else = Wait` (para no abortar el
sequence). El 4º es el pool de ataque rotativo. El 5º es el dice-block.

> **Ojo con el Sequence raíz:** aborta ante el primer hijo `Failed`. Por eso cada
> gate tiene `Else = AINode_Wait` (que devuelve Succeeded) y por eso el bug
> histórico del boss "que no hacía nada" era el RainHazardService sin registrar
> (ver §7): `AINode_ActivateRainHazard.Tick` devolvía `Failed` y cortaba todo el
> sequence antes de llegar al pool de ataque.

Clases de nodo (C#) bajo `Assets/Scripts/Rollgeon/Combat/AI/Decisions/`.

### Hijo 1 — Fase 2 a 10% HP (buff de velocidad)

```
AINode_If (TargetSelector_Self, PcOwnerHpBelow Percent=0.10)
  Then → AINode_Once → AINode_ApplyStatModifier
                         AttackDelta=0, SpeedDelta=+2,
                         PhaseIndex=2, EmitPhaseChangedEvent=true
  Else → AINode_Wait
```

- **`AINode_ApplyStatModifier`** (`AINode_ApplyStatModifier.cs`): aplica un
  `Modifier<int>` **permanente** (`ModifierLifetime.Permanent`) al propio boss.
  Params: `AttackDelta`, `SpeedDelta`, `PhaseIndex`, `EmitPhaseChangedEvent`.
  Emite `EventName.OnBossPhaseChanged(selfGuid, PhaseIndex)` para feedback/diálogo.
- Aquí: **+2 Speed** (reordena la cola de turnos en la ronda siguiente), Attack
  sin cambio. Se dispara una sola vez (`AINode_Once`) al bajar de **10%** HP.

### Hijo 2 — Activar Rain Hazard a 70% HP

```
AINode_If (PcOwnerHpBelow Percent=0.70)
  Then → AINode_Once → AINode_ActivateRainHazard
  Else → AINode_Wait
```

- **`AINode_ActivateRainHazard`** (`AINode_ActivateRainHazard.cs`): llama
  `RainHazardService.Activate()` (idempotente). Una sola vez al cruzar **70%**.
  Devuelve `Failed` si el servicio no está registrado (ver §7). Detalle en §2.

### Hijo 3 — Refuerzos a 50% HP

```
AINode_If (PcOwnerHpBelow Percent=0.50)
  Then → AINode_Once → AINode_SpawnReinforcements
                         EnemyToSpawn=<ref>, Count=2
  Else → AINode_Wait
```

- **`AINode_SpawnReinforcements`** (§3). Spawnea **2** copias de `EnemyToSpawn`
  una sola vez al cruzar **50%**. `EnemyToSpawn` es una referencia Odin a un
  `EnemyDataSO` (en el asset apunta al índice 0 de `ReferencedUnityObjects` — el
  enemigo ranged; hay que re-asignarlo en el Inspector al re-aplicar).

### Hijo 4 — Pool de ataque rotativo (`AINode_Alternate`, 2 patas)

```
AINode_Alternate  (rota determinísticamente A,B,A,B,... — 1 por turno)
├─ A) AINode_Sequence
│     1. AINode_ExecuteTelegraph                 (resuelve lo marcado el turno previo)
│     2. AINode_Selector [ AINode_Move , AINode_Wait ]   (chase; si no puede, espera)
│           Move: MaxSteps=3, TargetSelector_AlwaysPlayer,
│                 DesiredRange=1, Retreat=false, StopAdjacent=true
│     3. AINode_TelegraphMark  (ÁREA alrededor del boss)
│           Shape=SquareAroundSelf, Size=2, Damage=6, Kind=BasicAttack
│
└─ B) AINode_Sequence
      1. AINode_ExecuteTelegraph
      2. AINode_Selector [ AINode_Move , AINode_Wait ]   (mismo Move que A)
      3. AINode_TelegraphMark  (SLASH direccional hacia el jugador)
            Shape=DirectionalBand, Size=1, Depth=3, Damage=8, Kind=BasicAttack
```

- **`AINode_Alternate`** (`AINode_Alternate.cs`): rota entre `Children` uno por
  tick (0,1,0,1,...). Índice `[NonSerialized]` → arranca en 0 cada combate
  (copia runtime vía `EnemyDataSO.CreateRuntimeAIRoot`). A diferencia de
  `AINode_Random`, nunca repite antes de completar el ciclo.
- **`AINode_ExecuteTelegraph`** (`AINode_ExecuteTelegraph.cs`): primer hijo del
  sequence. Consume el área marcada el turno anterior: si el jugador sigue en una
  casilla marcada aplica el daño guardado vía `IDamagePipeline`; si esquivó, no
  hace daño. **Siempre devuelve Succeeded** (no es un gate). Apaga el overlay.
- **`AINode_Move`** (`AINode_Move.cs`): chase configurable. Aquí `DesiredRange=1`
  (se pega adyacente al jugador), `MaxSteps=3`, `Retreat=false`. Envuelto en un
  `Selector` con `Wait` para que, si ya está en rango o no hay mejor tile, no
  aborte el sequence.
- **`AINode_TelegraphMark`** (`AINode_TelegraphMark.cs`): marca el área para el
  próximo turno (**no** hace daño ese turno). Params clave: `Shape`, `Size`,
  `Depth`, `Count`, `HalfAxis`, `Damage`, `Kind`.

**Patrón telegraph → execute (2 turnos):** en el turno N el sequence corre
`ExecuteTelegraph` (resuelve lo del turno N-1), luego chase, luego `TelegraphMark`
(marca para N+1). Es el patrón de "aviso y golpe" clásico.

**Los dos ataques del ciclo:**

| Pata | Shape | Size | Depth | Damage | Kind | Forma resultante |
|---|---|---|---|---|---|---|
| A | `SquareAroundSelf` | 2 | — | **6** | BasicAttack | cuadrado 5×5 centrado en el **boss** |
| B | `DirectionalBand` | 1 | 3 | **8** | BasicAttack | banda de 3 de ancho, 3 de profundidad, del boss **hacia el jugador** (slash) |

### Hijo 5 — Dice-block a 15% HP (candadito) — PR #55

```
AINode_If (PcOwnerHpBelow Percent=0.15)
  Then → AINode_RotateBlock  Target=Dice, Count=1
  Else → AINode_Wait
```

- **NO** está envuelto en `AINode_Once`: se recalcula **cada turno** mientras el
  boss esté por debajo de **15%** HP. Detalle en §4.

**Fases resultantes por HP (descendente):**

| HP % | Evento |
|---|---|
| ≤ 70% | Se activa la lluvia de zonas (una vez). |
| ≤ 50% | Spawnea 2 refuerzos ranged (una vez). |
| ≤ 15% | Empieza a bloquear 1 dado al azar por turno (cada turno). |
| ≤ 10% | Fase 2: +2 Speed permanente + `OnBossPhaseChanged(2)` (una vez). |

En todo momento (todas las fases) corre el pool de ataque A/B alternado con chase.

---

## 2. Rain Hazard (lluvia de zonas dispersas)

Amenaza ambiental **independiente del boss** (fuente propia, nunca el GUID del
boss), inactiva hasta que el árbol la activa a 70% HP. Corre en paralelo a lo que
haga el boss.

Archivos:
- `Assets/Scripts/Rollgeon/Combat/Threat/RainHazardService.cs` (POCO,
  `IPreloadableService` + `IDisposable`).
- `Assets/Scripts/Rollgeon/Combat/Threat/RainHazardServiceBootstrap.cs` (wrapper SO).
- `Assets/Rollgeon/Services/RainHazardServiceBootstrap.asset`
  (guid `5e9a30023dcf2eb49bc59b5ae992e9e6`).
- `AINode_ActivateRainHazard.cs` (nodo que la activa).

Valores tuneados (constantes en `RainHazardService.cs`):

| Constante | Valor | Qué es |
|---|---|---|
| `CycleRounds` | **2** | Cada 2 rondas detona lo marcado el ciclo previo y re-marca. |
| `SquareCount` | **10** | Cantidad de cuadrados dispersos por ciclo. |
| `SquareSize` | **1** | Cada zona es 1×1. |
| `Damage` | **6** | Daño por zona (`AttackKind.Environmental`). |
| `RainSourceId` | `6c1f3a2e-7b4d-4a9e-9c3f-1a2b3c4d5e6f` | GUID fijo de la fuente (nunca el del boss). |
| `Priority` | 80 | Igual que `ThreatenedAreaService`. |

Mecánica: se suscribe a `OnTurnQueueBuilt`; cuando `roundIndex % CycleRounds == 0`
arma un `AIContext` a mano (con `SelfGuid = RainSourceId`) y reusa
`AINode_ExecuteTelegraph` + `AINode_TelegraphMark(Shape=ScatteredSquares,
Size=1, Count=10, Damage=6, Kind=Environmental)`. Cero lógica de telegraph
duplicada. Se limpia en `OnCombatEnd`/`OnRunEnd`. Las zonas caen al azar en el 50%
central de la sala, sin tocar paredes ni solaparse (ver §6, `ScatteredSquares`).

**Historia de tuning (commits):** empezó dentro de la rotación del boss
(`d2da6dcf`), pasó a hazard independiente activado a 70% (`68d2f019`, `7f911123`),
las zonas se achicaron a 1×1 y se dispersaron más (`a758cf97`), y el count subió
`4→6→10` (`57534a50`, `e551f85a`).

---

## 3. Refuerzos + fix de barra de HP del minion

Nodo: `AINode_SpawnReinforcements.cs`.

- Spawnea `Count` (=**2**) copias de `EnemyToSpawn` en **tiles del perímetro** de
  la sala (bounding box: X==min/max o Y==min/max), walkable y libres.
- **Lados distintos:** agrupa el perímetro en 4 lados (W/E/S/N), baraja el orden
  de lados y reparte, con separación Chebyshev mínima `MinSpawnSeparation = 3`
  entre refuerzos — así los 2 caen en lados distintos/opuestos y no apilados
  (commit `943380ae`).
- Los suma a la ronda en curso vía `TurnOrderService.Append(id)` — actúan recién
  cuando termina la ronda actual, y desde ahí rotan estable.
- Registra el runtime stats/AI del enemigo (`CreateRuntimeStats`,
  `CreateRuntimeAIRoot`) con `tier = 1`.

**Fix de barra de HP del minion (commit `c667cf47`):** los refuerzos spawnean a
full HP, pero la barra world-space es un widget que el caller debe inicializar.
Sin esto, la barra renderiza su default (0 HP) y nunca se bindea a los eventos de
daño. El nodo replica `DefaultEnemySpawnResolver`:

```csharp
if (visuals != null && visuals.TryGetPawn(id, out var pawn) && pawn.HealthBar != null)
{
    int maxHp = EnemyToSpawn.ResolveMaxHP(tier);
    pawn.HealthBar.Initialize(id, maxHp, maxHp);
}
```

> Al re-aplicar: si `develop` ya tiene un resolver de spawn distinto, verificar
> que la barra del minion se inicialice igual (este bloque o su equivalente).

---

## 4. Dice-block (candadito) — PR #55

Reemplaza a la regla repeat-combo (§5). El boss bloquea dados de la build del
jugador cuando está por debajo de 15% HP.

Archivos:
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_RotateBlock.cs`.
- `Assets/Scripts/Rollgeon/Combat/DiceBlock/IDiceBlockService.cs`.
- `Assets/Scripts/Rollgeon/Combat/DiceBlock/DiceBlockService.cs`.
- `Assets/Scripts/Rollgeon/Combat/DiceBlock/DiceBlockServiceBootstrap.cs`.
- `Assets/Rollgeon/Services/DiceBlockServiceBootstrap.asset`
  (guid `b4a6ff1e4f02ff84d968a7333a43c6f7`, **ya wireado** en ServiceBootstrap).

**`AINode_RotateBlock`** tiene 2 modos vía `enum BlockTarget { Dice, Combo }`:
- **`Dice`** (Boss 1, el que usamos): sortea `Count` dados **distintos** al azar
  de la build (Fisher-Yates parcial con el RNG del contexto) y los bloquea vía
  `IDiceBlockService.Block(index)`. Limpia (`dice.Clear()`) y re-sortea cada
  invocación. `bagSize` = `PlayerService.DiceBag.Dice.Count`.
- **`Combo`** (Boss 2, no usado acá): lee los últimos `Count` combos del
  `IComboLogService` y los prohíbe vía `IContractModifierService` (ventana
  deslizante). Un combo prohibido se muestra con daño 0 en el Contrato.

**Params en el asset del boss:** `Target = Dice (0)`, `Count = 1`.

**`DiceBlockService`** (`IDiceBlockService`): marca dados por **índice de slot**
(0..N-1, posicional y estable con `DiceBagSO`). Un dado bloqueado no entra a
ningún combo y no se puede re-rollear ese turno. **Auto-release** al finalizar el
turno del jugador (`OnTurnFinished` filtrado por player guid), y en
`OnCombatEnd`/`OnRunEnd`. Emite `EventName.OnDiceBlockChanged` en cada cambio
(este es el hook que la UI usa para dibujar el candadito sobre el slot).

**Decisión de diseño (timing):** el boss computa el bloqueo **al cerrar su
turno**; el jugador lo ve **al iniciar el suyo**.

**Tuning del trigger (commits):** el dice-block al principio se removió por
completo del piso 1 (`9eca7be6`), luego se re-introdujo bloqueando a bajo HP
(`9a083f56`) y finalmente el umbral se subió a **15%** "for better visibility"
(`627324e9`).

**UI del candadito:** el wiring del ícono de candado es lado-engine (escucha
`OnDiceBlockChanged` y consulta `IDiceBlockService.BlockedIndices`). Verificar en
la escena/prefab que el widget del dado suscriba ese evento.

---

## 5. Historia: regla "repeat-combo = 0 daño" (PR #46, REMOVIDA)

**Ya no existe en el código** — se removió en el commit `558b493c`
(*remove global repeat-combo-deals-zero rule*). Se documenta solo como historia
para que el equipo entienda la evolución del diseño.

- **Qué hacía:** repetir el mismo combo dos turnos seguidos ponía en 0 el daño
  del segundo golpe. La guarda vivía en
  `Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs`, en `Resolve()` y
  en `Preview()`, más un helper `IsRepeatOfPreviousCombo(...)` que comparaba
  contra `IComboLogService` (memoria de un paso: `LastCombo` / `Last(2)`).
- **Por qué se removió:** era **global** (afectaba también al jugador contra
  cualquier enemigo), **no tenía UI**, y en playtests aparecía como un bug
  confuso "el combo hace 0 daño" (ej. doble par `4-4-3-3` dos veces). Se agregó en
  `6095e2d6` (PR #46) y se reemplazó por el dice-block (§4).
- **Qué se conservó:** el registro de combos en `CombatHandoffService` y
  `ComboLogService` sigue intacto — alimenta el forbid-combo del boss de piso 2
  (`AINode_RotateBlock Target=Combo`) y el snapshot de resume. También se
  borraron el fixture `EffDealDamage_RepeatComboTests` y los casos repeat-combo de
  `DamagePipelineTests`.

> **Al re-aplicar: NO restaurar esta regla.** El diseño vigente es el dice-block.

---

## 6. Threat shapes de soporte

Enum `ThreatShape` en
`Assets/Scripts/Rollgeon/Combat/Threat/ThreatAreaShape.cs`. Valores (el índice
importa porque el asset serializa el int):

| Índice | Valor | Uso |
|---|---|---|
| 0 | `SquareAroundPlayer` | cuadrado centrado en el jugador |
| 1 | `Row` | franja horizontal |
| 2 | `Column` | franja vertical |
| 3 | `HalfRoom` | media sala |
| 4 | `DirectionalBand` | banda del boss hacia el jugador (slash) — **pata B** |
| 5 | `ScatteredSquares` | zonas dispersas — **rain** |
| 6 | `SquareAroundSelf` | cuadrado centrado en el **boss** — **pata A** |

Cómputo (todo en `ThreatAreaShape.cs`, código puro reusable):
- **`SquareAroundSelf`** (agregado en `84543c2c`): mismo math que
  `SquareAroundPlayer` pero centrado en la coord del boss. `Size` = radio
  (`2` ⇒ 5×5). El caller (`AINode_TelegraphMark`) resuelve `SelfGuid` en vez de
  `PlayerGuid`.
- **`DirectionalBand`** (`ComputeDirectionalBand`): sale del boss hacia el
  jugador en la dirección cardinal dominante; `Size` = half-width (`1` ⇒ 3 de
  ancho), `Depth` = profundidad (`3`).
- **`ScatteredSquares`** (`ComputeScatteredSquares`): `count` cuadrados de `w×w`
  anclados al azar en el **50% central** de la sala (margen del 25% por lado, no
  tocan paredes). Prioriza anclas separadas (gap 3→2→1→0, degradación en cascada)
  para que no se toquen ni solapen; último recurso permite solapar. Usado por el
  rain.

---

## 7. Wiring de bootstrap/servicios + dev console

**ServiceBootstrap** (`Assets/Rollgeon/ServiceBootstrap.asset`,
`ServiceBootstrapSO`): la lista `ExtraServices` registra los servicios via
`ServiceLocator`. Ambos bootstraps nuevos **ya están wireados** en esta rama:
- `RainHazardServiceBootstrap.asset` — guid `5e9a30023dcf2eb49bc59b5ae992e9e6`
  (commit `29990595`).
- `DiceBlockServiceBootstrap.asset` — guid `b4a6ff1e4f02ff84d968a7333a43c6f7`.

> **Trampa conocida** (commit `29990595`): si `RainHazardServiceBootstrap` NO está
> en `ExtraServices`, `AINode_ActivateRainHazard.Tick` devuelve `Failed`, y como
> está bajo el `AINode_Sequence` raíz (aborta al primer Failed), el boss **no
> hace nada, todo el turno**. Mismo riesgo con `DiceBlockServiceBootstrap` para el
> `AINode_RotateBlock` (loguea error y devuelve Failed, pero al estar en el hijo
> 5 con Else=Wait, el impacto es menor). **Verificar ambos GUIDs en
> `ServiceBootstrap.asset` después del merge.**

**Dev console — comando `boss`** (commit `257f5eb9`,
`Assets/Scripts/Rollgeon/DevConsole/Commands/Concrete/WorldCommands.cs`,
registrado en `DefaultCommands.cs`): teleporta directo a la sala `RoomType.Boss`
del piso actual (mismo patrón que `floor <n>`). Útil para testear el boss sin
jugar el piso entero.

---

## Re-application checklist

Después del merge develop-prioritized, re-aplicar en este orden. Marcar cada ítem.

### Servicios y wiring (C# + assets)
- [ ] Confirmar que existen los scripts: `RainHazardService.cs`,
      `RainHazardServiceBootstrap.cs`, `DiceBlockService.cs`,
      `IDiceBlockService.cs`, `DiceBlockServiceBootstrap.cs` (si el merge los
      borró, restaurarlos desde esta rama).
- [ ] Confirmar que existen los assets `RainHazardServiceBootstrap.asset` y
      `DiceBlockServiceBootstrap.asset` en `Assets/Rollgeon/Services/`.
- [ ] Verificar que **ambos GUIDs** (`5e9a3002...`, `b4a6ff1e...`) están en
      `ExtraServices` de `Assets/Rollgeon/ServiceBootstrap.asset`. Si no,
      agregarlos por Inspector.
- [ ] Confirmar los threat shapes `SquareAroundSelf`, `DirectionalBand`,
      `ScatteredSquares` en `ThreatAreaShape.cs` (y los valores de enum en el
      mismo orden — el asset serializa el int).

### Árbol de AI del boss (editor de AI-tree / Inspector de `ED_Boss_Sunken_Grand.asset`)
- [ ] `AIRoot = AINode_Sequence` con 5 hijos, en este orden.
- [ ] Hijo 1: `If(TargetSelector_Self, PcOwnerHpBelow Percent=0.10) → Once →
      ApplyStatModifier(AttackDelta=0, SpeedDelta=2, PhaseIndex=2,
      EmitPhaseChangedEvent=true)`, Else=Wait.
- [ ] Hijo 2: `If(PcOwnerHpBelow Percent=0.70) → Once → ActivateRainHazard`,
      Else=Wait.
- [ ] Hijo 3: `If(PcOwnerHpBelow Percent=0.50) → Once →
      SpawnReinforcements(EnemyToSpawn=<enemigo ranged>, Count=2)`, Else=Wait.
      **Re-asignar `EnemyToSpawn` a mano en el Inspector** (referencia a
      `EnemyDataSO`).
- [ ] Hijo 4: `AINode_Alternate` con 2 patas:
  - [ ] A: `Sequence[ ExecuteTelegraph, Selector[Move(MaxSteps=3,
        TargetSelector_AlwaysPlayer, DesiredRange=1, Retreat=false,
        StopAdjacent=true), Wait], TelegraphMark(Shape=SquareAroundSelf, Size=2,
        Damage=6, Kind=BasicAttack) ]`.
  - [ ] B: `Sequence[ ExecuteTelegraph, Selector[Move(igual que A), Wait],
        TelegraphMark(Shape=DirectionalBand, Size=1, Depth=3, Damage=8,
        Kind=BasicAttack) ]`.
- [ ] Hijo 5: `If(PcOwnerHpBelow Percent=0.15) → RotateBlock(Target=Dice,
      Count=1)`, Else=Wait. **Sin `Once`** (corre cada turno bajo 15%).

### Compatibilidad con develop (NO pisar)
- [ ] No revertir anim-by-manhattan-range, windup-sync, overlay-durante-windup,
      face-target, ni el fix de freeze (ver aviso arriba).
- [ ] Verificar que el patrón telegraph→execute del árbol nuestro sigue
      sincronizado con el windup-sync de develop (`312c5bec`); adaptar el árbol si
      hay conflicto, priorizando develop.
- [ ] Confirmar que la barra de HP del minion se inicializa (bloque
      `HealthBar.Initialize` en `AINode_SpawnReinforcements` o su equivalente en
      el resolver de spawn de develop).

### Lo que NO se re-aplica
- [ ] **NO** restaurar la regla repeat-combo-deals-zero en `DamagePipeline` (fue
      removida a propósito; el reemplazo es el dice-block).

### Verificación
- [ ] Dev console: comando `boss` presente y funcional.
- [ ] Playtest: bajar el boss por los 4 umbrales (70/50/15/10%) y confirmar rain,
      refuerzos, candadito y buff de speed. Suite EditMode en verde.
