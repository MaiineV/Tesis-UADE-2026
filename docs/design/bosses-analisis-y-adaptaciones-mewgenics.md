# Bosses — análisis del estado actual + adaptaciones desde Mewgenics

> **Fecha:** 2026-08-11. **Rama:** `develop` (`99e4c94b`).
> **Método:** parte 1 leída de los `.asset` reales (`ED_Boss_*.asset`, Odin
> `SerializationNodes`) y del código de los nodos, **no** de docs previos —
> `docs/design/boss1-feature-branch-behavior.md` quedó desactualizado y las
> diferencias están marcadas. Parte 2 relevada del wiki de Mewgenics (32 bosses).
> **Estado:** diagnóstico + propuestas para mesa de diseño. Nada implementado.

---

## 0. La restricción que ordena todo el documento

Mewgenics es un **party tactics**: llevás 3+ gatos desechables, y la mitad de su
diseño de bosses se apoya en eso — comerte una unidad, poseerla, charmearla,
clonarte el equipo, matar a uno para enfurecer al otro. **Nosotros tenemos un
solo personaje.** Todo mecanismo cuyo costo es "perdés una unidad" no traduce:
en Rollgeon "perdés la unidad" = perdiste la run.

Lo que **sí** traduce es la otra mitad, la que juega contra *el tablero* y contra
*los recursos del jugador*: objetos rompibles en la sala, ciclos de ataque
legibles, órdenes que redefinen dónde es seguro pararse, y bosses que reaccionan
a lo que hacés. Casualmente es la mitad que encaja con el problema abierto en
`docs/design/reposicionamiento-peso-tactico.md` (moverse cuesta energía y no paga
nada) y con la identidad de casino.

Ese es el filtro que aplico en la §5.

---

## 1. El vocabulario que ya tenemos (el presupuesto real)

Antes de proponer, el inventario de lo que se puede armar **sin escribir C#**,
sólo cableando el árbol de IA en el Inspector:

**Control de flujo:** `Sequence` (AND, aborta al primer `Failed`), `Selector`
(OR, corta al primer `Succeeded`), `If` (PreConditions AND-evaluadas, devuelve el
resultado *de la rama*), `While`, `Random` (weighted), `Alternate`
(round-robin determinista, índice `[NonSerialized]` → resetea por combate),
`Once`, `Wait` (no-op que siempre devuelve `Succeeded`).

**Acciones:** `Move` (chase con `DesiredRange`/`Retreat`), `KeepDistance`
(kiting), `Behavior` (un `EnemyActionBehavior` = ataque/heal/buff vía `EffectData`),
`TelegraphMark` + `ExecuteTelegraph` (marcar ahora, detonar el turno siguiente),
`ActivateHazard` (data-driven, `HazardDefinitionSO`), `SpawnReinforcements`
(`Count` copias de un `EnemyDataSO` en el perímetro, con `RespawnDelayTurns`),
`ApplyStatModifier` (Attack/Speed deltas permanentes + `PhaseIndex` + evento),
`RotateBlock` (bloquear dados o prohibir combos), `PromulgateRule` (reescribir el
Contrato).

**Formas de amenaza** (`ThreatShape`, el índice se serializa — **appendear al
final, nunca insertar**): `0 SquareAroundPlayer`, `1 Row`, `2 Column`,
`3 HalfRoom`, `4 DirectionalBand`, `5 ScatteredSquares`, `6 SquareAroundSelf`.

**PreConditions útiles para gatear:** `PcOwnerHpBelow` (%), `PcTargetInRange`,
`PcRoundNumber` (incluye `Multiple` → cadencia gratis), `PcAllyAliveExists`,
`PcAllyBelowMaxExists`, `PcChance`, `PcCurrentPhase`, `PCComboAvailable`,
`PcNoComboThisRoll`, `PCHasModifier`, `PcOwnerStatCompare`, `PcGoldCompare`,
`PCFirstRollOfCombat`, `PCAdjacentToDoor`, `PCEntityInRange`.

**Servicios de presión al jugador:** `IDiceBlockService` (bloquea dados por
índice de slot, auto-release al cerrar el turno del jugador),
`IContractModifierService` (`MultiplyCombo`, `ForbidCombo`, `SetComboToNeighbor`,
`ClearAll`), `IComboLogService` (memoria de combos jugados).

**Lo que NO existe** (confirmado en código): knockback/desplazamiento forzado del
target — `EffApplyImpulse` sólo escribe un `Vector3` en el bag del behavior para
una capa de feedback que todavía no aterrizó (§10), y `EffMove` mueve al *source*,
no al target; tiles con estado; props interactivos; daño por turno
(burn/poison/bleed); y **una entidad no puede tener dos turnos en la misma ronda**
(`TurnOrderService.Append` deduplica por GUID —
`Append_DuplicateGuid_IsNoOp`).

---

## 2. Los tres bosses, leídos del asset

Los tres comparten `BaseHP 200`, `MaxEnergy 3`, `BaseAttackRange 1` y
`ExtraTiers` vacío. Difieren en `BaseAttack` (20 / 30 / 40) y gold (15-23 /
30-60 / 60-80).

