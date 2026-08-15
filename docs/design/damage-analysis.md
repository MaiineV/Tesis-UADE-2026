# Damage Analysis — Can a build/roll deal 0 damage?

> Fecha: 2026-07-23. Rama analizada: `sprint04/fix/boss-damage-zero-fix`.
> Motivo: en playtest un build all-D6 con "un par de 1s" pegó 0 daño, lo cual
> no debería pasar.

---

## TL;DR (veredicto)

- **Un solo ataque del Guerrero nunca puede dar 0** por la fórmula sola. El
  `Attack` base del PJ (5) es un sumando puro que nunca se multiplica, así que
  el piso de cualquier golpe con dados es **≥ 6** (base 5 + al menos 1 del dado
  más bajo), y con combo es **≥ 13** (Par).
- **La causa real del 0 en playtest es la regla global "repeat-combo = 0"** que
  vivía en `DamagePipeline.Resolve/Preview`: repetir el mismo combo dos turnos
  seguidos zeroeaba el segundo golpe. **Esa regla ya fue removida en esta rama**
  (commit `558b493c`), pero **sigue viva en `develop`**. El ejemplo del commit es
  literalmente "doble par 4-4-3-3 dos veces".
- Los únicos otros caminos a 0 son de diseño intencional (encantamiento
  `BlockComboDamage`) o degenerados (no holdear ningún dado, o un PJ sin stat
  `Attack` y sin combo).

---

## 1. La fórmula de daño (con refs de código)

### 1.1 Cálculo del daño base del combo del jugador

`Assets/Scripts/Rollgeon/Combat/Damage/PlayerComboDamage.cs:31-81`

```
total   = dmg_base_PJ + bonos_PJ + comboTerm + bono_combo
comboTerm = combo_base × multi_dmg_combo × ability_mult × scratch_mult
dmg     = RoundToInt(total)          // Mathf.RoundToInt (banker's rounding)
clamped = dmg < 0 ? 0 : dmg          // guard sólo contra negativos
```

Términos (líneas exactas):

| Término | Origen | Ref |
|---|---|---|
| `dmg_base_PJ` | `Attack.Value` del atacante | `PlayerComboDamage.cs:41` |
| `bonos_PJ` | `Attack.ModifiedValue - Attack.Value` (buffs/items) | `:42` |
| `combo_base` | `ComboDetectionResult.BaseDamage` del combo ganador | pasado por el caller |
| `multi_dmg_combo` | EV de los dados contribuyentes / EV(d6) | `:70`, `:87-93` |
| `ability_mult` | perilla por habilidad (Warrior = 1.0, ver §5) | param `abilityMultiplier` |
| `scratch_mult` | encantamientos Gemelo/Par-Impar | `:53`, `:59` |
| `bono_combo` | bonus plano de pasivas/encantamientos | `:52`, `:58` |

**Regla de oro** (`:16-24`, `:72`): `dmg_base_PJ`, `bonos_PJ` y `bono_combo` son
aditivos puros — **nunca se multiplican**. Sólo `combo_base` se escala. Por eso
el `Attack` base del PJ actúa como **piso duro** que no puede desaparecer aunque
el combo escale a 0.

**Bandera de bloqueo** (`:48-68`): si algún encantamiento marca
`BlockComboDamage`, la función retorna **0** antes de calcular nada. Es la única
salida a 0 dentro de esta función (además del clamp de negativos, que en la
práctica no se alcanza porque todos los términos son ≥ 0).

`multi_dmg_combo` (`:87-93`): promedia el **Expected Value del *tipo* de dado**
de los dados que formaron el combo (no el valor tirado), dividido por EV(d6)=3.5.

`Assets/Scripts/Rollgeon/Dice/DiceType.cs:79-95`:

| Dado | EV | multi vs d6 |
|---|---|---|
| D4 | 2.5 | 0.714 |
| D6 | 3.5 | 1.000 |
| D8 | 4.5 | 1.286 |
| D10 | 5.5 | 1.571 |
| D12 | 6.5 | 1.857 |
| D20 | 10.5 | 3.000 |

> **Punto clave**: `multi_dmg_combo` depende del **tipo** de dado, no del pip
> tirado. Un Par de 1-1 en D6 pesa igual que un Par de 6-6 en D6 (multi = 1.0).
> Los pips bajos **no** reducen el daño de un combo plano.

### 1.2 De dónde sale `combo_base` (detección + tabla de clase)

