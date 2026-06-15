# TUTORIAL completo — Boss 1: Contador de Pisos (Bloqueo de Dados)

Tutorial **paso a paso desde cero**, asumiendo que **nunca usaste el Enemy
Editor**. Al terminar tenés el Boss 1 funcional al 100%: bloqueo de dados que
rota por turno, ataque telegráfico de 2 turnos, y Fase 2 al cruzar el umbral
de HP.

> **Prerequisitos (ya hechos):** los 4 bootstraps de boss están en
> `ServiceBootstrap.ExtraServices` y el proyecto compila. Si no, eso primero.

---

## Qué vamos a construir (el árbol final)

Un árbol de decisión que en **cada turno del Boss** hace, en orden:

```
[ROOT] Sequence  "Boss Turn"
 ├─ (1) ExecuteTelegraph                         ← ejecuta el telegráfico marcado el turno anterior
 ├─ (2) If  PcOwnerHpBelow 0.10                  ← entrar a Fase 2 una sola vez
 │        Then → Once → ApplyStatModifier(Speed +2, phase 2)
 │        Else → Wait
 ├─ (3) Selector  "Action Pool"
 │        ├─ If  PcTargetInRange 1               ← si está pegado → melee
 │        │      Then → Behavior "Melee"
 │        │      Else → (vacío)
 │        └─ Random  "Far Actions"               ← si no → ataca a rango / telegrafía / se acerca
 │              ├─ Behavior "Ranged"
 │              ├─ TelegraphMark (Square, size 1)
 │              └─ Move (3 pasos, stop adjacent)
 └─ (4) If  PcOwnerHpBelow 0.10                  ← al cerrar el turno: rota el bloqueo de dados
          Then → RotateBlock (Dice, Count 2)     ← Fase 2: 2 dados
          Else → RotateBlock (Dice, Count 1)     ← Fase 1: 1 dado
```

**Regla de oro del árbol** (clave para que funcione): un **`Sequence`
corta en el primer hijo que devuelve `Failed`**, y un **`If` con la rama
elegida vacía devuelve `Failed`**. Por eso el `If` del paso 2 lleva un `Wait`
en el `Else`, y el paso 4 tiene `RotateBlock` en ambas ramas. Si dejás esos
`Else` vacíos, el turno se corta antes de rotar el bloqueo.

---

## PARTE A — Crear el asset del Boss

### A1. Abrir el editor
Menú de Unity: **`Tools > Enemy Editor`**. Se abre una ventana con un panel
de lista a la izquierda y un panel con pestañas a la derecha.

### A2. Crear el EnemyDataSO
1. En el panel izquierdo, abajo, clic en **`+ Create Enemy`**.
2. Crea `Assets/Rollgeon/Enemies/ED_NewEnemy.asset` y lo selecciona solo.
   (Si querés renombrarlo, hacelo en la ventana Project: por ej.
   `ED_Boss_ContadorDePisos`.)

> El boss del Bloqueo de Dados usa un **EnemyDataSO normal** — toda su
> mecánica vive en los nodos del árbol, no en campos del SO. No necesitás
> `BossFloorManagerSO` (ese era para el boss viejo de bloqueo de combos).

### A3. Stats (pestaña "Enemy Data")
Con el boss seleccionado, clic en la pestaña **`Enemy Data`**. Completá:

| Campo (sección) | Valor | Stat del ticket |
|---|---|---|
| **Identity** → `EntityId` | `boss.floor_counter` (único) | — |
| **Identity** → `DisplayName` | `Contador de Pisos` | — |
| **Visual** → `Visual Prefab` | arrastrá el prefab visual del boss | — |
| **Base Stats** → `BaseHP` | el que quieras (ej. 120) | hpMaximo |
| **Base Stats** → `BaseAttack` | ej. 10 | fuerzaAtaque (daño de melee/rango) |
| **Base Stats** → `BaseSpeed` | ej. 8 (alto = juega antes) | velocidad |
| **Base Stats** → `MaxEnergy` | ej. 3 | — |
| **Rewards** → `MinGoldDrop` / `MaxGoldDrop` | a gusto | — |

> `BaseSpeed` alto conviene: si el Boss juega **antes** que el jugador, el
> primer turno del jugador ya tiene un dado bloqueado. Si juega después, el
> primer turno del jugador no tiene bloqueo (recién al cerrar su turno el
> Boss sortea) — comportamiento esperado.