### 2.1 Boss 1 — The Sunken Grand (`boss.sunken_grand`, Attack 20)

`AIRoot = Sequence` con **7 hijos**, en orden:

| # | Nodo | Qué hace |
|---|---|---|
| 1 | `ExecuteTelegraph` (`WindupFeedbackId: anim.enemy.sunken_grand.range`, `ImpactEventKey: hit`) | detona el área marcada el turno anterior |
| 2 | `Selector[ KeepDistance(MaxSteps 3, IdealDistance 5), Wait ]` | **kitea a distancia 5** |
| 3 | `If(Self, HpBelow 10%) → Once → ApplyStatModifier(Speed +2, Phase 2, emit)` | fase 2 |
| 4 | `Selector[ If(HpBelow 85%) → Once → ActivateHazard(RainHazardDefinition), Wait ]` | lluvia |
| 5 | `Selector[ If(HpBelow 65%) → Once → SpawnReinforcements(ED_RangedEnemy, Count 2, RespawnDelayTurns 2), Wait ]` | refuerzos |
| 6 | `Selector[ Alternate[ A, B ], Wait ]` | pool de ataque (abajo) |
| 7 | `RotateBlock(Dice, Count 1)` | **bloquea 1 dado, cada turno, sin gate de HP** |

Patas del `Alternate` (cada una es un `Sequence`):

| Pata | Contenido |
|---|---|
| A | `ExecuteTelegraph` → `Selector[Move(3, player, DesiredRange 1), Wait]` → `TelegraphMark(SquareAroundSelf, Size 2, Depth 2, Count 3, **Damage 40**)` |
| B | `ExecuteTelegraph` → `Selector[Move(idem), Wait]` → `TelegraphMark(DirectionalBand, Size 1, Depth 3, Count 3, **Damage 80**)` |

Hazard: `RainHazardDefinition.asset` — `ScatteredSquares`, 10 zonas de 1×1,
6 de daño `Environmental`, cada 2 rondas.

**Diferencias con `boss1-feature-branch-behavior.md`** (ese doc ya no describe el
asset): los umbrales pasaron de 70/50 → **85/65**; el dice-block **perdió el gate
de 15% HP** y ahora corre desde el turno 1; el rain pasó de `ActivateRainHazard`
al `ActivateHazard` genérico + SO; y apareció el `KeepDistance` del hijo 2.

### 2.2 Boss 2 — Security Boss (`boss.security_boss`, Attack 30)

`AIRoot = Sequence` con 4 hijos:

1. `ExecuteTelegraph`
2. `If(Self, HpBelow 20%) → Once → ApplyStatModifier(**Attack 0, Speed 0**, Phase 2, emit)` — o sea: la "fase 2" acá **no cambia ningún stat**, sólo emite el evento.
3. `Selector[ If(PcTargetInRange 1) → Behavior "Melee" ; Else → Move(3, player, range 1) , Random[ If(HpBelow 20%) → TelegraphMark(Row, Size 3, **Damage 100**) ; Else → TelegraphMark(Row, Size 1, **Damage 70**) ] ]`
4. `If(HpBelow 20%) → RotateBlock(Combo, 2) ; Else → RotateBlock(Combo, 1)`

El `Behavior "Melee"` es `EffDealDamage` con `_baseAmount 10` + `ReadEntityStat(Self, Attack, UseModified)` ⇒ **~40 de daño melee**.

### 2.3 Boss 3 — General Director (`boss.general_director`, Attack 40)

`AIRoot = Sequence` con 4 hijos:

1. `ExecuteTelegraph`
2. `PromulgateRule` — `EnabledRules` = las 6 (R01-R06), `RulesPerPromulgation 1`, `IntervalPhase1 2`, `IntervalPhase2 1`, `Phase2HpThreshold 0.5`, `DoubleFactor 2`, `HalfFactor 0.5`
3. `If(HpBelow 50%) → Once → ApplyStatModifier(**Attack 0, Speed 0**, Phase 2, emit)` — igual que B2: cosmético
4. `Selector[ If(PcTargetInRange 1) → Behavior(**Behavior = null**) ; Else → Move(3, range 1) , Random[ TelegraphMark(HalfRoom, **Damage 100**) , Move(3, range 1) ] ]`

Las 6 reglas (`AINode_PromulgateRule.ContractRule`): R01 duplica el daño de un
combo random, R02 lo reduce a la mitad, R03 lo prohíbe (daño 0), R04 sube el
combo de menor base al inmediatamente superior, R05 baja el de mayor base al
inferior, R06 no hace nada. Cada promulgación hace `ClearAll()` primero.

### 2.4 La identidad real de los tres