- El combo ganador se elige por **`Priority` descendente** en
  `ContractSheet.MatchBest` (`Assets/Scripts/Rollgeon/Heroes/ContractSheet.cs:166-197`).
  `Priority` default = `BaseDamage` del SO (`BaseComboSO.cs:147`); Generala
  override a `int.MaxValue`.
- `combo_base` = override de la tabla por clase si existe, si no el `BaseDamage`
  global del SO (`ContractSheet.cs:68-86`, `BaseComboSO.Detect` `:164-171`).
- **La tabla `BaseDamageTable` del Guerrero está VACÍA**
  (`Assets/Rollgeon/Classes/CH_Warrior.asset:79-85`, list count 0) ⇒ el Guerrero
  usa siempre el `BaseDamage` global del SO.

Bases globales autoradas (`Assets/Rollgeon/Combos/*.asset`):

| Combo | ComboId | `BaseDamage` | Nota |
|---|---|---|---|
| Higher Number | `combo.higher_number` | 10 + 4×hits | es un `Combo_SumaX` (X=4), Priority=5 (fallback) |
| Par | `combo.par` | 8 | |
| Doble Par | `combo.double_pair` | 15 | |
| Trio | `combo.trio` | 22 | |
| Fuerza Bruta | `combo.brute_force` | 30 | |
| Full House | `combo.full_house` | 35 | |
| Escalera | `combo.ladder` | 40 | |
| Poker | `combo.poker` | 55 | |
| Generala | `combo.generala` | 90 | Priority `int.MaxValue` |

`Combo_SumaX` (`Assets/Scripts/Rollgeon/Combos/Concretes/Combo_SumaX.cs:71-83`):
`base = _baseDamageConfigurable + X×hits`. Higher Number tiene
`_baseDamageConfigurable=10`, `X=4`, y `_baseDamage=5` (que sólo fija su
`Priority`, dejándolo como el combo de menor prioridad = fallback "hay un 4").

### 1.3 Camino sin combo (fallback)

`Assets/Scripts/Rollgeon/Effects/Concretes/EffDealDamage.cs:136-153`: si ningún
combo matchea, toma el **dado holdeado más alto** como `combo_base` y lo pasa por
la misma fórmula. Sin dados holdeados retorna 0 (y `ApplyEffect` corta sin pegar,
`:170-171`). Con dados, el piso sigue siendo `Attack` base + dado más alto.

### 1.4 Pipeline final

`Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs`

1. **Guard 0/negativo** (`:56-63`): si `BaseDamage <= 0` ⇒ FinalDamage 0.
2. Outgoing mult — placeholder, no-op (`:65-71`).
3. **Weakness** (`:73-83`): sólo **multiplica cuando `weakMult > 1`**; nunca
   reduce. No puede llevar a 0.
4. Incoming mult — placeholder, no-op (`:85-90`).
5. **Escudo** (`:92-111`): absorbe hasta `min(shield, damage)`. Es defensa del
   *target*, no un problema del build atacante; puede dejar `FinalDamage 0` si el
   escudo del enemigo cubre todo el golpe (marca `BlockedByShield`).
6. Commit a Health (`:113-153`).

**No hay clamp que fabrique 0 en el atacante** salvo el guard de la etapa 0.

---

## 2. ¿Puede un build/roll dar 0? — veredicto y condiciones exactas

Para el **Guerrero** (`Attack` base = 5, `Assets/Rollgeon/Classes/CH_Warrior.asset:3003`):

**Un único golpe NO puede dar 0.** Demostración:

```
total = 5 (Attack base, aditivo puro) + combo_base × multi (≥ 0) + ...
```

Como `Attack` base = 5 no se multiplica y todos los otros términos son ≥ 0, el
mínimo posible con cualquier dado holdeado es **5 + (dado más bajo)**. Un all-D6
con "un par de 1s" (ej. `[1,1,x,x,x]`) matchea **Par** ⇒ `5 + 8×1.0 = 13`. Sin
par, si hay un 4 matchea Higher Number ⇒ `5 + (10+4)×1 = 19`; si no, fallback al
dado más alto ⇒ `5 + max_die`. **Nunca 0.**

Las **únicas** formas de llegar a 0 son:

