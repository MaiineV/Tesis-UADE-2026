# Passive System — Technical Analysis

> Documento de planificacion para implementar el sistema de pasivas (TECHNICAL.md §4.4, §4.4.1, §4.5, §2.4).
> Fecha: 2026-04-25. Autor: Claude + Gabriel.

---

## 1. Objetivo

Implementar `ClassPassiveSO` y el ciclo `Entity.BindPassive/UnbindPassive` para que cada clase de heroe
tenga una habilidad pasiva que reaccione a eventos del bus (`EventManager`) ejecutando `EffectData`.

**Resultado esperado:** Un disenador puede crear un `ClassPassiveSO` desde el menu de assets, agregarle hooks
(evento + efectos), asignarlo al `ClassHeroSO`, y al iniciar una run los hooks se auto-suscriben al bus
y reaccionan automaticamente filtrados por el `InstanceId` del jugador.

---

## 2. Estado actual del codebase

### 2.1 Lo que YA existe y funciona

| Componente | Archivo | Estado |
|---|---|---|
| `EventManager` (bus legacy) | `Patterns/EventManager.cs` | Completo. Subscribe/UnSubscribe/Trigger con `object[]` payload |
| `EventName` enum | `Patterns/EventName.cs` | 60+ eventos definidos. Los que la pasiva necesita ya existen (`OnRollResolved`, `OnComboMatched`*, `OnTurnStarted`, etc.) |
| `EffectData` | `Effects/EffectData.cs` | Completo. PreConditions + Effects con short-circuit. `Execute(ctx)` y `TryExecute(ctx, preCtx)` |
| `EffectContext` | `Effects/EffectContext.cs` | Completo. Campos `SourceGuid`, `SourceEntity`, `TriggeringEntity`, `lastResult`, etc. |
| `BaseEffect` / `IEffect` | `Effects/BaseEffect.cs`, `IEffect.cs` | Completo. Pipeline sellada con `Apply`/`ApplyEffect` |
| `BasePreCondition` | `PreConditions/BasePreCondition.cs` | Completo. `EvaluateAll` con AND semantico |
| `ServiceLocator` | `Patterns/ServiceLocator.cs` | Completo. Scopes Global/Run, `ClearScope` con `IDisposable` auto-cleanup |
| `BaseEntitySO` | `Entities/BaseEntitySO.cs` | Completo. `EntityId`, `DisplayName`, `Description`, `CreateRuntimeStats()` abstract |
| `ClassHeroSO` | `Heroes/ClassHeroSO.cs` | Parcial. Tiene `PassiveRef : ScriptableObject` como STUB (linea 85) |
| `Entity` | `Entities/Entity.cs` | **Stub minimo**: solo tiene `public Guid Guid` |
| `ISaveable` | `Patterns/Save/ISaveable.cs` | Stub. Interface definida, SaveSystem no implementado |
| `ModifiableAttributes` | `Attributes/ModifiableAttributes.cs` | Completo. Type-keyed dict de `IModifiable` |
| `EnemyDataSO` | `Entities/EnemyDataSO.cs` | Completo. Hereda `BaseEntitySO`. Tiene `CreateRuntimeStats()` + `CreateRuntimeBehaviors()` |

(*) `OnComboMatched` vive como `TypedEvent<ComboMatchedPayload>`, no en `EventManager`. Las pasivas que reaccionen
a combos usaran `TypedEvent<T>` en vez del bus legacy. Esto requiere un ajuste al patron de binding.

### 2.2 Lo que NO existe

| Componente | Descripcion |
|---|---|
| `ClassPassiveSO` | El SO que define la pasiva: `PassiveId`, `DisplayName`, `Hooks : List<PassiveHook>` |
| `PassiveHook` | Struct serializable: `EventName TriggerEvent` + `EffectData Effect` |
| `Entity.BindPassive()` | Metodo que suscribe handlers al bus, cerrados sobre `InstanceId` |
| `Entity.UnbindPassive()` | Metodo que des-suscribe todos los handlers |
| `Entity.Passive` property | `ClassPassiveSO Passive { get; private set; }` |
| `Entity._passiveHandlers` | Lista local de `(EventName, handler)` para cleanup |
| `Entity : IDisposable` | `Dispose()` que llame a `UnbindPassive()` |
| `Entity.InstanceId` property | Actualmente solo tiene `Guid Guid` — renombrar o agregar property |
| `RunBootstrapper` | No existe. El flujo `player.BindPassive(hero.Passive)` no tiene donde vivir todavia |

