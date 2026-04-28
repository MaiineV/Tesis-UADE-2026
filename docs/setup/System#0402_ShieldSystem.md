# Setup — System#0402: Shield System

Shield temporal que absorbe dano antes de Health. Se resetea a 0 al inicio de cada turno.
El jugador lo obtiene via `EffAddShield` (source Constant o ComboValue). Enemigos soporte
podrian otorgarlo a aliados en futuras iteraciones.

TECHNICAL.md §12.4.

---

## 1. Verificar compilacion

1. Abrir Unity. Esperar recompilacion.
2. Verificar 0 errores en la consola.
3. **Test Runner > EditMode**, correr:
   - `Rollgeon.Attributes.Stats.Tests.ShieldTests` — 6 tests.
   - `Rollgeon.Effects.Tests.EffAddShieldTests` — 11 tests.
   - `Rollgeon.Combat.Tests.ShieldResetHandlerTests` — 4 tests.
   - `Rollgeon.Combat.Pipelines.Tests.DamagePipelineTests` — 12 tests (incluye 5 de shield).
   - Todos deben pasar en verde.

---

## 2. Registracion automatica (ya hecha en codigo)

No se requiere wiring manual de servicios. El codigo ya registra todo:

- `RunController.RegisterPlayer()` agrega `Shield(0)` al player.
- `EnemyDataSO.CreateRuntimeStats()` agrega `Shield(0)` a cada enemigo.
- `RunController.OnRunStart()` registra `ShieldResetHandler` como servicio scoped a Run.
- `DamagePipeline.Resolve()` Stage 4 absorbe shield antes de Health (ya activo).

---

## 3. Crear behavior "Defend" en el hero class SO

El jugador necesita una accion que otorgue shield. Crear un `HeroActionBehavior`:

1. Abrir el SO de la clase (ej. `Assets/Rollgeon/Classes/CH_Warrior.asset`).
2. En la lista de **Behaviors**, agregar un nuevo `HeroActionBehavior`.
3. Configurar:

| Campo | Valor | Nota |
|---|---|---|
| `ActionName` | `"Defend"` | Label visible en el HUD |
| `IsBaseBehavior` | `true` | Ocupa slot fijo |
| `Slot` | Elegir slot libre (ej. 3) | De los 4 base slots |
| `EnergyCost` | `1` | Ajustar segun balance |
| `NeedsDiceRoll` | `true` | Shield se calcula desde combo |
| `FreeRollCount` | `3` | 1 roll + 2 rerolls gratis |
| `AllowsReroll` | `true` | Permitir reroll |
| `AllowsEnergyReroll` | `true` | Pagar por rerolls extra |

4. En **Effects** (lista de `EffectData`), crear un grupo:
   - **PreConditions**: vacio (siempre ejecutable).
   - **Effects[0]**: `EffAddShield`
     - `_shieldSource` = `ComboValue`
     - `_comboMultiplier` = `1.0` (ajustar segun balance)
     - `_baseAmount` = `3` (fallback si no hay combo match)
   - **Effects[1]** (opcional): `EffPlayFeedback`
     - Feedback id apuntando a VFX/SFX de shield.

> **Nota**: `EffAddShield` resuelve el target como self (fallback a `SourceGuid`
> cuando no hay selection). No necesita `SelectionSettings` especiales.

---

## 4. Wiring UI — ShieldBarView

`ShieldBarView` muestra el shield actual del jugador. Se oculta cuando shield = 0.

1. En el prefab del **CombatHUD**, crear un child GameObject:
   - Nombre: `ShieldBar`
   - Agregar componente `ShieldBarView`.
2. Crear los widgets hijos:
   - **Image** (tipo Filled, Horizontal) para la barra visual → arrastrar a `_fillImage`.
   - **TextMeshProUGUI** para el label → arrastrar a `_text`.
   - El `_container` es el GameObject raiz (`ShieldBar`) → arrastrar a `_container`.
     Se auto-oculta cuando shield = 0.
3. En el `CombatHUDView` (root del prefab), arrastrar `ShieldBarView` al campo `_shieldBar`.
4. Posicionar debajo o al lado de la barra de HP.

> `ShieldBarView` se subscribe a `EventName.OnShieldChanged` automaticamente via `Bind()`.
> No requiere wiring manual de eventos.

---

## 5. Floating numbers (ya funciona)

El sistema de floating numbers ya soporta shield:

- `FeedbackManager` mapea `BehaviorValueKey.FloatingShield` → `NumberType.Shield` (cyan).
- Si el behavior de Defend tiene un `EffPlayFeedback` despues de `EffAddShield`,
  el numero flotante aparece automaticamente.

---

## 6. Archivos entregados

| Archivo | Descripcion |
|---|---|
| `Attributes/Stats/Shield.cs` | Stat concreto `Shield : BaseAttribute<int>` |
| `Effects/Concretes/ShieldArgs.cs` | Payload struct para EffAddShield |
| `Effects/Concretes/EffAddShield.cs` | Efecto que ADD shield (Constant o ComboValue) |
| `Combat/ShieldResetHandler.cs` | Listener que resetea shield a 0 en OnTurnStarted |
| `Combat/Pipelines/DamagePipeline.cs` | Stage 4 activado — absorcion de shield |
| `Combat/Pipelines/DamageContext.cs` | Campos `ShieldAbsorbed`, `BlockedByShield` |
| `Patterns/EventPayloads.cs` | `ShieldAbsorbed` en `DamageResolvedPayload` |
| `Run/RunController.cs` | Shield(0) en player + ShieldResetHandler registrado |
| `Entities/EnemyDataSO.cs` | Shield(0) en enemies |
| `UI/HUD/ShieldBarView.cs` | Componente UI para mostrar shield |
| `UI/Screens/CombatHUDView.cs` | Campos `_shieldBar` y `_chainPhaseIndicator` agregados |
| `Attributes/Stats/Tests/ShieldTests.cs` | 6 smoke tests |
| `Effects/Tests/EffAddShieldTests.cs` | 11 tests (Constant + ComboValue + ADD) |
| `Combat/Tests/ShieldResetHandlerTests.cs` | 4 tests |
| `Combat/Pipelines/Tests/DamagePipelineTests.cs` | 5 tests de absorcion agregados |

---

## 7. Comportamiento del shield

- **ADD**: cada `EffAddShield` suma al shield existente (no reemplaza).
- **Absorcion**: DamagePipeline Stage 4 resta del shield antes de tocar Health.
  - `ShieldAbsorbed` = min(shield, damage).
  - `BlockedByShield` = true si todo el dano fue absorbido.
  - Evento `OnShieldChanged` dispara con el shield restante.
- **Reset**: `ShieldResetHandler` escucha `OnTurnStarted` y resetea a 0.
- **Floating**: FeedbackManager ya mapea `FloatingShield → cyan`.
