# Setup — System#0100c Turn Order con Velocidad Oculta

## Qué hay que hacer ahora

Este worktree entrega únicamente código en:
- `Assets/Scripts/Rollgeon/Attributes/HiddenFromUIAttribute.cs`
- `Assets/Scripts/Rollgeon/Attributes/Stats/Speed.cs`
- `Assets/Scripts/Rollgeon/Balance/RulesetSO.cs` (stub si T100a no mergeó antes)
- `Assets/Scripts/Rollgeon/Balance/TurnOrderConfig.cs`
- `Assets/Scripts/Rollgeon/Combat/TurnOrderService.cs`
- `Assets/Scripts/Rollgeon/Combat/TurnOrderServiceBootstrap.cs`
- `Assets/Scripts/Rollgeon/Combat/Initiative/*.cs`
- `Assets/Scripts/Rollgeon/Combat/Random/*.cs`
- `Assets/Scripts/Rollgeon/Combat/Tests/*.cs` (editor-only)

Tras mergear a `develop`:

1. Abrir Unity (versión fijada en `ProjectSettings/ProjectVersion.txt`).
2. Esperar la recompilación automática. Verificar 0 errores / 0 warnings nuevos.
3. Abrir `Window → General → Test Runner → EditMode` y correr `Run All`. Los tests del
   assembly `Rollgeon.Combat.Tests` deben pasar en verde.

## Pasos en Unity (manual)

### 1. Crear el `RulesetSO` activo

Si T100a (Energy) ya mergeó antes y creó el asset, saltear este paso.

1. `Create → Rollgeon → Meta → Ruleset` (el menú lo expone `RulesetSO.cs`).
2. Guardar en `Assets/Rollgeon/Rulesets/Ruleset_Default.asset`.
3. En el inspector, bajo **Initiative** (viene de `TurnOrderConfig`):
   - `Speed Die Min = 1`
   - `Speed Die Max = 6` (default GDD-friendly)
   - `Fallback Initiative For Missing Speed = 0`

El `OnValidate` corrige automáticamente si alguien pone `Max < Min`.

### 2. Bootstrap del servicio

Foundation#0005 ya carga el `ServiceBootstrapSO`. Para que este worktree quede activo al
arrancar:

1. `Create → Rollgeon → Bootstrap → Turn Order Service` (el menú lo expone
   `TurnOrderServiceBootstrap.cs`).
2. Guardar en `Assets/Rollgeon/Bootstrap/TurnOrderServiceBootstrap.asset`.
3. Dejar `Rng Seed = 0` en producción. Para QA/tests reproducibles, subir el seed a
   cualquier entero positivo.
4. Abrir `Assets/Rollgeon/Bootstrap/ServiceBootstrapSO.asset`:
   - Asegurarse que el `RulesetSO` activo esté en `Catalogs` / `ExtraServices` con
     `Priority` menor que `100` (así se registra antes que el TurnOrderServiceBootstrap).
   - Agregar `TurnOrderServiceBootstrap.asset` a la lista de `ExtraServices`.

### 3. Entity registry (stub)

Mientras no exista el registro real de entidades (Rollgeon.Entities), el bootstrap
registra un `InMemoryEntityRegistry` vacío como fallback. En scenes de prueba, poblarlo
manualmente antes de llamar `BuildForCombat`:

```csharp
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.Initiative;

var registry = ServiceLocator.GetService<IEntityRegistry>() as InMemoryEntityRegistry;
var playerId = System.Guid.NewGuid();
var attrs = new ModifiableAttributes();
attrs.SetAttribute<Speed>(new Speed(5));
registry!.Register(playerId, attrs);
```

### 4. Verificación manual

1. En una scene con `BootstrapRunner`, crear un `TempSmokeTest.cs` temporal (no commitear):

```csharp
using Patterns;
using Rollgeon.Combat;
using UnityEngine;

public class TempSmokeTest : MonoBehaviour
{
    private void Start()
    {
        EventManager.Subscribe(EventName.OnTurnQueueBuilt, args =>
        {
            Debug.Log($"OnTurnQueueBuilt fired — count={((System.Collections.Generic.IReadOnlyList<System.Guid>)args[0]).Count} round={args[1]}");
        });

        // Armar registry con entidades dummy, luego:
        var service = ServiceLocator.GetService<TurnOrderService>();
        service.BuildForCombat(new[] { /* playerId, enemyId, ... */ });
    }
}
```

2. Correr la scene. Verificar en consola que se loggea `OnTurnQueueBuilt` con el orden
   y `roundIndex=0`. Llamar `service.Advance()` hasta dar la vuelta; el último
   advance debe volver a dispararlo con `roundIndex=1`.

## Coordinación con T100a

- Ambos worktrees editan `Assets/Scripts/Rollgeon/Balance/RulesetSO.cs`.
- Si T100a mergea primero: el archivo ya existe con la sección **Combat — Energy**;
  este worktree debe **agregar** la sección `TurnOrderConfig TurnOrder` al mismo SO
  durante el merge. Buscar el `[Merge hook]` comment para saber dónde insertar.
- Si este worktree mergea primero (stub): el archivo queda con sólo `TurnOrderConfig`
  + el `[Merge hook]` comment listo para que T100a agregue su sub-struct.
- Reviewer alerta: **un solo `RulesetSO.cs`** en `Rollgeon.Balance`. Si hay dos
  archivos con ese nombre, es un bug de merge.

## Qué queda pendiente para otros worktrees

- **`CombatTurnFSM` (T100d)** — este worktree entrega `TurnOrderService` consumible,
  no lo orquesta. T100d decide cuándo llamar `Advance()`, cuándo skipear un muerto,
  etc.
- **HUD de turn queue (T95b)** — se suscribe a `OnTurnQueueBuilt` y renderiza
  portraits en el orden recibido. Regla de diseño: **nunca números**. Los stats
  marcados con `[HiddenFromUI]` (incluyendo `Speed`) deben ser skipeados por
  cualquier iterador de render.
- **Balance audit (#101)** — validar los defaults `SpeedDieMin=1, SpeedDieMax=6`
  con playtests. Exponer `BaseSpeed` en `ClassHeroSO.Sheet` (T97) y en
  `EnemyDataSO` (T99).
- **Determinismo de run (§15)** — cuando exista `RunProgress`, el
  `DefaultInitiativeRng` debe recibir su seed desde ahí para que replays sean
  reproducibles. Marcado como `TODO [Run determinism]` en el código.

## Decisiones congeladas en este worktree

- **`_roundIndex` incrementa en cada wrap-around.** Desviación consciente del snippet
  de TECHNICAL.md §12.7 (que muestra `-1` como sentinel). Motivo: el schema
  documentado del evento no habla de sentinels, y un `roundIndex` monótono da más
  info al HUD.
- **Tie-break por `Guid.CompareTo` ASC.** Determinista y total; no depende del orden
  de llegada ni del RNG.
- **Payload de `OnTurnQueueBuilt` es una copia.** `new ReadOnlyCollection<Guid>(new List<Guid>(_orderForRound))`
  para impedir que un listener mute el estado interno del servicio.
- **`[HiddenFromUI]` attribute** sobre `Speed` — mecanismo acordado para el contrato
  "stat oculto en UI". Test estático lo verifica.
- **`IEntityRegistry` vive en `Rollgeon.Combat.Initiative`** (no en `Rollgeon.Entities`)
  porque ese namespace aún no existe. Migración futura = cambio de namespace + find/replace.