| | Movimiento | Amenaza espacial | Presión al build | Compañía |
|---|---|---|---|---|
| **B1** | kitea a 5 **y** persigue a 1 (los dos, el mismo turno) | 5×5 alrededor suyo (40) / banda 3×3 hacia vos (80) + lluvia 10×1×1 (6) | bloquea 1 dado por turno | 2 ranged a 65% HP |
| **B2** | se acerca | franja Row (70 / 100 bajo 20%) | prohíbe tus últimos 1-2 combos | — |
| **B3** | se acerca (o se acerca de nuevo) | media sala (100) | reescribe el Contrato cada 2 turnos (1 cada turno bajo 50%) | — |

La lectura de diseño es sólida y **ya está diferenciada**: cada boss ataca una
capa distinta del jugador (los dados / los combos jugados / la tabla de combos).
Eso es más de lo que tiene la mayoría de los juegos del género en su primera
pasada. El problema no es la idea, es la **ejecución del árbol** (§3).

---

## 3. Hallazgos de código — cosas a decidir antes de sumar contenido

Ordenados por impacto. Los 4 primeros son, a mi lectura, bugs; los otros son
decisiones de balance/diseño que hay que confirmar. **No toqué nada.**

**H1 — El telegraph de B2 casi nunca dispara.** `AINode_If` devuelve el
resultado *de la rama que ejecuta* (`AINode_If.cs:46`). En el `Selector` del hijo
3, la rama `Then` (melee) devuelve `Succeeded` siempre
(`AINode_Behavior.cs:42`) y la rama `Else` (`Move`) devuelve `Succeeded` si se
movió. En ambos casos el `Selector` **corta ahí** y el `Random[TelegraphMark]`
nunca corre. El ataque telegrafiado de 70/100 sólo sale cuando el boss **no
puede moverse** (ya está en la banda o no hay tile mejor). Es el ataque
característico del boss y es prácticamente código muerto.

**H2 — B3 no pega nunca cuerpo a cuerpo, y su único ataque sólo se arma si estás
pegado.** Su `AINode_Behavior` tiene `Behavior = null` ⇒ devuelve `Failed`
(`AINode_Behavior.cs:26`). Efecto en cascada: cuando estás adyacente el `If`
falla, el `Selector` pasa al `Random`, y ahí sale `TelegraphMark(HalfRoom, 100)`
o un `Move` (50/50). Cuando **no** estás adyacente, el `Move` del `Else` tiene
éxito y el `Selector` corta ⇒ **no telegrafía nada**. Resultado: si el jugador se
queda lejos, el boss de piso 3 sólo promulga reglas y camina. Nunca hace daño.

**H3 — B1 se pelea consigo mismo por el movimiento.** El hijo 2 kitea hasta
distancia 5, y después en el mismo turno la pata del `Alternate` corre
`Move(DesiredRange 1)` que lo trae 3 casillas de vuelta. Dos nodos de movimiento
por turno con objetivos opuestos: se va y vuelve. Se ve como jitter y le saca
legibilidad al kiteo, que es su identidad declarada (el actuario que no te deja
acercarte).

**H4 — El dice-block de B1 perdió su gate.** El hijo 7 es un `RotateBlock(Dice,1)`
suelto, sin `If(PcOwnerHpBelow)`. Bloquea 1 dado **desde el turno 1 y para
siempre**. El doc viejo dice 15% "for better visibility" (commit `627324e9`).
¿Fue una decisión nueva o se perdió el `If` en un merge? Cambia mucho la
experiencia del piso 1.

**H5 — Los `ExecuteTelegraph` están duplicados en B1.** El hijo 1 tiene el
`WindupFeedbackId` y el `ImpactEventKey` cableados; las dos patas del `Alternate`
tienen otro `ExecuteTelegraph` **sin** esos ids. Como el hijo 1 corre primero y
limpia lo pendiente, los anidados quedan no-op — pero si alguna vez se reordena
el árbol, el que dispare va a ser el silencioso (sin anim ni sonido de impacto).
Candidatos a borrar.

**H6 — "Fase 2" es un evento vacío en B2 y B3.** Los dos `ApplyStatModifier`
tienen `AttackDelta 0` y `SpeedDelta 0`. Lo único que cambia realmente de fase es
el `Count` del `RotateBlock` (B2: 1→2) y el intervalo de `PromulgateRule` (B3:
2→1). Si la intención es que la fase 2 *se sienta*, hoy no tiene stats detrás.

**H7 — Escala de daño de los telegraphs vs HP del jugador.** 40 y 80 (B1), 70 y
100 (B2), 100 (B3), más ~40 de melee en B2. `CH_Warrior.BaseMaxHp` es 10 y el HP
efectivo observado en piso 1 ronda 50-63
(`docs/design/reposicionamiento-peso-tactico.md §1.1`). Un `DirectionalBand` de
80 es one-shot. Esto es tema de la mesa con Bocco, no algo a tocar de acá.

**H8 — HP plano 200/200/200** y `ExtraTiers` vacío en los tres — ya está
levantado en `docs/planning/balance-modelo-3-pisos.md §3`, lo repito porque
cualquier boss nuevo hereda el mismo problema de curva.

---

## 4. Mewgenics — catálogo de mecánicas, agrupado por patrón

