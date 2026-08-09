# Plan de Acción — Sprint actual (2026-07-09)

> Rama: `feature/damage-formula-v2` · Autor del plan: Claude + Sebastián
> Fuentes: spec de Escudo de Santi Bocco (2026-07-08), tareas asignadas 2026-07-09,
> análisis del código en `823e155..17cb4ed`.

> **NOTA 2026-08-07 (Spec Escudo v3).** La separación total escudo/daño que define
> este documento (§3, pregunta abierta #5: sin `abilityMultiplier`, sin scratch, con
> `ESCUDO_CAP`) **se revirtió por decisión de diseño**: el escudo ahora se resuelve con
> la MISMA fórmula que el daño (`PlayerComboShield` delega en `PlayerComboDamage.Resolve`),
> afectado por Attack, ability multiplier y todos los canales de scratch. El cap se
> eliminó — el freno anti-inmunidad pasa a ser el reset de escudo por turno
> (`ShieldResetHandler`) más el rebalance ×10 del daño enemigo (escala de vida 100).
> Lo que SÍ sigue vigente de esta spec: la base sale de la `ShieldBaseTable` (nunca de
> la tabla de daño — BUG-021) y sin entrada la clase no genera escudo con ese combo.

---

## 0. Resumen ejecutivo

Hay **cinco frentes de trabajo** con una regla que gobierna todo: **el PR de
`feature/damage-formula-v2` no se toca hasta recibir el feedback de Maiine y
Bocco** (prometido para la noche del 09/07 — día de examen de ambos; si no
llega, perseguirlos).

| # | Frente | Estado | Bloqueado por |
|---|--------|--------|---------------|
| WS1 | Resolver feedback del PR | ⏸ Esperando feedback | Maiine + Bocco (esta noche) |
| WS2 | Sistema de Escudo v2 (spec Bocco) | 🎨 Diseño hoy, código post-feedback | WS1 + validación de Maine (tabla) |
| WS3 | Balance escalonado 3 pisos | 📊 Prep de data hoy, sesión con Bocco después | Disponibilidad de Bocco |
| WS4 | Análisis planteo del profesor (defensa solo tras atacar) | ✍️ Se hace hoy, sin dependencias | — |
| WS5 | Bug testing + registro de pulido | 🔁 Continuo, transversal | Formato del sheet |

La secuencia del día: WS4 (análisis, cero riesgo) → WS2-diseño (tabla propuesta
para Maine) → WS3-prep (data de balance) → a la noche WS1 (feedback) → recién
después WS2-código.

---

## 1. Estado actual del proyecto (análisis)

### 1.1 La fórmula de daño v2 — lo que ya funciona

El corazón del PR es `PlayerComboDamage.Resolve()`
(`Assets/Scripts/Rollgeon/Combat/Damage/PlayerComboDamage.cs`):

```
dmg = dmg_base_PJ + bonos_PJ + (daño_combo_base × multi_dmg_combo × abilityMult × scratchMult) + bono_combo
multi_dmg_combo = EV_promedio(dados_contribuyentes) / 3.5
```

Piezas ya construidas y testeadas que **se reusan tal cual** para el escudo:

- `DiceType.ExpectedValue()` + `DiceTypeExt.BaselineExpectedValue = 3.5f`
  (`Dice/DiceType.cs:75-95`).
- `PlayerComboDamage.ComputeMultiDmgCombo()` — público, ya lo consume el
  preview de `DiceZoneView`.
- `ComboDetectionResult.ContributingIndices` — qué dados formaron el combo
  ganador; los concretos de subconjunto (Par, Trío, Poker, DoblePar, SumaX)
  ya overridean `GetContributingIndices`.
- `ContractSheet.BaseDamageTable` (`Heroes/ContractSheet.cs:50`) — tabla por
  clase que overridea el base plano del SO, con `GetBaseDamageOverride()` /
  `GetBaseDamage()`. Entró hoy con el pull, junto con
  `HeroClassEditorWindow` (+200 líneas) para editarla y
  `ContractSheetBaseDamageTests` (98 líneas de cobertura).
- `BaseComboSO.Detect(dice, flatBaseOverride)` — el override reemplaza solo la
  parte plana; SumaX suma su parte variable encima.

**Implicancia clave:** el patrón "tabla por clase embebida en ContractSheet +
editor window + tests espejo" ya existe para daño. El escudo es una segunda
instancia del mismo patrón, no arquitectura nueva.

