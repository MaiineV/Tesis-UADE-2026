# TUTORIAL completo — Boss 2: Jefe de Seguridad (Memoria de Combos)

Tutorial **paso a paso desde cero** (mismo formato que el del Boss 1). Al
terminar tenés el Boss 2 funcional: recuerda el/los combo(s) que ejecutaste y
los **prohíbe** (aparecen con daño 0 en el Contrato; si los armás hacen 0
daño), ataque telegráfico de **franja**, y Fase 2 al cruzar el umbral de HP.

> **Prerequisitos (ya hechos):** los 4 bootstraps de boss están en
> `ServiceBootstrap.ExtraServices` (en especial **`ComboLogServiceBootstrap`**
> y **`ContractModifierServiceBootstrap`**, que usa este boss). Y el proyecto
> compila. Si te falta alguno, eso primero.

> **Importante (cómo funciona el bloqueo de combos):** la "memoria" usa la
> **capa de modificadores del Contrato** (no el viejo combo-block que salteaba
> el combo). El combo recordado queda **prohibido**: se ve con **daño 0 en la
> tabla del Contrato** antes de tirar, y si lo armás hace **0 daño total** (ni
> siquiera el daño base del PJ). Si armás cualquier otro combo, daño normal.
> Para que el "0" se vea en pantalla, el HUD de combate tiene que tener un
> `ContractDisplayView` con sus `ComboRowView` (refrescan solos al cambiar la
> regla).

---

## Qué vamos a construir (el árbol final)

```
[ROOT] Sequence  "Boss Turn"
 ├─ (1) ExecuteTelegraph                          ← ejecuta la franja marcada el turno anterior
 ├─ (2) If  PcOwnerHpBelow 0.20                   ← feedback de Fase 2, una sola vez
 │        Then → Once → ApplyStatModifier(phase 2, sin cambio de stat)
 │        Else → Wait
 ├─ (3) Selector  "Action Pool"
 │        ├─ If  PcTargetInRange 1                ← pegado → melee
 │        │      Then → Behavior "Melee"
 │        │      Else → (vacío)
 │        └─ Random  "Far Actions"               ← si no → rango / franja / reposicionarse
 │              ├─ Behavior "Ranged"
 │              ├─ If  PcOwnerHpBelow 0.20        ← franja más ancha en Fase 2
 │              │      Then → TelegraphMark(Row, size anchoFase2)
 │              │      Else → TelegraphMark(Row, size anchoFase1)
 │              └─ Move (reposiciona la ruta de escape)
 └─ (4) If  PcOwnerHpBelow 0.20                   ← al cerrar el turno: actualiza la memoria
          Then → RotateBlock(Combo, Count 2)      ← Fase 2: ventana de 2 combos
          Else → RotateBlock(Combo, Count 1)      ← Fase 1: ventana de 1 combo
```

**Las dos reglas de oro del árbol** (idénticas al Boss 1):
1. Un **`Sequence` corta en el primer hijo que devuelve `Failed`**, y un **`If`
   con la rama elegida vacía devuelve `Failed`**. Por eso el `If` del paso 2
   lleva un `Wait` en el `Else`, y el paso 4 tiene `RotateBlock` en ambas ramas.
2. **Conectá antes de configurar:** el panel derecho solo muestra los
   parámetros de un nodo cuando ya está conectado con un camino hasta el ROOT.
   Si ves *"este nodo no es alcanzable desde el AIRoot…"* / *"Orphan node"*,
   es que todavía no lo conectaste. Construí de arriba (ROOT) hacia abajo.

---

## PARTE A — Crear el asset del Boss

### A1. Abrir el editor
Menú de Unity: **`Tools > Enemy Editor`**.

### A2. Crear el EnemyDataSO
En el panel izquierdo, abajo, **`+ Create Enemy`**. Renombralo en Project
(ej. `ED_Boss_JefeDeSeguridad`). Es un **EnemyDataSO normal** — toda la
mecánica vive en el árbol.

### A3. Stats (pestaña "Enemy Data")

| Campo (sección) | Valor | Stat del ticket |
|---|---|---|
| **Identity** → `EntityId` | `boss.security_chief` (único) | — |
| **Identity** → `DisplayName` | `Jefe de Seguridad` | — |
| **Visual** → `Visual Prefab` | prefab visual del boss | — |
| **Base Stats** → `BaseHP` | ej. 110 | hpMaximo |
| **Base Stats** → `BaseAttack` | ej. 10 | fuerzaAtaque (daño melee/rango) |
| **Base Stats** → `BaseSpeed` | ej. 8 | velocidad |
| **Base Stats** → `MaxEnergy` | ej. 3 | — |
| **Rewards** → `Min/MaxGoldDrop` | a gusto | — |