### 2.3 Lo que necesita MODIFICACION

| Archivo | Cambio |
|---|---|
| `ClassHeroSO.cs` | Reemplazar `ScriptableObject PassiveRef` (linea 85) por `ClassPassiveSO Passive` |
| `Entity.cs` | Expandir de stub a clase funcional con `InstanceId`, `Passive`, `BindPassive`, `UnbindPassive`, `IDisposable` |
| `BaseEntitySO.cs` | Nota: `ClassHeroSO` NO hereda de `BaseEntitySO` actualmente. Son paralelos. Esto es un gap conocido del TECHNICAL.md §7.0/v7 que no necesita cerrarse para pasivas. |

---

## 3. Dependencias y riesgos

### 3.1 Dependencias RESUELTAS

- **EventManager**: listo, la API `Subscribe/UnSubscribe` es exactamente lo que `BindPassive` necesita.
- **EffectData.Execute(ctx)**: listo, con short-circuit y soporte de preconditions.
- **EffectContext**: listo, tiene `SourceEntity`, `SourceGuid`, `lastResult`.
- **Odin serialization**: disponible. `ClassPassiveSO` hereda `SerializedScriptableObject` para `[OdinSerialize]` en la lista de hooks.

### 3.2 Dependencias PARCIALES

- **TypedEvent<T>**: existe pero algunos eventos criticos para pasivas (ej. `OnComboMatched`, `OnDamageResolved`, `OnHealthChanged`) viven ahi, NO en `EventManager`. El TECHNICAL.md §4.4.1 describe hooks solo contra `EventName` (bus legacy). **Decision**: para v1, las pasivas solo hookean al bus legacy (`EventManager`). Las pasivas que necesiten `TypedEvent<T>` se abordan en un segundo pass. Los eventos mas importantes (`OnTurnStarted`, `OnRollResolved`, `OnDamageOutgoing`, `OnDamageIncoming`) estan en el bus legacy.

- **Entity runtime**: el Entity actual es un stub. Necesita expansion, pero el scope esta acotado — solo agregamos lo necesario para pasivas, no todo §2.4. Los campos hero-specific (`Sheet`, `DiceBag`, `Attributes`, `RuntimeBehaviors`) quedan para tareas futuras.

### 3.3 Sin riesgo

- **RunBootstrapper**: no existe, pero no es bloqueante. La pasiva se bindea manualmente cuando el caller lo decida (selection screen, test, etc). Documentamos el punto de integracion.
- **EntityCatalogSO.AllPassiveIds**: el dropdown de `PassiveId` necesita un catalogo. Como `EntityCatalogSO` no unifica heroes todavia, el dropdown puede caer a `Array.Empty` sin romper nada. El `PassiveId` se escribe a mano por ahora.
- **ClassHeroSO no hereda BaseEntitySO**: esto es un gap conocido. La pasiva no depende de esa herencia — `ClassHeroSO.Passive` es un campo directo.

---

## 4. Plan de implementacion

### Fase 1: Archivos nuevos (3 archivos)

#### 4.1 `Assets/Scripts/Rollgeon/Heroes/ClassPassiveSO.cs` — NUEVO

```
Namespace: Rollgeon.Heroes
Hereda: SerializedScriptableObject (Odin)
Menu: "Rollgeon/Heroes/Class Passive"

Campos:
  - string PassiveId          [ValueDropdown si hay catalogo, sino manual]
  - string DisplayName
  - string Description        [TextArea]
  - List<PassiveHook> Hooks   [OdinSerialize]
```

#### 4.2 `Assets/Scripts/Rollgeon/Heroes/PassiveHook.cs` — NUEVO