### 1.2 El bug del escudo — causa raíz confirmada en código

`EffAddShield` (`Effects/Concretes/EffAddShield.cs:78-79`), modo
`DamageSource.ComboValue`:

```csharp
DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
    => Mathf.RoundToInt(combo.BaseDamage * _comboMultiplier),
```

Tres problemas, exactamente los que marca la spec de Bocco:

1. **Reusa `combo.BaseDamage`** — la tabla de ATAQUE. Con los valores actuales
   (ver §1.4) una Generala da escudo base 90. Contra enemigos que pegan 1-2 de
   `BaseAttack`, cualquier combo medio deja al jugador inmune varios turnos.
   Ese es el "escudo trivial" que se venía arrastrando.
2. **No aplica `multi_dmg_combo`** — el escudo ignora qué dados se usaron.
3. **No tiene cap** — no existe tope duro.

El parche actual (`_comboMultiplier` como float de ajuste) es un curita sobre
la tabla equivocada: bajar el multiplicador rompe los combos chicos antes de
arreglar los grandes. La spec lo resuelve de raíz: **tabla propia**.

### 1.3 El flujo ataque→escudo — punto a verificar

- Los ataques del guerrero envuelven `EffDealDamage` en un `EffChain` con
  fases (daño + escudo) — comentario en `HeroActionBehavior.cs:235-236`.
- `CombatHandoffService` orquesta las fases; la fase escudo es self-cast y el
  chain comparte **un solo** `EffectContext` cuyo `ComboResult` se setea una
  vez (`CombatHandoffService.cs:876`).

⚠️ **Tensión con la spec:** Bocco dice que `multi_dmg_combo` del escudo se
calcula "sobre los dados de esta tirada de escudo, **no la de ataque**". Hoy
el chain no tiene tirada propia para la fase escudo — hereda el roll del
ataque. Esto es la **pregunta #1 para Bocco/Maine** antes de codear:

- **(a)** ¿Existe/va a existir una tirada de escudo separada (post-ataque,
  coherente con la tarea 3: "si no atacás, no podés tirar para escudo")? →
  la fase escudo del chain debe disparar su propio `ActionRoll` y detectar
  combo sobre esos dados.
- **(b)** ¿O "tirada de escudo" refiere al mismo roll cuando la acción es
  defensiva? → el cálculo es directo sobre el `ComboResult` existente.

El diseño de WS2 cubre ambos casos (el servicio de cálculo es idéntico; cambia
solo de dónde vienen los dados).

### 1.4 Números actuales (para balance y para la tabla de escudo)

Tabla de ataque global (assets en `Assets/Rollgeon/Combos/`):

| Combo | `_baseDamage` ataque | Escudo si se reusa (bug) vs CAP 8 |
|---|---|---|
| Par | 8 | 8 → justo en cap |
| Suma X | 10 (+X×hits) | 10+ → sobre cap |
| Doble Par | 15 | ~2× cap |
| Trío | 28 | 3.5× cap |
| Full House | 35 | 4.4× cap |
| Escalera | 40 | 5× cap |
| Poker | 55 | ~7× cap |
| Generala | 90 | 11× cap — inmunidad multi-turno |

Enemigos (assets actualizados hoy): `ED_Healer` BaseAttack 2, `ED_MeleeCard`
BaseAttack 2, `ED_Ranged` BaseAttack 1. **Un escudo de 90 absorbe ~45 turnos
del melee.** La escala del problema justifica el cap duro además de la tabla.

