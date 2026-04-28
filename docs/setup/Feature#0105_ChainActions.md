# Setup — Feature#0105: Chain Actions (EffChain + Multi-Phase)

Acciones de combate multi-fase. Un `EffChain` contiene N `ChainPhase`, cada una con
sus propios effects y preconditions. El jugador tira dados por fase, con presupuesto
compartido de rerolls. Puede "pasar" para terminar el chain antes.

TECHNICAL.md §8.10.

---

## 1. Verificar compilacion

1. Abrir Unity. Esperar recompilacion.
2. Verificar 0 errores en la consola.
3. **Test Runner > EditMode**, correr:
   - `Rollgeon.Effects.Tests.EffChainTests` — todos deben pasar.

---

## 2. Registracion automatica (ya hecha en codigo)

No se requiere wiring manual de servicios:

- `CombatHandoffService.WireCombatHUDDelegates()` detecta chains automaticamente
  via `behavior.FindChainEffect()`.
- El budget de rerolls por fase usa `IRerollBudgetService` (ya registrado).
- Eventos `OnChainPhaseStarted` y `OnChainCompleted` se disparan automaticamente.

---

## 3. Crear behavior con EffChain en el hero class SO

1. Abrir el SO de la clase (ej. `Assets/Rollgeon/Classes/CH_Warrior.asset`).
2. En la lista de **Behaviors**, agregar un nuevo `HeroActionBehavior`.
3. Configurar:

| Campo | Valor | Nota |
|---|---|---|
| `ActionName` | `"Combo Chain"` | Label visible en el HUD |
| `IsBaseBehavior` | `true` o `false` | Segun slot disponible |
| `EnergyCost` | `2` | Ajustar — el chain consume mas energia |
| `NeedsDiceRoll` | `true` | Cada fase usa dados |
| `FreeRollCount` | `3` | Compartido entre todas las fases |
| `AllowsReroll` | `true` | |
| `AllowsEnergyReroll` | `true` | |

4. En **Effects** (lista de `EffectData`), crear un grupo:
   - **PreConditions**: vacio (o condicion de energia minima).
   - **Effects[0]**: `EffChain`
     - Configurar **Phases** (lista de `ChainPhase`):

### Ejemplo: Chain de 3 fases (Ataque → Dano → Shield)

**Phase 0** — "Strike":
- `Label` = `"Strike"`
- `Effects.Effects[0]` = `EffDealDamage` (source = ComboValue, multiplier = 1.0)
- `Effects.Effects[1]` = `EffPlayFeedback` (feedback de ataque)

**Phase 1** — "Follow-up":
- `Label` = `"Follow-up"`
- `Effects.Effects[0]` = `EffDealDamage` (source = ComboValue, multiplier = 0.5)

**Phase 2** — "Guard":
- `Label` = `"Guard"`
- `Effects.Effects[0]` = `EffAddShield` (source = ComboValue, multiplier = 1.0)

> Cada fase puede tener sus propias **PreConditions**. Si las preconditions de una fase
> fallan, esa fase se salta (soft skip) pero el chain continua. Si un efecto retorna
> `false`, el chain se corta (hard stop, short-circuit §8.8).

---

## 4. Wiring UI — ChainPhaseIndicatorView

`ChainPhaseIndicatorView` muestra "Phase X/Y" mientras un chain esta activo.

1. En el prefab del **CombatHUD**, crear un child GameObject:
   - Nombre: `ChainPhaseIndicator`
   - Agregar componente `ChainPhaseIndicatorView`.
2. Crear los widgets hijos:
   - **TextMeshProUGUI** para el label → arrastrar a `_text`.
   - El `_container` es el GameObject raiz → arrastrar a `_container`.
     Se auto-oculta cuando no hay chain activo.
3. En el `CombatHUDView` (root del prefab), arrastrar al campo `_chainPhaseIndicator`.
4. Posicionar donde sea visible durante combate (ej. arriba de la zona de dados).

> El indicador se subscribe a `OnChainPhaseStarted` / `OnChainCompleted` automaticamente.

---

## 5. Wiring UI — Chain Pass button

El delegate `CombatHUDView.OnChainPassRequested` ya esta cableado por
`CombatHandoffService`. Solo falta un boton en el prefab.

1. En el prefab del CombatHUD, crear un **Button** hijo:
   - Nombre: `ChainPassButton`
   - Texto: `"Pass"` o `"Skip"`
2. En el `OnClick()` del Button, referenciar `CombatHUDView.InvokeChainPassRequested()`.
   - Alternativa: crear un wrapper MonoBehaviour que llame al delegate.
3. El boton deberia mostrarse solo durante chains. Opciones:
   - Manejar visibilidad desde `ChainPhaseIndicatorView` (si estan en el mismo container).
   - Escuchar `OnChainPhaseStarted` (mostrar) / `OnChainCompleted` (ocultar).

> Cuando el jugador presiona Pass, `CombatHandoffService` cancela la seleccion activa,
> termina el budget de rerolls, y dispara `OnChainCompleted` con `wasPass = true`.

---

## 6. Flujo runtime del chain

```
1. Player selecciona behavior con EffChain
2. CombatHandoffService detecta chain (FindChainEffect)
3. StartBudget() — presupuesto de rolls compartido
4. Por cada fase (0..N):
   a. Fire OnChainPhaseStarted(playerGuid, phaseIndex, totalPhases)
   b. Roll dados (si NeedsDiceRoll)
   c. Player elige dados a mantener
   d. Resolve combo
   e. Ejecutar effects de la fase
   f. Si budget agotado (free + energy = 0) → auto-finish
5. Fire OnChainCompleted(playerGuid, phasesCompleted, totalPhases, wasPass)
6. Limpiar estado (_activeChain = null)
```

---

## 7. Archivos entregados

| Archivo | Descripcion |
|---|---|
| `Effects/Concretes/EffChain.cs` | Efecto container multi-fase |
| `Effects/ChainPhase.cs` | Data container por fase (Label + EffectData) |
| `Combat/Handoff/CombatHandoffService.cs` | Logica de chain phases integrada |
| `Heroes/HeroActionBehavior.cs` | `FindChainEffect()` para detectar chains |
| `UI/HUD/ChainPhaseIndicatorView.cs` | Componente UI "Phase X/Y" |
| `UI/Screens/CombatHUDView.cs` | Campo `_chainPhaseIndicator` agregado |
| `Patterns/EventName.cs` | `OnChainPhaseStarted`, `OnChainCompleted` |
| `Effects/Tests/EffChainTests.cs` | Tests de EffChain |

---

## 8. Eventos

| Evento | Args | Cuando |
|---|---|---|
| `OnChainPhaseStarted` | `[Guid source, int phaseIndex, int totalPhases]` | Al iniciar cada fase |
| `OnChainCompleted` | `[Guid source, int phasesCompleted, int totalPhases, bool wasPass]` | Al terminar el chain |