| # | Condición | ¿Fórmula o externo? | Ref |
|---|---|---|---|
| **A** | **Repetir el mismo combo dos turnos seguidos** | **Regla externa (removida acá, viva en develop)** | ver §3 |
| B | Encantamiento con `BlockComboDamage = true` | Diseño intencional | `PlayerComboDamage.cs:63-68` |
| C | No holdear ningún dado en un ataque ComboValue | Degenerado (input vacío) | `EffDealDamage.cs:139` |
| D | PJ sin stat `Attack` **y** sin combo **y** sin dados | Sólo si `dmg_base_PJ=0` | `PlayerComboDamage.cs:34-44` |
| E | Escudo del enemigo absorbe el golpe entero | Defensa del target, no del build | `DamagePipeline.cs:92-111` |

El caso **D** es el único "gap de fórmula" teórico, pero requiere un PJ con
`Attack` base 0 (el Guerrero es 5) **y** que no haya combo ni dados — no aplica al
build reportado.

**Conclusión: el 0 del playtest es el caso A (repeat-combo), no un agujero en la
fórmula del build all-D6.**

---

## 3. La regla repeat-combo (causa raíz del 0)

**Estado por rama:**

- En **`develop`**: `DamagePipeline.Resolve` y `Preview` tienen un guard
  `IsRepeatOfPreviousCombo(...)` que, si el `ComboId` del golpe coincide con el
  del turno anterior (vía `IComboLogService`), setea `FinalDamage = 0` y retorna.
  Es **global, sin UI y silencioso**.
- En **esta rama** (`sprint04/fix/boss-damage-zero-fix`, commit `558b493c`
  "fix(combat): remove global repeat-combo-deals-zero rule"): el guard y el helper
  `IsRepeatOfPreviousCombo` fueron **eliminados**; también se borraron el fixture
  `EffDealDamage_RepeatComboTests` y los casos repeat en `DamagePipelineTests`. El
  `Record` del combo en `CombatHandoffService`/`ComboLogService` se **mantiene**
  (lo consume el forbid-combo del jefe de piso 2, no el daño).

Código removido (del diff de `558b493c`, antes en `DamagePipeline.cs`):

```csharp
// ── 0b. Repeat-combo guard (no repetir el mismo combo 2 veces seguidas) ──
if (IsRepeatOfPreviousCombo(ctx.ComboId, alreadyRecordedThisAttack: true))
{
    ctx.FinalDamage = 0;
    ctx.WeaknessMultiplier = 1f;
    ctx.WasLethal = false;
    return ctx;
}
```

Por qué explica el playtest: un build all-D6 tiende a repetir el mismo combo
barato (Par / Doble Par) turno tras turno. El primer Par pega 13; el **segundo
Par consecutivo** caía en el guard y pegaba **0** — exactamente el síntoma
"combo deal 0 damage" y el ejemplo del commit ("doble par 4-4-3-3 dos veces").

---

## 4. Daño por arquetipo de build (Guerrero)

Supuestos, todos verificables en código/assets:

- Guerrero: `Attack` base = **5**, sin buffs (`bonos_PJ = 0`).
- `ability_mult = 1.0` y `scratch_mult = 1.0` (Base Attack y Special Attack del
  `CH_Warrior.asset` tienen `_comboMultiplier: 1`; sin encantamientos).
- `bono_combo = 0`.
- Sin weakness (×1.0) y sin escudo enemigo.
- Fórmula reducida: **`dmg = round(5 + combo_base × multi)`**.
- `multi` se calcula sobre los **dados contribuyentes** (los que forman el combo),
  no los 5. Para builds mono-tipo, `multi` es el de ese tipo.

### 4.1 Full D6 (multi = 1.000)

| Combo | combo_base | Daño |
|---|---|---|
| Sin combo (fallback, keep bajo=1) | 1 | **6** (peor caso) |
| Sin combo (fallback, keep=6) | 6 | 11 |
| Higher Number (un 4) | 14 | 19 |
| Par | 8 | 13 |
| Doble Par | 15 | 20 |
| Trio | 22 | 27 |
| Fuerza Bruta | 30 | 35 |
| Full House | 35 | 40 |
| Escalera | 40 | 45 |
| Poker | 55 | 60 |
| Generala | 90 | 95 |

Rango típico del build all-D6: **~13–27** (combos que salen seguido), pico 95
(Generala). **Piso 6.** El "0" reportado sólo aparece con la regla repeat (develop).

### 4.2 Full D4 (multi = 0.714)

D4 sólo tiene caras 1–4 ⇒ **Escalera imposible** (no hay 5 valores consecutivos);
X=4 de Higher Number sale seguido.