Progresión de pisos: `FloorProgressionService` (#158) ya orquesta la
transición multi-piso desde `RunController` con seeds derivados por piso —
la infraestructura para "experiencia escalonada" existe; lo que falta es la
curva de números.

### 1.5 Infraestructura disponible

- **Tests**: EditMode, patrón establecido (`PlayerComboDamageTests`,
  `ContractSheetBaseDamageTests`, `EffAddShieldTests` ya existen).
- **Editor tooling**: `HeroClassEditorWindow` recién revisada
  (`docs/tools/hero-class-editor-review.md`).
- **Unity MCP**: conectado (verificado hoy). `execute_code` roto — usar
  `[MenuItem]` + `execute_menu_item` para mutar el editor.
- **Bloqueo operativo**: `gh` sin autenticar en esta máquina → no puedo leer
  el PR ni su feedback por CLI. **Acción: correr `gh auth login`** o pegar el
  feedback a mano en la sesión.

---

## 2. WS1 — Resolver el PR (gate de todo lo demás)

**Regla:** no implementar NADA del feedback hasta tenerlo completo de ambos
revisores. Bocco lo manda esta noche; si a la noche no está, perseguir a los
dos (hoy rendían, después del examen están libres).

### Protocolo al recibir el feedback

1. **Triage** de cada comentario en una matriz:
   - ✅ *Aceptar directo* — fix mecánico, sin ambigüedad → se implementa.
   - 💬 *Discutir* — contradice la spec, otro comentario, o una decisión de
     arquitectura ya tomada → responder en el PR con argumento, no codear.
   - ⏭ *Diferir* — válido pero fuera de scope del PR → anotar como tarea/issue.
2. **Cross-check** Maiine vs Bocco: si se contradicen entre sí, escalar antes
   de tocar código (probablemente en la sesión de balance de WS3).
3. **Implementar** en commits atómicos conventional-commit, un scope por
   commit, con tests para cada fix de comportamiento.
4. **Verificar**: suite EditMode completa verde + smoke en editor vía MCP.
5. **No pushear sin autorización explícita** (regla del repo).

### Contingencia

- Feedback parcial (solo uno de los dos responde): triage y preparación de lo
  de ese revisor, implementación recién con el segundo — salvo fixes triviales
  no conflictivos.
- Sin feedback a la noche: mensaje de seguimiento a ambos; mientras tanto se
  avanza WS2-diseño/WS3/WS4 que no tocan el PR.

---

## 3. WS2 — Sistema de Escudo v2 (spec de Bocco)

### 3.1 La spec, formalizada

```
Escudo = min( escudo_combo_base × multi_dmg_combo , ESCUDO_CAP )

escudo_combo_base : int — tabla fija por combo Y por clase,
                    independiente de daño_combo_base (NO derivar, NO reusar)
multi_dmg_combo   : EV_promedio(dados de la tirada de ESCUDO) / 3.5
ESCUDO_CAP        : 8 (sugerido) — tope duro post-multiplicación
```

Racional del cap: ni una Generala perfecta con d20s debe dar inmunidad
multi-turno; mantiene la tensión en el mejor roll posible.

### 3.2 Preguntas abiertas ANTES de codear (para Bocco y Maine)

| # | Pregunta | Para | Impacto |
|---|---|---|---|
| 1 | ¿La fase escudo tiene tirada propia o usa el roll del ataque? (§1.3) | Bocco | Define si hay que tocar el chain de `CombatHandoffService` |
| 2 | Valores de la tabla `escudo_combo_base` por combo × clase (propuesta en §3.3) | Maine + Bocco | Solo data, no bloquea el código |
| 3 | Fallback si una clase no define entrada para un combo: ¿0 (sin escudo) o un default global chico? Propongo **0** — hace explícito el diseño por clase | Maine | Firma de `GetShieldBase` |
| 4 | ¿`ESCUDO_CAP` es global o futura variable por clase/piso? Propongo constante global ahora, sin sobre-ingeniería | Bocco | Dónde vive la constante |
| 5 | ¿El escudo respeta `abilityMultiplier`/scratch de encantamientos como el daño? La spec no los menciona → propongo **no** (fórmula limpia, spec literal) | Bocco | Firma de `Resolve` |

### 3.3 Propuesta de tabla inicial (para discutir con Maine)

Calibrada para que con dados d6 (multi = 1.0) el escudo ≈ 1-4 golpes enemigos
del piso 1 (BaseAttack 1-2), y el cap solo lo alcancen combos altos con dados
mejorados (multi > 1):

| Combo | escudo_base propuesto | Escudo con d6 (×1.0) | Con d10s (×~1.57) | Con d20s (×3.0) |
|---|---|---|---|---|
| Par | 2 | 2 | 3 | 6 |
| Suma X | 2 | 2 | 3 | 6 |
| Doble Par | 3 | 3 | 5 | 8 (cap) |
| Trío | 4 | 4 | 6 | 8 (cap) |
| Full House | 5 | 5 | 8 (cap) | 8 (cap) |
| Escalera | 5 | 5 | 8 (cap) | 8 (cap) |
| Poker | 6 | 6 | 8 (cap) | 8 (cap) |
| Generala | 8 | 8 (cap) | 8 (cap) | 8 (cap) |

Propiedades deseables que cumple: progresión monótona con la dificultad del
combo, la Generala toca el cap incluso con d6 (se siente "máximo"), y el
upgrade de dados sigue importando para los combos chicos sin romper el techo.

### 3.4 Diseño técnico (espejo del patrón de daño v2)

**Capa de datos** — `ContractSheet`:

```csharp
// Nuevo, junto a BaseDamageTable — mismo patrón struct plano:
public List<ComboShieldBaseEntry> ShieldBaseTable = new();

public int GetShieldBase(string comboId)   // sin entrada → 0 (pregunta #3)

[Serializable] public struct ComboShieldBaseEntry {
    [ValueDropdown(...)] public string ComboId;   // mismo dropdown que daño
    [Range(0, 50)]       public int ShieldBase;
}
```

- Copia por valor en `ContractSheet.Instantiate()` (igual que
  `BaseDamageTable` — mutar la run no toca el asset).
- **No** se agrega nada a `BaseComboSO`: la spec dice tabla por clase; el SO
  global no necesita un "shield base global" (fallback = 0).

**Capa de cálculo** — nuevo `Combat/Damage/PlayerComboShield.cs`, estático y
puro como su gemelo:

```csharp
public static class PlayerComboShield
{
    public const int ShieldCap = 8;   // ESCUDO_CAP — spec Bocco 2026-07-08

    public static int Resolve(int shieldBase, IReadOnlyList<DiceType> contributingDice)
    {
        float multi = PlayerComboDamage.ComputeMultiDmgCombo(contributingDice);
        return Mathf.Min(Mathf.RoundToInt(shieldBase * multi), ShieldCap);
    }
}
```

Sin `Attack`, sin `bono_combo`, sin scratch — separación total de la fórmula
de ataque, que es el mandato central de la spec.

**Capa de efecto** — `EffAddShield`:

- El path `DamageSource.ComboValue` pasa a resolver:
  `PlayerComboShield.Resolve(sheet.GetShieldBase(combo.ComboId), contributingDice)`.
- `_comboMultiplier` queda **deprecado** (se elimina el campo o se marca
  obsoleto según prefiera Maiine para no romper assets serializados — decisión
  de triage en WS1).
- `BuildTooltip()` refleja la fórmula nueva ("Escudo: base × multi, máx 8").
- Cómo llega `contributingDice`: mismo mecanismo con el que
  `EffDealDamage`/`PlayerComboDamage` resuelve el `DiceType` contribuyente
  (commit `c7ff77e2`) — se reusa; si la respuesta a la pregunta #1 es (a),
  el `EffectContext` de la fase escudo carga el `ComboResult` de la tirada
  de escudo en lugar del heredado.

**Capa editor** — `HeroClassEditorWindow`: columna/sección "Escudo" espejo de
la de base damage (el review de hoy en `docs/tools/hero-class-editor-review.md`
marca los puntos a respetar).

**Capa UI**:

- Tooltip de `EffAddShield` actualizado.
- Preview en `DiceZoneView`/`DamageFormulaView` del escudo computado, igual
  que se hizo con `multi_dmg_combo` en `985bb87a`.
- `ShieldBarView` ya existe — sin cambios, solo recibe números sanos.

**Tests EditMode** (espejo de `PlayerComboDamageTests` + casos propios):

1. `Resolve_AllD6_UsesBaseTimesOne` — multi 1.0.
2. `Resolve_AllD20_TriplesBase_ButCapsAt8` — el cap corta.
3. `Resolve_MixedDice_AveragesEV`.
4. `Resolve_NoContributingDice_NeutralMultiplier`.
5. `Cap_AppliesAfterMultiplication_NotBefore` — orden de operaciones.
6. `ShieldTable_IsIndependent_FromDamageTable` — cambiar
   `BaseDamageTable` NO afecta el escudo (test anti-regresión de la causa
   raíz del bug).
7. `GetShieldBase_MissingEntry_ReturnsZero` (o el fallback que defina Maine).
8. `ContractSheet.Instantiate_CopiesShieldTable_ByValue`.

### 3.5 Secuencia de implementación (post-feedback WS1)

| Paso | Qué | Depende de |
|---|---|---|
| 0 | Respuestas a preguntas #1-#5 (charla Bocco/Maine) | — |
| 1 | `ComboShieldBaseEntry` + `ShieldBaseTable` + `GetShieldBase` + tests de tabla | 0 |
| 2 | `PlayerComboShield` + tests de fórmula | — (paralelo a 1) |
| 3 | Rewire `EffAddShield.ComboValue` + tests de efecto | 1, 2 |
| 4 | (Solo si pregunta #1 = (a)) tirada propia en fase escudo del chain | 0, 3 |
| 5 | `HeroClassEditorWindow` columna escudo | 1 |
| 6 | Cargar tabla acordada en `CH_Warrior.asset` (MCP o a mano vía docs/setup) | 5, Maine |
| 7 | Tooltip + preview UI | 3 |
| 8 | Suite completa + smoke en editor + pasada de balance rápida vs piso 1 | todo |

Commits sugeridos: `feat(heroes): add per-class shield base table to ContractSheet`,
`feat(combat): add PlayerComboShield formula with hard cap`,
`fix(effects): stop EffAddShield from reusing attack damage table`,
`feat(ui): shield formula preview`, etc. — un scope por commit.

---

## 4. WS3 — Balance escalonado entre los 3 pisos (con Bocco)

**Objetivo verificable:** que las curvas de daño/HP/economía produzcan una
dificultad creciente y perceptible piso a piso — "experiencia escalonada".

### Método (prep se hace hoy, sin Bocco)

1. **Extracción de data actual**: volcar a una planilla los stats de todos los
   `ED_*.asset` (HP, BaseAttack, rango, comportamiento), la config de
   `FloorLayoutSO`/`FloorProgressionService` (qué enemigos aparecen por piso,
   cantidad), y la tabla de daño del jugador (§1.4).
2. **Modelo de presión por piso**: para cada piso, calcular
   - *TTK jugador→enemigo*: EV de daño por turno del jugador (fórmula v2 con
     dados esperables en ese piso) vs HP enemigo.
   - *TTK enemigo→jugador*: daño enemigo por turno × cantidad vs HP+escudo
     esperado del jugador (con la fórmula NUEVA de escudo — WS2 y WS3 se
     alimentan mutuamente: el cap 8 cambia la supervivencia esperada).
3. **Curva objetivo** a proponerle a Bocco (punto de partida de la charla, no
   verdad revelada): piso 1 = enseñanza, TTK enemigo alto y perdón; piso 2 =
   presión real, el jugador necesita usar bien holds/rerolls; piso 3 = exige
   dados mejorados y decisiones de riesgo.
4. **Sesión con Bocco**: contrastar modelo vs su intención de diseño, acordar
   números, anotar TODO en la planilla compartida.
5. **Aplicar + playtest**: tocar assets (no código), corrida completa de los
   3 pisos, registrar sensaciones y outliers en el sheet de WS5.

Herramienta de apoyo: skill `/balance-check` o `/game-analysis` sobre los
assets para detectar outliers automáticamente antes de la sesión.

---

## 5. WS4 — Planteo del profesor: "defensa solo accesible tras atacar"

**El planteo:** no existe acción de defensa pura; si el jugador no ataca, no
puede tirar para generar escudo. ¿Es una problemática real? ¿Una habilidad
defensiva sumaría al apartado táctico?

**Entregable:** documento PAS corto (`docs/design/pas-defensa-pura.md`) con
recomendación explícita — aceptar o descartar con argumentos. Estructura:

1. **Problema** — enunciado del profe + en qué estados de juego se sentiría la
   falta (jugador con 2 HP, sin buen roll ofensivo posible, rodeado).
2. **Análisis**
   - *A favor de agregar defensa pura*: más decisiones por turno; salida
     digna a un mal roll; espacio de build defensivo (encantamientos/clases
     futuras); lo pide un evaluador externo (señal de legibilidad).
   - *En contra*: riesgo de **turtling** (defender en loop hasta el roll
     perfecto — anti-tensión, el problema que el cap 8 justamente combate);
     el diseño actual acopla riesgo y recompensa (atacás para ganar derecho a
     defenderte — identidad del juego, coherente con push-your-luck de dados);
     costo de contenido/UI/balance en semanas finales de tesis.
   - *Dato duro*: con ESCUDO_CAP=8 y enemigos pegando 1-2, una defensa pura
     spammeable sería casi-inmunidad — cualquier versión aceptada necesita un
     costo real (energía alta, cooldown, o escudo que no stackea).
3. **Opciones**
   - **A. Descarte argumentado**: mantener el diseño; documentar el porqué
     (identidad agresiva, anti-turtling) — respuesta formal al profe.
   - **B. Aceptación acotada**: acción "Guardia" con tirada propia, costo de
     energía alto y regla no-stack (reemplaza, no suma) — mantiene tensión.
   - **C. Híbrido barato**: sin acción nueva; un encantamiento/upgrade raro
     otorga defensa pura — lo convierte en decisión de build, no de turno.
4. **Recomendación** — se define al final del análisis, con el argumento
   táctico Y el de scope (fecha de entrega de la tesis) explícitos.

Este doc se escribe HOY (no depende de nadie) y se valida con Bocco/Maine en
la misma conversación del feedback.

---

## 6. WS5 — Bug testing y registro de pulido (transversal)

**Mandato:** todo bug Y todo detalle de mejora/pulido se anota — en el sheet
compartido y en su ventana correspondiente.

### Protocolo

- **Cuándo**: durante cualquier sesión de trabajo — playtest de balance (WS3),
  smoke tests del PR (WS1), verificación del escudo (WS2).
- **Formato mínimo por entrada**: ID (`BUG-0NN` siguiendo la serie existente —
  el repo ya referencia BUG-015/016/018), severidad, pasos de reproducción,
  esperado vs observado, build/branch, screenshot si aplica. Pulido:
  descripción + impacto percibido + esfuerzo estimado.
- **Fuentes automáticas**: `read_console` vía MCP tras cada smoke (errores y
  warnings van directo al registro), resultados de suite EditMode.
- **Pendiente del usuario**: pasarme el link/formato del sheet y qué es "su
  ventana correspondiente" para que las entradas salgan listas para pegar
  (o cargarlas directo vía Chrome si el sheet es accesible).

---

## 7. Cronograma propuesto

| Cuándo | Qué | Frente |
|---|---|---|
| **Hoy (día)** | Doc PAS defensa pura, redactado y listo para validar | WS4 |
| **Hoy (día)** | Diseño técnico escudo cerrado + tabla propuesta + 5 preguntas empaquetadas para Bocco/Maine | WS2 |
| **Hoy (día)** | Extracción de data de balance + modelo TTK por piso | WS3 |
| **Hoy (tarde)** | `gh auth login` (usuario) para poder leer el PR | WS1 |
| **Hoy (noche)** | Llega feedback → triage + implementación. Si no llega → perseguir a Maiine y Bocco | WS1 |
| **Post-feedback** | Implementación escudo (secuencia §3.5) en la misma rama o rama nueva según prefiera el equipo | WS2 |
| **Post-examen Bocco** | Sesión de balance con modelo TTK sobre la mesa | WS3 |
| **Siempre** | Registro de bugs/pulido | WS5 |

## 8. Riesgos y contingencias

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Feedback no llega esta noche | Media | Perseguir activamente; todo lo no-PR sigue avanzando (WS2-diseño, WS3-prep, WS4) |
| Feedback de Maiine y Bocco se contradicen | Media | Matriz de triage con columna "conflicto" → resolver en llamada, no en código |
| La spec de escudo requiere tirada propia (pregunta #1 = (a)) y el chain no la soporta | Media | Paso 4 de §3.5 aislado; el resto del escudo no depende de eso y se entrega igual |
| Deprecar `_comboMultiplier` rompe assets serializados (Odin) | Baja | Mantener el campo marcado obsoleto un sprint; migración de assets vía editor script |
| Balance: cap 8 hace el escudo irrelevante en piso 3 | Media | El modelo TTK de WS3 lo detecta antes del playtest; el cap es constante → ajuste barato |
| Scope creep con la propuesta del profe | Alta | WS4 entrega ANÁLISIS con recomendación, no implementación; cualquier código sale de una decisión del equipo con costo explícito |

## 9. Criterios de done

- **WS1**: todos los comentarios del PR con disposición (implementado /
  respondido / diferido con tarea), suite verde, aprobación de ambos, merge
  autorizado por el usuario.
- **WS2**: fórmula nueva en runtime con los 8 tests de §3.4 verdes, tabla
  cargada con valores acordados con Maine, escudo jamás > 8, preview en UI,
  y el test anti-regresión de independencia ataque/escudo pasando.
- **WS3**: planilla con curva acordada firmada por Bocco, assets ajustados,
  una run completa de 3 pisos jugada con sensación escalonada registrada.
- **WS4**: doc PAS entregado con recomendación única y argumentos; respuesta
  al profesor redactada.
- **WS5**: cero bugs/pulidos observados sin registrar al cierre de cada sesión.
