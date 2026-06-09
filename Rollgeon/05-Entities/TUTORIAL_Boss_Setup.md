# TUTORIAL — Armar los 3 Bosses (The Sunken Grand)

Guía de wiring en engine para los Bosses sobre la infraestructura de
`feat(combat): add boss prerequisite combat systems`. Los **agents solo
escriben C#**; este doc es para armar SOs / AI trees / prefabs a mano.

> Prereq: los 4 bootstraps ya están en `ServiceBootstrap.ExtraServices`
> (`ThreatenedAreaServiceBootstrap`, `ComboLogServiceBootstrap`,
> `DiceBlockServiceBootstrap`, `ContractModifierServiceBootstrap`).

---

## 0. Gotcha de control de flujo (leer antes de armar el árbol)

- **`Sequence`** corta en el primer hijo que devuelve `Failed`.
- **`If`** devuelve `Failed` si la rama elegida (`Then`/`Else`) es **null**.
  → Todo `If` "de efecto" dentro de un `Sequence` necesita un `Else` real:
  poné un **`Wait`** en el `Else` para que no aborte la secuencia.
- **`Selector`** devuelve `Failed` si todos los hijos fallan.
  → El pool de acción siempre debe terminar en un fallback que tenga éxito
  (`Move` o `Wait`).

Los nodos de inicio/fin de turno (`ExecuteTelegraph`, `PromulgateRule`,
`RotateBlock`, `ApplyStatModifier`, `Wait`) devuelven `Succeeded`, así que
encadenan bien en el `Sequence` raíz.

---

## 1. Candado del dado bloqueado (UI, Boss 1)

En el **prefab del slot de dado** (el GameObject con `DiceSlotView`):

1. Agregá un hijo UI `Image` llamado `LockIcon` con el sprite de candado.
   Dejalo activo o inactivo — `SetBlocked` controla `SetActive`.
2. En el componente `DiceSlotView`, sección **"Boss 1 — Dice block"**,
   arrastrá ese GameObject al campo **`Lock Icon`**.
3. Confirmá que `Background` (el `Graphic` del slot) esté asignado: se usa
   para el tinte gris al bloquear.

No hace falta nada más. `DiceBlockService` + `DiceZoneView` llaman a
`SetBlocked(true/false)` solos: el dado bloqueado se ve gris + candado, no
se puede holdear, queda fuera del combo y no se re-rollea.

---

## 2. Estructura de turno común a los 3 Bosses

El **root** de cada boss es un `Sequence` con este orden:

```
Sequence (root)
├─ 1. ExecuteTelegraph                     ← resuelve el telegráfico de N-1 (siempre primero)
├─ 2. [Boss 3] PromulgateRule              ← promulga regla al inicio del turno
├─ 3. If (PcOwnerHpBelow umbral)           ← setup de Fase 2, UNA sola vez
│      Then → Once → ApplyStatModifier
│      Else → Wait                         ← OBLIGATORIO (sino aborta el Sequence)
├─ 4. Selector (pool de acción)            ← melee / telegráfico / rango / move
│      └─ ... (fallback Move/Wait al final)
└─ 5. [Boss 1 y 2] If (PcOwnerHpBelow umbral)  ← rotación del bloqueo (fin de turno)
       Then → RotateBlock (Count=2)
       Else → RotateBlock (Count=1)
```

- El **telegráfico** es un ciclo de 2 turnos: en el turno N el boss elige
  `TelegraphMark` (paso 4), en N+1 `ExecuteTelegraph` (paso 1) lo ejecuta.
- La **rotación va al final** para que el bloqueo quede fresco para el
  próximo turno del jugador (decisión: el boss computa al cerrar su turno).
- `PcOwnerHpBelow.Percent` es ratio 0..1 (0.10 = 10%).

---

## 3. Boss 1 — Contador de Pisos (Bloqueo de Dados)

