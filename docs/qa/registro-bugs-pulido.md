# Registro de bugs y pulido — staging local

> **WS5.** Entradas listas para copiar al sheet compartido y a su ventana
> correspondiente. IDs siguen la serie existente del repo (última referencia
> encontrada en código: BUG-018). Formato: ID · Severidad · Área · Repro ·
> Esperado vs Observado · Branch.

## Bugs

### BUG-019 — Escudo reusa la tabla de daño de ataque (causa raíz "escudo trivial")
- **Severidad**: Alta (balance-breaking)
- **Área**: Combat / Effects
- **Repro**: combatir con el Warrior y resolver cualquier combo alto; observar el escudo otorgado por la fase escudo del chain.
- **Esperado**: escudo acotado (spec v2: `min(base_escudo × multi, 8)`).
- **Observado**: escudo = `BaseDamage` del combo de ATAQUE × multiplier (`EffAddShield.cs:78-79`). Generala → 90 de escudo ≈ 45 turnos de inmunidad vs Melee (Attack 2).
- **Branch**: `feature/damage-formula-v2` (pre-fix). **Estado**: **FIXED 09/07** — `PlayerComboShield` (min(tabla × multi, 8)) + rewire de `EffAddShield` + tabla seedeada en CH_Warrior. 1743/1743 tests EditMode verdes, incl. regresión `ComboValue_UsesShieldTable_IgnoresAttackBaseDamage`. Sin commitear aún.

### BUG-020 — Fase escudo del chain hereda el ComboResult del ataque
- **Severidad**: Media (diseño divergente de spec)
- **Área**: Combat / Handoff
- **Repro**: atacar con acción que encadena daño+escudo; la fase escudo no abre tirada propia.
- **Esperado** (spec Bocco 08/07 + decisión de equipo 09/07, lectura A): la fase escudo dispara su propia tirada y el multi se calcula sobre esos dados.
- **Observado**: un solo `ComboResult` compartido para todo el chain (`CombatHandoffService.cs:876`).
- **Branch**: `feature/damage-formula-v2`. **Estado**: solución definida (flow `IActionRollEffect` existente, patrón Curarse), pendiente confirmación de Bocco.

## Pulido / mejoras

### PUL-001 — `ED_Boss.asset` (100 HP / Atk 2) aparenta ser placeholder
Los tres bosses nominales (Sunken_Grand, Security_Boss, GeneralDirector) tienen 200 HP. Confirmar si `ED_Boss` se referencia en algún layout; si no, eliminarlo para evitar que un pool lo agarre por accidente.

### PUL-002 — `ExtraTiers` vacío en Ranged, Healer y los 3 bosses
Solo `ED_MeleeCardEnemy` define T2. El sistema de tiers está construido pero desaprovechado — es la perilla natural para la escalada de pisos 2-3 (ver `docs/planning/balance-modelo-3-pisos.md` §3.2).

### PUL-003 — HP de bosses plano entre pisos (200/200/200)
La escalada entre bosses es solo por Attack. Con upgrades de dados del jugador, el boss de piso 3 puede caer más rápido que el de piso 1. Revisar curva en sesión de balance.

### PUL-004 — Nada comunica que el escudo se gana atacando
Origen del planteo del profesor (ver `docs/design/pas-defensa-pura.md`): la regla "defensa solo tras atacar" es invisible para el jugador. Fix barato: tooltip/onboarding con la regla explícita.

---

**Pendiente del usuario**: link/columnas del sheet compartido y qué ventana
corresponde a cada categoría, para migrar estas entradas y las futuras.