Dejá la lista **Behaviors** vacía: las acciones del boss van en el árbol.

---

## PARTE B — Conceptos del GraphView (leelo una vez)

Clic en la pestaña **`AI Tree`**. Es un canvas con un panel inspector a la
derecha. Cinco cosas que tenés que saber:

1. **Agregar un nodo:** **clic derecho** en un espacio vacío del canvas →
   se abre un buscador con categorías **Composites** (Selector, Sequence,
   Once), **Branching** (If, Random, While) y **Leaves** (Move, KeepDistance,
   Wait, Behavior, **TelegraphMark, ExecuteTelegraph, RotateBlock,
   PromulgateRule, ApplyStatModifier**). Escribí para filtrar y clic para
   crear; aparece donde clickeaste.
2. **Editar los valores de un nodo:** **clic en el nodo** → el **panel
   derecho** muestra sus campos editables. *La edición NO es en el canvas, es
   en el panel derecho.*
3. **Conectar nodos:** arrastrá desde un **puerto de salida** (abajo del nodo
   padre, con etiqueta `Children` / `Then` / `Else` / `Options` / `Child`)
   hasta el **puerto de entrada** (`in`, arriba del hijo).
4. **Marcar el root:** **clic derecho sobre un nodo → `Set as Root`**. El root
   muestra un chip amarillo `ROOT`. Solo puede haber uno.
5. **Guardado:** es **automático** en cada cambio. Si hay un error de
   estructura, aparece en **rojo arriba a la izquierda** (ej. "No root node",
   "Orphan node", "If-node has no Then branch"). El árbol no es válido hasta
   que no haya texto rojo.

> ⚠️ **MUY IMPORTANTE — conectá antes de configurar.** El panel derecho
> **solo muestra los parámetros de un nodo si está conectado al árbol con un
> camino ininterrumpido hasta el ROOT.** Mientras un nodo esté suelto
> (huérfano) vas a ver el mensaje *"este nodo no es alcanzable desde el
> AIRoot…"* en vez de sus campos, y un *"Orphan node"* en rojo arriba. No
> faltan los parámetros: están escondidos hasta que lo conectes.
> **Por eso construí el árbol de arriba (ROOT) hacia abajo: creá un nodo,
> conectá su `in` a un puerto de un nodo que ya cuelgue del ROOT, y recién
> ahí configuralo.** (`ExecuteTelegraph` no tiene parámetros, así que para
> ese el "no inline parameters" es normal aun bien conectado.)

> Slots dinámicos (`Children` de Sequence/Selector, `Options` de Random):
> cada vez que conectás un hijo, aparece **un puerto libre nuevo** para
> agregar el siguiente. El **orden** de conexión = orden de ejecución
> (izquierda→derecha / arriba→abajo).

---

## PARTE C — Construir el árbol paso a paso

Vamos de arriba (ROOT) hacia abajo: **creá cada nodo, conectalo a un puerto
de salida de su padre —que ya tiene que colgar del ROOT— y recién ahí
configuralo en el panel derecho.** Si configurás un nodo todavía suelto, el
panel solo te muestra el warning de "orphan / no alcanzable" (ver la nota
⚠️ de la Parte B).

### C1. Root: Sequence
1. Clic derecho → **Composites → Sequence**.
2. Clic derecho sobre ese nodo → **`Set as Root`** (chip `ROOT` amarillo).

### C2. (1) ExecuteTelegraph
1. Clic derecho → **Leaves → ExecuteTelegraph**.
2. Conectá: puerto **`Children`** del Sequence → **`in`** del ExecuteTelegraph.
   (No tiene parámetros — es correcto que el panel diga "no inline params".)

### C3. (2) Bloque de Fase 2 (una sola vez)
Creá estos 4 nodos:
- **Branching → If**
- **Composites → Once**
- **Leaves → ApplyStatModifier**
- **Leaves → Wait**

Configurá el **If** (clic en él, panel derecho):
- **Target Selector:** dejalo en `(none)` (cae a "el jugador" por defecto; da
  igual, la condición mira el HP del Boss).
- **Conditions (AND):** clic **`+ Add Condition`** → elegí **`PcOwnerHpBelow`**.
  En el campo `Percent` que aparece debajo, poné **`0.1`** (= 10%, umbral de
  Fase 2).