```
Namespace: Rollgeon.Heroes
Clase: PassiveHook [Serializable]

Campos:
  - EventName TriggerEvent
  - EffectData Effect = new()  [OdinSerialize]
```

#### 4.3 `Assets/Scripts/Rollgeon/Heroes/Tests/ClassPassiveSOTests.cs` — NUEVO

Tests EditMode:
  - BindPassive_SubscribesHandlers
  - UnbindPassive_UnsubscribesAll
  - PassiveHook_FiltersBy_InstanceId (no cross-talk)
  - BindPassive_Null_DoesNothing
  - BindPassive_Twice_UnbindsPrevious
  - Dispose_UnbindsPassive

### Fase 2: Archivos modificados (2 archivos)

#### 4.4 `Assets/Scripts/Rollgeon/Entities/Entity.cs` — MODIFICAR

Expandir el stub actual. Agregar:

```
using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Heroes;

namespace Rollgeon.Entities
{
    public class Entity : IDisposable
    {
        // Existente (renombrar de Guid a InstanceId para alinear con §2.4,
        // pero mantener Guid como alias para no romper consumers existentes)
        public Guid InstanceId;
        public Guid Guid { get => InstanceId; set => InstanceId = value; }

        // Passive system
        public ClassPassiveSO Passive { get; private set; }
        private readonly List<(EventName evt, EventManager.EventReceiver handler)>
            _passiveHandlers = new();

        public void BindPassive(ClassPassiveSO passive) { ... }
        public void UnbindPassive() { ... }
        public void Dispose() => UnbindPassive();
    }
}
```

**Logica de BindPassive:**
1. Si `passive == null`, return.
2. Si ya hay una pasiva bindeada, llamar `UnbindPassive()` primero (idempotente).
3. Setear `Passive = passive`.
4. Para cada `hook` en `passive.Hooks`:
   a. Crear un `EventManager.EventReceiver handler` como lambda.
   b. En el handler: filtrar por `args[0] is Guid ownerId && ownerId == InstanceId`.
   c. Si pasa: construir `EffectContext(source: this, target: this)` y llamar `hook.Effect.Execute(ctx)`.
   d. Suscribir con `EventManager.Subscribe(hook.TriggerEvent, handler)`.
   e. Guardar `(evt, handler)` en `_passiveHandlers`.

**Logica de UnbindPassive:**
1. Iterar `_passiveHandlers` y llamar `EventManager.UnSubscribe(evt, handler)`.
2. Limpiar la lista.
3. Setear `Passive = null`.

**Impacto en consumers existentes:**
- `Entity.Guid` se usa en `EffectContext`, `PreConditionContext`, `HeroActionBehavior`. La property alias `Guid { get => InstanceId }` mantiene compatibilidad binaria.

#### 4.5 `ClassHeroSO.cs` — MODIFICAR

```diff
- [Tooltip("[STUB] — elevated by Hero Template task. Opaque ref al PassiveAbilitySO de la clase.")]
- public ScriptableObject PassiveRef;
+ [Tooltip("Pasiva de la clase (§4.4). Null = sin pasiva.")]
+ public ClassPassiveSO Passive;
```

Esto reemplaza el stub por la referencia tipada. Los assets existentes que tuvieran `PassiveRef = null` seguiran funcionando (el campo nuevo empieza null).

---

## 5. Detalle de cada archivo

### 5.1 ClassPassiveSO.cs

```
Responsabilidad: Asset inmutable que describe QUE hace la pasiva.
No tiene estado runtime. No se suscribe a nada.
Shared entre todas las instancias del mismo heroe.

Campos:
  PassiveId     — identidad unica, string. Convencion: "passive.<hero>.<nombre>"
  DisplayName   — nombre para UI
  Description   — tooltip/codex
  Hooks         — lista de PassiveHook (evento + efectos)
```

### 5.2 PassiveHook.cs

```
Responsabilidad: Par (TriggerEvent, Effect) que define CUANDO y QUE hacer.

TriggerEvent  — EventName del bus legacy.
Effect        — EffectData con PreConditions + Effects.
                Las preconditions del hook permiten filtrar condiciones extra
                (ej. PCFirstRollOfCombat para Berserker).
```