Dejá la lista **Behaviors** vacía.

---

## PARTE B — Recordatorio del GraphView (igual que Boss 1)

Pestaña **`AI Tree`**:
1. **Agregar nodo:** clic derecho en el canvas → buscador con **Composites**
   (Selector, Sequence, Once), **Branching** (If, Random, While) y **Leaves**
   (Move, Wait, Behavior, **TelegraphMark, ExecuteTelegraph, RotateBlock**, …).
2. **Editar valores:** clic en el nodo → panel derecho.
3. **Conectar:** arrastrá del puerto de salida (abajo: `Children`/`Then`/
   `Else`/`Options`/`Child`) al puerto `in` (arriba) del hijo.
4. **Root:** clic derecho sobre un nodo → `Set as Root` (chip amarillo).
5. **Guardado automático.** Si hay texto rojo arriba a la izquierda, el árbol
   no es válido (orphan / falta Then / etc.).

---

## PARTE C — Construir el árbol paso a paso (de arriba hacia abajo)

### C1. Root: Sequence
Clic derecho → **Composites → Sequence**. Luego clic derecho sobre él →
**`Set as Root`**.

### C2. (1) ExecuteTelegraph
Clic derecho → **Leaves → ExecuteTelegraph**. Conectá: `Children` del Sequence
→ `in` del ExecuteTelegraph. (No tiene parámetros — es normal.)

### C3. (2) Bloque de Fase 2 (feedback, una sola vez)
Creá **If**, **Once**, **ApplyStatModifier**, **Wait** y conectalos primero:
- Sequence `Children` (puerto libre) → `in` del **If**.
- If **`Then`** → `in` del **Once**;  Once **`Child`** → `in` del **ApplyStatModifier**.
- If **`Else`** → `in` del **Wait**.  ← no lo omitas.

Ahora configurá:
- **If** → Conditions → `+ Add Condition` → **`PcOwnerHpBelow`** → `Percent` = **`0.2`** (20%).
- **ApplyStatModifier** → `Attack Delta` = `0`, `Speed Delta` = `0`,
  `Phase Index` = `2`, `Emit Phase Changed Event` = ✔.
  > El Boss 2 **no cambia stats** en Fase 2; su cambio es de comportamiento
  > (recuerda 2 combos + franja más ancha, que se hacen en los pasos 3 y 4).
  > Este nodo solo dispara `OnBossPhaseChanged` para tu animación de
  > "actualización de sistema" + línea de diálogo.

### C4. (3) Action Pool
Creá **Selector**, **If** (gate melee), **Behavior** (Melee), **Random**,
**Behavior** (Ranged), **If** (franja por fase), **TelegraphMark** (×2, ancha y
angosta) y **Move**.

Conexiones del pool:
- Sequence `Children` (puerto libre) → `in` del **Selector**.
- Selector `Children` (1er puerto) → `in` del **If de melee**.
  - If de melee **`Then`** → **Behavior "Melee"**.  Dejá el **`Else` vacío**
    (acá SÍ va vacío: que falle hace que el Selector pruebe la opción siguiente).
- Selector `Children` (2º puerto) → `in` del **Random**.
  - Random `Options` (1) → **Behavior "Ranged"**.
  - Random `Options` (2) → **If de franja**:
    - If de franja **`Then`** → **TelegraphMark (ancha, Fase 2)**.
    - If de franja **`Else`** → **TelegraphMark (angosta, Fase 1)**.
  - Random `Options` (3) → **Move**.

Configuración de cada uno (clic → panel derecho):

**If de melee** → Conditions → `+ Add Condition` → **`PcTargetInRange`** →
`Range` = `1`, `Metric` = `Manhattan`.

**Behavior "Melee"** → `Behavior` Type = **`EnemyActionBehavior`**;
`Action Name` = `Melee`; Target Selector = `(none)`; **Effect Pipeline**
→ `+ Add Effect Group` → **`EffectData`** → dentro: `Effects` →
`+ Add Effect` → **`EffDealDamage`** → `Damage Source` = **`FromReader`** →
`Reader Type` = **`ReadEntityStat`** (`Entity` = `Source`, `Stat` = `Attack`,
`Use Modified` = ✔), `Attack Kind` = `BasicAttack`. (Igual que el Boss 1.)

**Behavior "Ranged"** → idéntico a Melee pero `Action Name` = `Ranged`.

**If de franja** → Conditions → `+ Add Condition` → **`PcOwnerHpBelow`** →
`Percent` = `0.2`.

**TelegraphMark (Fase 2, ancha)** → `Shape` = **`Row`** (franja horizontal; usá
`Column` si querés vertical), `Size` = el ancho de Fase 2 (ej. **3** = la fila
del jugador ±1), `Damage` = danioAreaTelegrafico, `Kind` = `BasicAttack`,
`Highlight Style` = `warning`.