**EnemyDataSO** (Enemy Editor → pestaña Enemy Data):
`BaseHP`=hpMaximo, `BaseAttack`=fuerzaAtaque, `BaseSpeed`=velocidad, +VisualPrefab.

| Stat del ticket | Dónde vive |
|---|---|
| dadosBloqueadosBase = 1 | `RotateBlock.Count` (rama Else, Fase 1) |
| dadosBloqueadosCritico = 2 | `RotateBlock.Count` (rama Then, Fase 2) |
| umbralFaseCritica = 10% | `PcOwnerHpBelow.Percent = 0.10` |
| radioAreaTelegrafico = 3×3 | `TelegraphMark.Size = 1` (radio 1 ⇒ 3×3) |
| danioAreaTelegrafico | `TelegraphMark.Damage` |

**AI Tree:**

```
Sequence
├─ ExecuteTelegraph
├─ If (PcOwnerHpBelow 0.10)
│    Then → Once → ApplyStatModifier { SpeedDelta = +N, PhaseIndex = 2, EmitPhaseChangedEvent = true }
│    Else → Wait
├─ Selector (pool)
│    ├─ If (distancia ≤ 1) Then → Behavior: Ataque melee
│    ├─ TelegraphMark { Shape = SquareAroundPlayer, Size = 1, Damage = danioArea, Kind = BasicAttack }
│    ├─ If (distancia > 1) Then → Behavior: Ataque rango
│    └─ Move (fallback)
└─ If (PcOwnerHpBelow 0.10)
     Then → RotateBlock { Target = Dice, Count = 2 }
     Else → RotateBlock { Target = Dice, Count = 1 }
```

> El pool (melee/rango/move + condición de distancia) es el mismo patrón que
> ya usás para enemigos normales; sólo se agrega `TelegraphMark` como opción.
> Para que no telegrafíe todos los turnos, podés meter el pool en un
> `Random` ponderado en vez de `Selector`, o gatearlo por turno.

---

## 4. Boss 2 — Jefe de Seguridad (Memoria de Combos)

**EnemyDataSO:** igual que Boss 1.

| Stat del ticket | Dónde vive |
|---|---|
| combosRecordadosBase = 1 | `RotateBlock.Count` (Else, Fase 1) |
| combosRecordadosCritico = 2 | `RotateBlock.Count` (Then, Fase 2) |
| umbralFaseCritica = 20% | `PcOwnerHpBelow.Percent = 0.20` |
| anchoFranjaTelegrafico | `TelegraphMark.Size` (1 = línea del jugador) |
| danioAreaTelegrafico | `TelegraphMark.Damage` |

El `RotateBlock { Target = Combo }` lee solo el `IComboLogService` y bloquea
los últimos `Count` combos vía `IComboBlockService` con `ComboBlockDuration = 1`
(ventana deslizante turno a turno). El log se escribe solo al resolver el
ataque del jugador.

**AI Tree:**

```
Sequence
├─ ExecuteTelegraph
├─ If (PcOwnerHpBelow 0.20)
│    Then → Once → ApplyStatModifier { AttackDelta = 0, SpeedDelta = 0, PhaseIndex = 2, EmitPhaseChangedEvent = true }
│    Else → Wait                       ← (solo dispara el feedback de Fase 2; sin cambio de stat)
├─ Selector (pool)
│    ├─ If (distancia ≤ 1) Then → Behavior: Ataque melee
│    ├─ If (PcOwnerHpBelow 0.20)        ← franja más ancha en Fase 2
│    │    Then → TelegraphMark { Shape = Row, Size = anchoFase2, Damage = danioArea }
│    │    Else → TelegraphMark { Shape = Row, Size = anchoFase1, Damage = danioArea }
│    ├─ If (distancia > 1) Then → Behavior: Ataque rango
│    └─ Move (fallback)
└─ If (PcOwnerHpBelow 0.20)
     Then → RotateBlock { Target = Combo, Count = 2, ComboBlockDuration = 1 }
     Else → RotateBlock { Target = Combo, Count = 1, ComboBlockDuration = 1 }
```