Relevé los 32 bosses del wiki. Agrupo por **patrón de diseño** en vez de por boss,
porque lo que nos sirve es el mecanismo, no el gato.

**P1 — Objeto en el tablero que el jugador puede atacar, y que cambia lo que hace
el boss.** El patrón más repetido del juego y el más adaptable.
*Gambit* pone un **d6 en el suelo**: cada ronda lo tira, y al final de la ronda
salta encima y ejecuta el ataque correspondiente a la cara (1 confusión → 2 poop
→ 3 láser en línea, 5 dmg → 4 terremoto diagonal con knockback → 5 tormenta en
diamante 7×7 con stun → 6 explosiones por todo el tablero, 10 dmg + 3 burn).
**Pegarle al dado lo re-rollea**, y si el dado desaparece antes de su último
turno el ataque "fizzlea" y el boss se daña a sí mismo. Con 1-3 el boss intenta
reposicionar el dado; con 4+ te ataca a vos.
*The Coven* pelea en un pentagrama con **5 velas de 50 HP**; cada vela que quede
prendida al final del ritual le da una runa distinta al demonio de la fase 2
(turno extra, spawns, knockback, +movimiento, +daño de fuego). Apagarlas con agua
o romperlas con un pico es *la* estrategia.
*Radical Rat* tira **bombas de 1 HP** que detonan en cruz al final de la ronda;
podés desactivarlas pegándoles.
*Spewer* tiene dos **tubos de pastillas** indestructibles-salvo-herramientas que
le dan formas distintas (creep/tar/lava) al comerlas.
*The Man in the Moon* es cabeza + **dos manos de 100 HP**; pegarle a una mano por
atrás la hace embestir la cabeza y romperle el Brace.
*The Creator* lleva un **escudo con 6 etapas** (140 de daño total) que si lo
rompés adelanta la fase.

**P2 — Ciclo de ataques fijo y legible.** El boss no elige al azar: rota, y la
pose te dice qué viene.
*The Coven* corre un ritual de 5 turnos siempre igual (Rise → Pestilence →
Famine → War → Death). *Crater Maker* cebca uno de 4 ataques y **te lo
telegrafía con los tentáculos de la cabeza** (3 extendidos = Howl, 2 = Consume…)
— y **cambia de ataque cebado cada vez que le pegás**. *C-800* rota
escopeta → francotirador → subfusil. *Johnny* cicla 4 hechizos psíquicos en orden
fijo. *Guillotina* actúa 3 veces por turno con un set fijo.

**P3 — Órdenes que redefinen dónde es seguro pararse.** El *Throbbing King* no
apunta a tu unidad: **dicta una regla espacial** y después llena de tentáculos
todo lo que no la cumple (16 de daño): **Roulette** (todo el tablero excepto 7-9
casillas seguras al azar), **Kneel** (todo excepto lo adyacente al rey),
**Spread Out** (2 casillas alrededor de cada gato, y **te sigue** si te movés),
**Go Away** (todo lo que no sea borde). Es inmune a stun mientras dicta órdenes.
En la fase final, *Soahc* re-usa "Tuo Daerps" (Spread Out invertido).

**P4 — IA reactiva: el boss responde a lo que hacés, no a dónde estás.**
*Zodiac* "le dispara a todo lo que se mueve" y tiene **6 balas**; recarga sólo
cuando se queda sin munición — quedarte quieto es defensa válida y forzarle tiros
con señuelos es la estrategia. *Boris* se mueve 1 casilla hacia **lo último que
lo dañó**. *C-1000* se mueve cada vez que **cualquier** unidad actúa, incluso
stuneado. *C-800* se acerca cuando lanzás un hechizo. *Spinnerette* se
**enfurece si le pegás por la espalda** (3 turnos de +daño/+velocidad, y no puede
huir al techo). *Spewer* y *Chubs* contraatacan **al recibir daño**.

**P5 — Contadores visibles en vez de (o además de) HP.** *Queen Hippo* arranca
con **Brace 4** y pierde una stack por ronda ganando movimiento: cuando se le
acaban, **muere sola** — y si le sacás todas las stacks de golpe con un
dispel, se muere en el acto ("tiene problemas del corazón"). *C-800* tiene **50
stacks de Brace** que hay que picar antes de hacer daño real, y se ve cada vez
más robótico. *The Child* cicla tres estados con **cuenta regresiva desde 7**.

**P6 — Transiciones de fase que cuestan caro.** *Guillotina* al bajar de 200 HP
**se cura 50, gana 100 de HP máximo y se limpia todos los debuffs**. *Hitler II*
se cura entera al pasar a mecha. *Soahc* limpia debuffs en **cada**
transformación (13 formas) — lo que vuelve inútil todo el daño por turno.
Lección de diseño: la transición es un *reset* de la presión acumulada, no sólo
un cambio de ataques.