**TelegraphMark (Fase 1, angosta)** → igual pero `Size` = ancho de Fase 1
(ej. **1** = solo la fila del jugador).

**Move** → `Max Steps` Type = **`AIConstantInt`** → `Value` = `3`;
`Stop Adjacent` = ✔.

> `Shape = Row` marca toda(s) la(s) fila(s) del jugador; `Column` marca
> columna(s). El `Size` es el ancho en casillas (1 = la línea del jugador,
> 3 = ±1). Eso obliga al jugador a moverse perpendicular a la franja.

### C5. (4) Memoria de combos (fin de turno)
Creá **If** (gate de fase), **RotateBlock** (Fase 2) y **RotateBlock** (Fase 1).

Conexiones:
- Sequence `Children` (último puerto libre) → `in` del **If de memoria**.
- If **`Then`** → **RotateBlock "Fase 2"**;  If **`Else`** → **RotateBlock "Fase 1"**.

Configuración:
- **If de memoria** → Conditions → `+ Add Condition` → **`PcOwnerHpBelow`** →
  `Percent` = `0.2`.
- **RotateBlock "Fase 2"** → `Target` = **`Combo`**, `Count` = **`2`**.
- **RotateBlock "Fase 1"** → `Target` = **`Combo`**, `Count` = **`1`**.

> `RotateBlock(Combo)` lee los últimos `Count` combos del log del jugador y los
> **prohíbe** en el Contrato (ventana deslizante: borra los del turno previo y
> prohíbe los nuevos). Un combo prohibido se muestra con **0** en la tabla y, si
> el jugador lo arma, hace **0 daño**. Compara por **tipo** de combo (Par = Par,
> sin importar el valor de los dados), porque el log guarda el `ComboId`.

### C6. Verificación
Sin texto rojo arriba a la izquierda. Orden de hijos del Sequence raíz:
**ExecuteTelegraph → If(Fase2) → Selector → If(Memoria)**.

---

## PARTE D — Hacer que aparezca en combate

Igual que cualquier boss: que la sala de boss spawnee **este** EnemyDataSO.
La forma más directa (la que ya usás): en el **prefab de la sala** de boss, el
componente **`SpawnPointConfig` → `Enemy Sets`** tiene que apuntar a
`ED_Boss_JefeDeSeguridad` (o dejá `Enemy Sets` vacío y poné el boss en el
`EnemyPool` del `RoomSO`). Registralo también en el `EnemyCatalogSO`.

---

## PARTE E — Probar (checklist de Definition of Done)

- [ ] **Turno 1:** podés usar cualquier combo libre; al cerrar tu turno, el
      Boss registra cuál fue.
- [ ] **Turno 2+:** el combo del turno anterior aparece **marcado con 0** en la
      tabla del Contrato **antes de tirar**.
- [ ] Si armás ese combo → **0 daño**. Si armás otro → **daño normal**.
- [ ] El bloqueo compara por **tipo** (Par de doses = Par de cincos).
- [ ] **Fase 2 (HP ≤ 20%):** se dispara `OnBossPhaseChanged` (tu feedback de
      "actualización de sistema") una sola vez, pasa a **2 combos** en memoria
      simultáneos, y la **franja se ensancha**.
- [ ] El **telegráfico de franja** resalta una fila/columna un turno y **pega al
      siguiente** si seguís adentro; si te moviste fuera, **no hace daño**.
- [ ] Probá con build de **un solo combo dominante** (te duele perderlo) y con
      una **diversificada** (rotás combos para esquivar el bloqueo).

---

## Notas / límites honestos

- **Umbral de Fase 2:** es el `Percent` de los **tres** `PcOwnerHpBelow` (Fase 2
  setup, franja, memoria) — todos en `0.2`. Si lo cambiás, cambialo en los tres.
- **Ventana de memoria:** el `Count` de los `RotateBlock` (1 → 2).
- **Daño/ancho de la franja:** `TelegraphMark.Damage` y `Size` (angosta vs ancha).
- **Daño mínimo (dado más alto):** el ticket pide que el "sin combo" también
  quede bloqueado. Hoy el ataque de combate **sin combo ya hace 0** (el daño
  mínimo de la GD §5 no está implementado en el ataque de combate), así que ese
  sub-caso es inocuo: el log registra el marcador `none` para cuando se
  implemente el daño mínimo, pero por ahora no hay nada que anular.
- **UI del combo bloqueado:** el "0" en rojo lo muestra el `ComboRowView` del
  `ContractDisplayView` del HUD de combate (refresca solo al cambiar la regla).
  Si tu HUD de combate todavía no instancia el Contrato, ese feedback no se ve
  (el 0 daño igual aplica).