| Combo | combo_base | Daño (round) |
|---|---|---|
| Sin combo (keep=4) | 4 | 8 |
| Higher Number (un 4) | 14 | 15 |
| Par | 8 | 11 |
| Doble Par | 15 | 16 |
| Trio | 22 | 21 |
| Fuerza Bruta | 30 | 26 |
| Full House | 35 | 30 |
| Poker | 55 | 44 |
| Generala | 90 | 69 |

Rango típico: **~11–21**; pico 69. **Piso ~8.** El D4 pega ~29% menos por combo
que el D6, pero es mucho más fácil de agrupar (menos caras ⇒ más pares/tríos), así
que su DPS efectivo sube por frecuencia.

### 4.3 Mixto D6/D4

`multi` = promedio de EV de los dados contribuyentes / 3.5. Ejemplos:

- Par con 1×D6 + 1×D4 → EV (3.5+2.5)/2 = 3.0 → multi 0.857 → `5 + 8×0.857 = 12`.
- Trio con 2×D6 + 1×D4 → EV 3.17 → multi 0.905 → `5 + 22×0.905 = 25`.
- Full House (3+2, mezcla ~50/50) → multi ≈ 0.86 → `5 + 35×0.86 ≈ 35`.

Regla práctica: cada combo cae **entre su valor D4 y su valor D6** según cuántos
D6 entren al set ganador. Rango típico **~12–26**, pico Generala ~69–95 según
composición.

### 4.4 Mixto D20/D6/D4

`MaxPerBag(D20) = 1` (`DiceType.cs:53`) ⇒ **como mucho un D20 por bolsa**. El D20
pesa fuerte (multi 3.0) pero **sólo cuando entra al combo ganador**:

- Par donde el D20 y un D6 muestran el mismo pip → EV (10.5+3.5)/2 = 7.0 →
  multi 2.0 → `5 + 8×2.0 = 21`.
- Higher Number si el 4 lo muestra el D20 → base 14 × 3.0 → `5 + 42 = 47`.
- Combos que **no** incluyen al D20 (el D20 quedó fuera del set) → igual que el
  build base D6/D4 correspondiente.

Rango: muy variable, **~13 hasta ~50+** en combos chicos que capturan al D20;
el D20 es un multiplicador de varianza, no un piso. Su mayor valor está en
SumaX/Higher Number y en subir el EV promedio de sets que lo incluyan.

---

## 5. Recomendaciones

1. **Confirmar que el fix de esta rama (remover repeat-combo) llegue a `develop`.**
   Es la causa directa del 0 reportado y hoy sigue activo en `develop`. Es el
   punto #1.
2. **Si se quiere conservar algún desincentivo a spamear el mismo combo**, que
   **no sea daño 0 y silencioso**: usar daño decreciente (ej. ×0.75 al repetir)
   con feedback de UI explícito, o moverlo a un sistema visible (fatiga de combo).
   Un 0 sin telegrafía se lee como bug.
3. **Piso de daño explícito (defensivo).** Aunque la fórmula del Guerrero ya tiene
   piso natural (`Attack` base), conviene un floor duro post-pipeline
   `FinalDamage = max(1, FinalDamage)` para ataques del jugador con combo válido y
   sin bloqueo intencional, para blindar contra futuras clases con `Attack` base 0
   (caso D) o regresiones.
4. **Auditar clases nuevas con `Attack` base 0.** El único gap de fórmula (caso D)
   depende de eso; documentar que toda clase jugable debe tener `Attack` base ≥ 1.
5. **Distinguir el 0 de escudo (caso E)** en la UI: `BlockedByShield` ya existe en
   el payload (`DamagePipeline.cs:111`) — asegurarse de que la UI muestre "escudo"
   y no un "0" pelado que se confunde con bug.

---

## Anexo — Archivos citados

- `Assets/Scripts/Rollgeon/Combat/Damage/PlayerComboDamage.cs`
- `Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs`
- `Assets/Scripts/Rollgeon/Effects/Concretes/EffDealDamage.cs`
- `Assets/Scripts/Rollgeon/Heroes/ContractSheet.cs`
- `Assets/Scripts/Rollgeon/Heroes/ContractWarriorFactory.cs`
- `Assets/Scripts/Rollgeon/Combos/BaseComboSO.cs`
- `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_SumaX.cs`
- `Assets/Scripts/Rollgeon/Dice/DiceType.cs`
- `Assets/Rollgeon/Classes/CH_Warrior.asset`
- `Assets/Rollgeon/Combos/Combo_*.asset`
- commit `558b493c` — `fix(combat): remove global repeat-combo-deals-zero rule`