**P7 — El boss no es tu único problema.** *Pyrophina vs Zaratana*: dos kaijus de
1000 HP que **se pelean entre ellos y te ignoran**; el peligro es el daño
colateral, y cuando uno muere el otro se vuelve inmortal y termina la pelea.
*Chubs & Nubs*: dos perros ciegos que se mueven al azar; **cuando uno muere, el
otro se enfurece** (y Nubs se autodestruye por 50 en línea de visión).
*Lord Bunga* mira desde el trono mientras pelean sus dos campeones.
*The Creator* es inmune y **te hace pelear contra clones de tu propio equipo**.

**P8 — Deshabilitar al jugador en vez de dañarlo.** *Guillotina* te **inhala** y
tu único ataque pasa a ser "Flail"; *The Mother* te **captura** con un segmento de
tumor y tu ataque pasa a "Thrash"; *Spinnerette* te **entela** y tu ataque pasa a
"Break Free"; *Man in the Moon* te come o te agarra con la mano.
El patrón compartido: **te reemplazan el set de acciones por una sola acción de
zafar**, y la salida existe (romper el segmento, pegarle a la mano por atrás).

**P9 — El boss fabrica el terreno.** Lava/creep/tar/hielo/telarañas/tierra según
el boss; *The Coven* dispara 20 proyectiles al azar que dejan lava; *Dreadnoughtus*
deja tierra al pisar; *Pyrophina* prende los bordes del mapa.

**P10 — Bosses multi-parte.** *Man in the Moon* (cabeza + 2 manos),
*Dreadnoughtus* (4 patas de 55 HP; con 2 caídas la cabeza se estrella, recibe 100
de daño y queda **stuneada una ronda** = ventana de daño), *Guillotina 2/3*
(cabeza y cuerpo separados), *The Mother* (3×3 que se **expande** por el tablero
spawneando segmentos de 5 HP; los desconectados mueren solos).

**P11 — Copiar / transformarse.** *C-1000* arranca con **100% de dodge** que baja
10% por movimiento, y **copia a un gato adyacente** con sus habilidades, ítems y
stats. *Stacy Mutant* es un boss cuyo loadout **lo elige el jugador** en un
evento previo (4 categorías × 4 opciones = 256 combinaciones, y saltear una
opción te aplica una penalidad).

**P12 — El boss juega a favor tuyo para alargar.** *The Creator* usa **Rise**:
revive a tus gatos caídos a media vida. Anti-attrition puro: no te deja ganar por
desgaste.

---

## 5. Matriz de adaptación

Clasificado por **costo real contra nuestro código**, no por lo lindo que suene.

### 5.1 T0 — cero C# nuevo (sólo árbol / assets)

| Idea | Origen | Cómo se arma con lo que hay |
|---|---|---|
| **Matar a los refuerzos enfurece al boss** | P7 (*Chubs & Nubs*) | B1 ya spawnea 2 ranged a 65%. Sumar `If(NOT PcAllyAliveExists) → Once → ApplyStatModifier(Attack +N)`. Único detalle: `PcAllyAliveExists` no tiene negación — se resuelve poniendo el buff en la rama `Else` del `If`. |
| **Ciclo de 4 ataques legible en vez de 2** | P2 (*Coven*, *C-800*, *Johnny*) | `AINode_Alternate` ya rota determinísticamente. Pasar de 2 a 4 patas con shapes distintas (`Row`, `Column`, `SquareAroundSelf`, `DirectionalBand`) da un ciclo aprendible sin una línea de código. |
| **Cadencia de ritual** | P2 (*Coven*: 5 turnos) | `PcRoundNumber(Mode = Multiple, Value = N)` dentro de un `If`. Ya existe y nadie lo está usando en los bosses. |
| **Objetos rompibles en la sala** | P1 (*velas del Coven*, *bombas de Radical Rat*) | `AINode_SpawnReinforcements` apuntando a un `EnemyDataSO` nuevo con `BaseHP 1-50`, `BaseAttack 0`, `BaseSpeed 0` y `AIRoot = Wait`. Es un objeto rompible que ya entra a la cola de turnos y ya tiene barra de vida. **Sin C# nuevo.** |
| **Arreglar H1/H2/H3** | — | Sacar el `TelegraphMark` de dentro del `Selector` y darle su propio slot en el `Sequence` raíz (el patrón que B1 ya usa bien), asignar el `Behavior` faltante de B3, y gatear el movimiento de B1 con `If(PcTargetInRange)` para que kitee **o** persiga, no las dos. |
| **Fase 2 con stats de verdad** | P6 | Los `ApplyStatModifier` de B2/B3 ya están cableados con deltas en 0: es cambiar dos números. |
| **Hazards distintos por boss** | P9 | `HazardDefinitionSO` ya es data-driven (shape/size/count/damage/kind/cadencia). Autorar 2-3 SOs más y apuntarles `AINode_ActivateHazard`. El `FireHazardDefinition` huérfano existe justamente para esto. |

### 5.2 T1 — un nodo o PreCondition chico (30-80 líneas, mismo patrón que uno existente)