Configurá el **ApplyStatModifier** (clic, panel derecho):
- `Attack Delta` = `0`
- `Speed Delta` = `2`  (la velocidad sube en Fase 2; cambio **permanente**)
- `Phase Index` = `2`
- `Emit Phase Changed Event` = ✔ (dispara `OnBossPhaseChanged` para tu VFX/diálogo)

Conexiones:
- Sequence `Children` (puerto libre) → `in` del **If**.
- If **`Then`** → `in` del **Once**.
- Once **`Child`** → `in` del **ApplyStatModifier**.
- If **`Else`** → `in` del **Wait**.  ← **no lo omitas** (regla de oro).

### C4. (3) Action Pool: Selector + ramas
Creá:
- **Composites → Selector**
- **Branching → If**  (gate de melee)
- **Leaves → Behavior**  (Melee)
- **Branching → Random**
- **Leaves → Behavior**  (Ranged)
- **Leaves → TelegraphMark**
- **Leaves → Move**

**If de melee** (clic, panel derecho):
- Target Selector: `(none)`.
- Conditions → `+ Add Condition` → **`PcTargetInRange`** → `Range` = `1`,
  `Metric` = `Manhattan`. (distancia ≤ 1 = pegado)

**Behavior "Melee"** (clic, panel derecho):
- **Behavior → Type:** elegí **`EnemyActionBehavior`**.
- `Action Name` = `Melee`.
- `Trigger / Phases`: dejá los defaults (en el árbol el Trigger no se usa).
- Target Selector: `(none)` (= ataca al jugador).
- **Effect Pipeline:** clic **`+ Add Effect Group`** → elegí **`EffectData`**.
  Dentro de ese EffectData:
  - `Effects` → **`+ Add Effect`** → elegí **`EffDealDamage`**.
    - `Damage Source` = **`FromReader`**.
    - Aparece **`Reader Type`** → elegí **`ReadEntityStat`** → `Entity` =
      `Source`, `Stat` = `Attack`, `Use Modified` = ✔ (usa la fuerza del Boss,
      incluyendo el buff de Fase 2).
    - `Attack Kind` = `BasicAttack`.
  - `PreConditions`: dejalo vacío (el gate de distancia ya lo hace el If).

**Behavior "Ranged"** (clic, panel derecho): igual que Melee pero
`Action Name` = `Ranged` y el mismo `EffDealDamage` (FromReader → Attack). Si
querés que pegue distinto a rango, cambiá el reader/multiplier; para empezar,
idéntico está bien.

**TelegraphMark** (clic, panel derecho):
- `Shape` = **`SquareAroundPlayer`**
- `Size` = `1`  (radio 1 ⇒ área 3×3 = radioAreaTelegrafico)
- `Damage` = el daño del telegráfico (danioAreaTelegrafico, ej. 18)
- `Kind` = `BasicAttack`
- `Highlight Style` = `warning` (dejalo así)
- (`Half Axis` solo aplica a HalfRoom; ignoralo)

**Move** (clic, panel derecho):
- `Max Steps` → **Type** = **`AIConstantInt`** → `Value` = `3`.
- `Stop Adjacent` = ✔ (se frena al quedar pegado, para poder pegar melee).

Conexiones del pool:
- Sequence `Children` (puerto libre) → `in` del **Selector**.
- Selector `Children` (1er puerto) → `in` del **If de melee**.
- If de melee **`Then`** → `in` del **Behavior "Melee"**. Dejá el **`Else` vacío**
  (acá SÍ va vacío: que el If falle es lo que hace que el Selector pruebe la
  siguiente opción).
- Selector `Children` (2º puerto) → `in` del **Random**.
- Random `Options` (tres puertos) → `in` de **Behavior "Ranged"**,
  **TelegraphMark** y **Move** (uno por puerto).

> Los pesos del Random quedan iguales (1 cada uno) → elige uniforme entre
> Ranged / Telegráfico / Moverse cuando el jugador no está pegado. El
> telegráfico se centra en el jugador; el daño llega el turno siguiente.

### C5. (4) Rotación del bloqueo de dados (fin de turno)
Creá:
- **Branching → If**  (gate de fase)
- **Leaves → RotateBlock**  (Fase 2)
- **Leaves → RotateBlock**  (Fase 1)