> `Shape = Row` = franja horizontal; usá `Column` si querés vertical.
> El combo bloqueado aparece tachado en el Contrato (la UI ya lo lee vía
> `IComboBlockService` en `ContractSheet.MatchBest`).

---

## 5. Boss 3 — Director General (Reglas Variables)

**EnemyDataSO:** igual que los otros. Boss 3 **no** usa `RotateBlock`.

| Stat del ticket | Dónde vive |
|---|---|
| intervaloCambioReglaFase1 = 3 | `PromulgateRule.IntervalPhase1` |
| intervaloCambioReglaFase2 = 2 | `PromulgateRule.IntervalPhase2` |
| cantidadReglasSimultaneas = 1 | `PromulgateRule.RulesPerPromulgation` |
| umbralFase2 = 50% | `PromulgateRule.Phase2HpThreshold = 0.5` |
| listaReglasActivas R01–R06 | `PromulgateRule.EnabledRules` |
| danioAreaTelegrafico | `TelegraphMark.Damage` |

`PromulgateRule` maneja la Fase 2 **internamente** (acorta el intervalo al
cruzar `Phase2HpThreshold` leyendo su propia vida), así que no necesitás un
`If` para el intervalo. R01 usa `DoubleFactor` (×2) y R02 `HalfFactor` (×0.5).

**AI Tree:**

```
Sequence
├─ ExecuteTelegraph
├─ PromulgateRule {
│     EnabledRules = [R01, R02, R03, R04, R05, R06],
│     RulesPerPromulgation = 1,
│     IntervalPhase1 = 3, IntervalPhase2 = 2,
│     Phase2HpThreshold = 0.5,
│     DoubleFactor = 2, HalfFactor = 0.5 }
├─ If (PcOwnerHpBelow 0.50)
│    Then → Once → ApplyStatModifier { PhaseIndex = 2, EmitPhaseChangedEvent = true }   ← feedback "decreto urgente"
│    Else → Wait
└─ Selector (pool)
     ├─ If (distancia ≤ 1) Then → Behavior: Ataque melee
     ├─ TelegraphMark { Shape = HalfRoom, HalfAxis = Vertical, Damage = danioArea }
     ├─ If (distancia > 1) Then → Behavior: Ataque rango
     └─ Move (fallback)
```

> La regla activa se refleja en tiempo real en la tabla del Contrato
> (`ContractDisplayView` re-lee al recibir `OnContractModifierChanged`):
> los valores buffeados salen en verde, los nerfeados/prohibidos en rojo.
> `HalfRoom` marca la mitad de la sala donde está el jugador; `HalfAxis`
> elige el eje del corte (Vertical = izq/der, Horizontal = abajo/arriba).

---

## 6. Eventos para VFX / diálogo (los wireás vos)

Los sistemas solo emiten; enganchá tus feedbacks a:

| Evento | Args | Uso |
|---|---|---|
| `OnThreatenedAreaMarked` | `[Guid source]` | SFX/anim de advertencia al marcar |
| `OnThreatenedAreaResolved` | `[Guid source, bool hit]` | impacto / esquive del telegráfico |
| `OnDiceBlockChanged` | `[Guid player]` | refrescar UI de dados (ya lo hace `DiceZoneView`) |
| `OnContractModifierChanged` | `[]` | refrescar Contrato (ya lo hace `ContractDisplayView`) |
| `OnBossPhaseChanged` | `[Guid boss, int phaseIndex]` | animación + cambio de color + línea de diálogo de Fase 2 |

El `OnBossPhaseChanged` lo dispara `ApplyStatModifier` (paso 3 del árbol) una
sola vez al cruzar el umbral, aunque no cambie stats (`EmitPhaseChangedEvent = true`).