| Idea | Origen | Qué hay que escribir |
|---|---|---|
| **"¿Sigue vivo *ese* objeto?"** | P1 | `PcAllyWithIdAlive(entityId)` — copia de `PcAllyAliveExists` filtrando por `EntityId`. Es **la pieza que habilita todo P1**: sin ella no podés ramificar según si el jugador rompió la vela/el dado. |
| **Telegraph anclado a un objeto** | P1 (*bombas*) | `AINode_TelegraphMark` hoy ancla en el jugador o en sí mismo. Sumar un modo "anclar en la coord de la entidad X" (una `ThreatShape` nueva **appendeada al final del enum**). |
| **Formas invertidas (todo menos…)** | P3 (*Throbbing King*) | `ThreatAreaShape.Compute` es código puro y testeado. Sumar `AllExceptSafeTiles(n)` (Roulette), `AllExceptAdjacentToSelf` (Kneel) y `AllExceptBorder` (Go Away). Es la adaptación con mejor relación impacto/costo de todo el documento. |
| **Munición / contador de uso** | P4 (*Zodiac*), P5 | `AINode_Counter` o un `PcSelfCounter` — el patrón de estado `[NonSerialized]` por combate ya está en `Alternate` y en `PromulgateRule._bossTurnCounter`. |
| **Knockback real** | P4/P8 | `EffKnockback`: resolver la coord destino y llamar `IMovementService.Move(targetGuid, coord)` — el service ya existe y es genérico. Lo caro no es moverlo, son las reglas de colisión (¿qué pasa si hay pared/otra entidad?). |
| **Contador que mata al boss** | P5 (*Queen Hippo*) | Un `AINode_Tick`/`ApplyStatModifier` con daño a sí mismo + `PcOwnerStatCompare` ya alcanzan para un timer; lo que falta es la UI que lo haga legible. |

### 5.3 T2 — sistema nuevo (decisión de sprint, no tarea de boss)

- **Estados por turno (burn / poison / bleed / bruise / brace)**. Es el esqueleto
  de medio Mewgenics y no lo tenemos: hoy hay `Modifier<int>` con lifetimes,
  escudo y stats, pero nada que tickee daño al inicio del turno. Sin esto, P9
  (terreno) es sólo "daño instantáneo al pisar" y P6 (limpiar debuffs en la
  transición) no significa nada.
- **Tiles con estado.** Ya está evaluado en `reposicionamiento-peso-tactico.md`
  (Opción A). Reusa el overlay de telegraph, pero es sistema nuevo igual.
- **Deshabilitar el set de acciones del jugador (P8).** "Te agarró, tu única
  acción es Zafar" necesita gatear `ActionDefinitionSO` desde un estado de
  combate. Es el mecanismo más **potente** de la lista para un juego de un solo
  personaje, porque el costo no es perder una unidad: es perder un turno de
  decisión. Diría que es el T2 con mejor retorno.
- **Bosses multi-parte (P10).** Requiere que varias entidades compartan un HP/una
  fase y que el `TurnOrderService` las coordine. Grande.
- **Clon del jugador (P7, *The Creator*).** Un `EnemyDataSO` no puede leer la
  `DiceBag` ni el Contrato del jugador. Sería un enemigo con build derivada en
  runtime. Concepto fuerte para un boss final, sistema entero.

### 5.4 Descartado, y por qué

| Mecánica | Por qué no |
|---|---|
| **2-4 turnos por ronda** (Gambit 3, Throbbing King 4, Guillotina 3, Tormentor 2) | `TurnOrderService.Append` deduplica por GUID: una entidad no puede tener dos slots. Nuestro equivalente es "más acciones en un mismo turno" (`Sequence` con varios nodos de acción), que es lo que B3 ya hace con dos `Move`. **No perseguir turnos dobles**; usar `SpeedDelta` y secuencias más largas. |
| **Instakill / comer / digerir** (Consume, Digest, Boulder 999, Feast) | Un solo personaje: instakill = game over sin lectura. Traducible sólo como "te agarra y perdés turnos" (P8). |
| **Charm / posesión** (Marshmallow, Dybbuk, Mind Control) | Necesita aliados a quienes traicionar. Con un PJ, "charm" es "perdés el turno" = P8 otra vez. |
| **Dodge %** (C-1000, Backflip) | Nuestra fórmula de daño no tiene tirada de acierto; meter miss agrega varianza arriba de la que ya aportan los dados. |
| **Escalado por dificultad** (Hard/Crazy/Impossible ×1.2/1.4/1.6) | Nuestro análogo es `ExtraTiers`, hoy vacío en los tres bosses. Es tema de la curva de `balance-modelo-3-pisos.md`, no de mecánicas. |

---

## 6. Propuestas concretas

### 6.1 Boss 1 — el dado en la mesa (P1 *Gambit* + lo que ya tiene)

La descripción del Sunken Grand dice literalmente que "no necesita adivinar qué
combo vas a tirar: le alcanza con sacar una parte de la ecuación". Hoy eso es un
`RotateBlock(Dice, 1)` invisible salvo por un candadito. **Gambit convierte esa
misma idea en un objeto del tablero:**