**If de rotación** (clic): Conditions → `+ Add Condition` → **`PcOwnerHpBelow`**
→ `Percent` = `0.1`.

**RotateBlock "Fase 2"** (clic): `Target` = **`Dice`**, `Count` = `2`.
**RotateBlock "Fase 1"** (clic): `Target` = **`Dice`**, `Count` = `1`.

Conexiones:
- Sequence `Children` (último puerto libre) → `in` del **If de rotación**.
- If **`Then`** → RotateBlock **Fase 2** (Count 2).
- If **`Else`** → RotateBlock **Fase 1** (Count 1).

> `RotateBlock(Dice)` hace `Clear()` y sortea `Count` dados distintos al azar
> entre los 5 de la build. Se libera solo al terminar el turno del jugador.
> El sorteo es **100% aleatorio** (sin "bloquear el más valioso"), tal como
> pide el diseño.

### C6. Verificación de estructura
Mirá la esquina superior izquierda del canvas: **no debe haber texto rojo**.
Si dice "Orphan node", te quedó un nodo sin conectar al árbol — conectalo o
borralo. El orden de hijos del Sequence raíz, de arriba a abajo, debe ser:
**ExecuteTelegraph → If(Fase2) → Selector → If(Rotación)**.

---

## PARTE D — Hacer que aparezca en combate

El árbol ya funciona, pero el boss tiene que spawnear. Pasos mínimos:

1. **Registrar en el catálogo:** abrí tu `EnemyCatalogSO` (Project) y agregá
   el `EnemyDataSO` del boss a la lista. (El botón `+ Create Enemy` ya te
   hace ping al catálogo como recordatorio.)
2. **Crear el encuentro:** Project → clic derecho → **Create → Rollgeon →
   Dungeon → Enemy Setup**. En `Slots`, agregá un slot con
   `SpawnPointIndex = 0` y arrastrá el `EnemyDataSO` del boss.
3. **Asignar a la sala:** en la `RoomSO` de la pelea de boss, agregá ese
   `EnemySetupSO` a `Possible Setups` (o usá un `EnemyPoolSO` si preferís
   pool aleatorio).

> Esto es la misma tubería que usás para enemigos normales; el boss no tiene
> nada especial acá.

---

## PARTE E — Probar (checklist de Definition of Done)

Entrá a la pelea y verificá:

- [ ] Al inicio de cada turno del jugador hay **N dados bloqueados** (1 normal).
- [ ] El dado bloqueado se ve **gris + candado** antes de tirar, no entra a
      ningún combo y no se re-rollea (probá tirar y re-rollear).
- [ ] Al turno siguiente, **se libera y se sortea otro** dado.
- [ ] El **telegráfico** resalta un área 3×3 naranja un turno y **pega al
      siguiente** si seguís adentro; si te movés afuera, **no hace daño** y el
      resaltado desaparece.
- [ ] Al bajar a **≤10% HP**: se dispara `OnBossPhaseChanged` (tu feedback de
      Fase 2) **una sola vez**, la velocidad sube, y pasa a bloquear **2 dados**.
- [ ] Probá con una build de **un solo combo fuerte** (el bloqueo duele más) y
      con una **diversificada** (el bloqueo molesta menos) — esa es la tensión.

> Para el candado visual: en el prefab del slot de dado, asigná el `Image` del
> candado al campo **`Lock Icon`** de `DiceSlotView` (sección "Boss 1 — Dice
> block"). Sin eso, el dado igual queda funcionalmente bloqueado pero no
> mostrás el ícono.

---

## Notas / tuning rápido

- **Umbral de Fase 2:** es el `Percent` de los dos `PcOwnerHpBelow` (0.10).
  Si lo cambiás, cambialo en **los dos** (Fase 2 setup y rotación).
- **Dados en Fase 2:** el `Count` del `RotateBlock` de la rama `Then`.
- **Daño telegráfico vs área:** `TelegraphMark.Damage` y `Size`.
- **Velocidad en Fase 2:** `ApplyStatModifier.Speed Delta` (reordena la cola
  recién en la ronda siguiente; es permanente, no se revierte si sube el HP).
- Todo es editable en el Inspector / panel del editor **sin recompilar**.
```