### 5.3 Entity.cs expandido

```
Responsabilidad: Bind/Unbind de la pasiva al bus de eventos.
Los handlers se cierran sobre InstanceId para garantizar aislamiento.

_passiveHandlers — almacena tuplas para poder des-suscribir limpiamente.
IDisposable      — ClearScope(Run) del ServiceLocator auto-invoca Dispose()
                   si Entity esta registrado como servicio. De lo contrario,
                   el caller es responsable de llamar Dispose().
```

---

## 6. Tests

Todos EditMode. No necesitan Play Mode.

| Test | Que valida |
|---|---|
| `BindPassive_WithOneHook_SubscribesToEvent` | Trigger del evento ejecuta el efecto |
| `BindPassive_WithMultipleHooks_SubscribesAll` | Cada hook se suscribe independientemente |
| `BindPassive_FiltersByInstanceId` | Disparar evento con otro Guid NO ejecuta el efecto |
| `BindPassive_MatchingInstanceId_ExecutesEffect` | Disparar con el Guid correcto SI ejecuta |
| `BindPassive_NullPassive_NoOp` | No lanza, no suscribe nada |
| `BindPassive_CalledTwice_UnbindsPrevious` | Idempotencia — solo una suscripcion activa |
| `UnbindPassive_ClearsHandlers` | Post-unbind, el evento no ejecuta el efecto |
| `UnbindPassive_SetsPassiveNull` | `entity.Passive == null` post-unbind |
| `Dispose_CallsUnbindPassive` | Dispose limpia todo |
| `TwoEntities_SamePassive_NosCrossTalk` | Cada entity reacciona solo a su propio InstanceId |
| `Hook_WithFailingPreCondition_DoesNotExecuteEffects` | EffectData.CanBeExecuted filtra |

---

## 7. Scope OUT (no incluido en esta tarea)

- **RunBootstrapper**: no existe. El punto de integracion `player.BindPassive(hero.Passive)` queda documentado pero sin caller concreto hasta que exista el flujo de inicio de run.
- **TypedEvent<T> hooks**: pasivas que reaccionen a `OnComboMatched`, `OnDamageResolved` o `OnHealthChanged` necesitarian un segundo tipo de hook. Fuera de scope v1.
- **Entity como §2.4 completo**: no expandimos `Entity` a todos los campos del spec (Template, Attributes, Sheet, DiceBag, AIRoot, GridPosition, ISaveable). Solo lo necesario para pasivas.
- **ClassHeroSO hereda BaseEntitySO**: gap conocido. No se cierra aca.
- **EntityCatalogSO.AllPassiveIds**: dropdown no funcional hasta que exista el catalogo unificado. El `PassiveId` se escribe manual.
- **Pasivas concretas** (Berserker, Gambler, Necromancer): son contenido, no sistema. Se crean como assets una vez que el sistema este en pie.
- **UI de pasivas**: mostrar la pasiva en selection screen / HUD es tarea de UI.

---

## 8. Orden de ejecucion

1. Crear `PassiveHook.cs` (no tiene dependencias)
2. Crear `ClassPassiveSO.cs` (depende de PassiveHook)
3. Modificar `Entity.cs` (depende de ClassPassiveSO + EventManager)
4. Modificar `ClassHeroSO.cs` (depende de ClassPassiveSO)
5. Crear tests (depende de todo lo anterior)

Los pasos 1-2 pueden ir en paralelo. El paso 3 es el core. El paso 4 es un one-liner.

---

## 9. Archivos tocados — resumen

| Archivo | Accion | Lineas estimadas |
|---|---|---|
| `Heroes/PassiveHook.cs` | NUEVO | ~15 |
| `Heroes/ClassPassiveSO.cs` | NUEVO | ~30 |
| `Entities/Entity.cs` | MODIFICAR (expandir stub) | ~60 |
| `Heroes/ClassHeroSO.cs` | MODIFICAR (1 campo) | ~3 |
| `Heroes/Tests/ClassPassiveSOTests.cs` | NUEVO | ~200 |
| **Total** | | ~308 |