> El boss **te saca un dado y lo pone en la mesa**. Mientras esté ahí: (a) está
> bloqueado de tu bolsa — es el mismo `IDiceBlockService` que ya corre —, y
> (b) su cara determina el ataque que el boss prepara. Cara baja → ataque chico;
> cara alta → el `DirectionalBand` de 80. **Podés pegarle al dado**: eso lo
> re-rollea (una cara nueva, quizá mejor) y si lo rompés, recuperás el dado en
> tu bolsa y el boss "fizzlea" el turno.

Por qué es la mejor adaptación disponible: el dado en la mesa **es** un objeto de
1 HP spawneado con `SpawnReinforcements` (T0), el bloqueo del dado **ya existe**,
y el único código nuevo es `PcAllyWithIdAlive` (T1) para ramificar el ataque
según si el dado sigue vivo. Y resuelve de una el problema de
`reposicionamiento-peso-tactico.md`: por primera vez hay una razón mecánica y
clarísima para caminar hasta un punto específico del tablero.

Además: resolver H3 dándole una identidad por pata — pata A (`SquareAroundSelf`)
kitea, pata B (`DirectionalBand`) cierra distancia. Y decidir H4.

### 6.2 Boss 2 — las cámaras (P1 *velas del Coven* + P2 *rotación de C-800*)

El Security Boss vigila, y su ataque hoy es una franja que casi nunca dispara
(H1). Propuesta:

> La sala tiene **3-4 cámaras** (objetos rompibles en las paredes). Cada cámara
> viva le habilita **una franja de vigilancia** al boss; el `TelegraphMark` cubre
> una `Row`/`Column` **por cámara viva**. Romper cámaras le achica la cobertura.
> Debajo del 20% de HP, el boss gasta su turno en **reencender una cámara** en
> vez de atacar (el `Rise` del Coven).

Con el `RotateBlock(Combo)` que ya tiene, la lectura es "me vigilan **y** me
limitan las jugadas", las dos presiones de la misma identidad. Costo: T0 (objetos
+ árbol) + `PcAllyWithIdAlive` (T1, compartido con 6.1). Requiere arreglar H1
primero, o el ataque sigue sin salir.

### 6.3 Boss 3 — los decretos espaciales (P3 *Throbbing King*)

Esta es la coincidencia temática más limpia del relevamiento. El Throbbing King
**dicta órdenes** y castiga todo lo que no las cumple; el General Director
**promulga reglas** con lenguaje burocrático. Es el mismo mecanismo, aplicado a
espacio en vez de a combos. Sumar al `switch` de `PromulgateRule`:

| Regla | Efecto | Origen |
|---|---|---|
| **R07 — Ruleta** | amenaza todo el tablero **menos** 7-9 casillas al azar | Roulette |
| **R08 — Audiencia** | amenaza todo **menos** lo adyacente al Director | Kneel |
| **R09 — Desalojo** | amenaza todo lo que **no** sea borde | Go Away |
| **R10 — Distanciamiento** | amenaza 2 casillas alrededor tuyo, y **te sigue** | Spread Out (≈ `SquareAroundPlayer`, ya existe) |

R10 es T0 (shape existente). R07-R09 son las tres formas invertidas de §5.2.
El nodo ya tiene el contador de intervalo y el gate de fase — es un `case` más en
el mismo `switch`, exactamente como se propuso R07 en
`reposicionamiento-peso-tactico.md §7.6`, pero con las 4 variantes que Mewgenics
ya probó que funcionan. Y arregla H2 de paso: el boss deja de depender de que te
acerques para hacer algo.

### 6.4 Dos conceptos para bosses nuevos

**"La Banca" (P10 + P8)** — cabeza inmóvil 3×3 + **dos manos de croupier** que se
mueven solas. Las manos **te agarran** si terminás el movimiento delante de la
palma (te queda una sola acción: Zafar); pegarle a una mano por atrás la suelta y
la hace embestir a la cabeza. Es *Man in the Moon* con temática de mesa. Requiere
P8 y multi-parte (T2) — candidato a boss final, no a piso 4.

**"El Croupier ciego" (P7 *Chubs & Nubs*)** — **dos** enemigos de HP medio que se
mueven semi-al azar y se pegan entre ellos si los posicionás bien; matar a uno
enfurece al otro. Es el boss más barato de todo el documento: dos `EnemyDataSO`
con `AINode_Random` para el movimiento y un `If(Else de PcAllyAliveExists) →
ApplyStatModifier`. **Casi T0.** Buen candidato si hace falta un boss más rápido
de lo que un boss nuevo suele costar.

---

## 7. Secuencia recomendada

1. **Decidir H1-H6 en mesa** (media hora, sin código). Sin esto, cualquier
   contenido nuevo se apila arriba de dos bosses cuyo ataque principal no
   dispara. Es la mejor relación esfuerzo/impacto del documento, por lejos.
2. **`PcAllyWithIdAlive`** (T1, chico) + **un `EnemyDataSO` "objeto"** (T0). Esas
   dos piezas juntas desbloquean P1 entero: dado, cámaras, bombas, velas.
3. **Prototipo 6.1 (el dado en la mesa)** en el boss de piso 1. Es la propuesta
   con más identidad, reusa 3 sistemas que ya funcionan, y da la primera razón
   real para reposicionarse.
4. **Formas invertidas + R07-R10** (§6.3) para el piso 3. Es código puro y
   testeable (`ThreatAreaShape` no depende de escena).
5. Recién después: evaluar **estados por turno** y **P8 (deshabilitar acciones)**
   como iniciativas de sistema. Las dos cambian el techo de diseño de todos los
   enemigos, no sólo de los bosses — no deberían entrar como "mejora de boss".

---

## 8. Preguntas abiertas

- **H4**: ¿el dice-block del piso 1 desde el turno 1 es intencional?
- **H7**: los telegraphs de 80/100 contra ~50-63 de HP efectivo — ¿one-shot es la
  intención, o quedaron de una escala de daño anterior? (Bocco)
- Si entra 6.1, ¿el dado en la mesa es **uno de los tuyos** (se siente como un
  robo, y su cara importa el doble porque conocés ese dado) o un dado del boss
  (más simple de comunicar)? Yo iría por el tuyo.
- ¿Los objetos rompibles entran a la cola de turnos como una entidad más? Con
  `SpawnReinforcements` es lo que pasa por default, y ocupa un slot visible en el
  `TurnQueueView` — puede ser bueno (legibilidad) o ruido.

---

## Fuentes

Wiki de Mewgenics (relevados: los 32 bosses de `Category:Bosses` + los 6 class
bosses):
[Bosses](https://mewgenics.wiki.gg/wiki/Bosses) ·
[Gambit](https://mewgenics.wiki.gg/wiki/Gambit) ·
[Throbbing King](https://mewgenics.wiki.gg/wiki/Throbbing_King) ·
[The Coven](https://mewgenics.wiki.gg/wiki/The_Coven) ·
[Queen Hippo](https://mewgenics.wiki.gg/wiki/Queen_Hippo) ·
[Zodiac](https://mewgenics.wiki.gg/wiki/Zodiac) ·
[Spinnerette](https://mewgenics.wiki.gg/wiki/Spinnerette) ·
[Guillotina](https://mewgenics.wiki.gg/wiki/Guillotina) ·
[Pyrophina vs Zaratana](https://mewgenics.wiki.gg/wiki/Pyrophina_vs_Zaratana) ·
[C-800](https://mewgenics.wiki.gg/wiki/C-800) ·
[C-1000](https://mewgenics.wiki.gg/wiki/C-1000) ·
[The Mother](https://mewgenics.wiki.gg/wiki/The_Mother) ·
[Dybbuk](https://mewgenics.wiki.gg/wiki/Dybbuk) ·
[Crater Maker](https://mewgenics.wiki.gg/wiki/Crater_Maker) ·
[Radical Rat](https://mewgenics.wiki.gg/wiki/Radical_Rat) ·
[Boris](https://mewgenics.wiki.gg/wiki/Boris) ·
[Chubs & Nubs](https://mewgenics.wiki.gg/wiki/Chubs_%26_Nubs) ·
[Spewer](https://mewgenics.wiki.gg/wiki/Spewer) ·
[Johnny](https://mewgenics.wiki.gg/wiki/Johnny) ·
[Lord Bunga](https://mewgenics.wiki.gg/wiki/Lord_Bunga) ·
[Stacy Mutant](https://mewgenics.wiki.gg/wiki/Stacy_Mutant) ·
[The Man in the Moon](https://mewgenics.wiki.gg/wiki/The_Man_in_the_Moon) ·
[Dreadnoughtus](https://mewgenics.wiki.gg/wiki/Dreadnoughtus) ·
[Pebbles](https://mewgenics.wiki.gg/wiki/Pebbles) ·
[Chaos!](https://mewgenics.wiki.gg/wiki/Chaos!) ·
[The Creator](https://mewgenics.wiki.gg/wiki/The_Creator) ·
[Status Effects](https://mewgenics.wiki.gg/wiki/Status_Effects) ·
[Class Bosses](https://mewgenics.wiki.gg/wiki/Category:Class_Bosses)

Código y assets citados: `ED_Boss_Sunken_Grand.asset`,
`ED_Boss_Security_Boss.asset`, `ED_Boss_GeneralDirector.asset`,
`RainHazardDefinition.asset`, `AINode_If.cs`, `AINode_Behavior.cs`,
`AINode_RotateBlock.cs`, `AINode_PromulgateRule.cs`, `AINode_TelegraphMark.cs`,
`HazardDefinitionSO.cs`, `ThreatAreaShape.cs`, `TurnOrderService.cs`,
`EffApplyImpulse.cs`, `EffMove.cs`, `PcAllyAliveExists.cs`, `PcRoundNumber.cs`.

Docs relacionados: `docs/design/reposicionamiento-peso-tactico.md`,
`docs/planning/balance-modelo-3-pisos.md`, `docs/design/damage-analysis.md`,
`docs/design/boss1-feature-branch-behavior.md` (desactualizado — ver §2.1).
