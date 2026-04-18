# Setup — Foundation#0001 ServiceLocator + EventManager

## Qué hay que hacer ahora

**Nada.** Este worktree entrega solo código estatico en `Assets/Scripts/Rollgeon/Patterns/`.
Tras mergear:

1. Abrir Unity.
2. Esperar la recompilacion.
3. Verificar 0 errores / 0 warnings en la consola.

Fin del setup obligatorio. No hay scenes que crear, ni prefabs, ni ScriptableObjects, ni componentes que agregar a ningun GameObject.

## Que queda pendiente para otros worktrees

- **Bootstrap scene y registro de servicios concretos** → `Foundation#0005_CatalogsAndBootstrap`. Ese worktree va a crear un MonoBehaviour entry point (tipo `GameBootstrap`) que llama `ServiceLocator.AddService<T>(...)` para cada servicio listado en un `ServiceBootstrapSO`. Acá solo publicamos la **API pura**.
- **Consumidores downstream** (Energy, Combat, Combos, HUD, Turn, Dice, Craps, etc.) no tienen que referenciar este worktree manualmente — basta con que su codigo use:
  - `Patterns.ServiceLocator`
  - `Patterns.EventManager` + `Patterns.EventName`
  - `Patterns.TypedEvent<T>` + los structs de payload (`DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`).

## Decisiones congeladas en este worktree

- **Canal unico.** `OnDamageResolved`, `OnHealthChanged` y `OnComboMatched` viven **solo** como `TypedEvent<T>`. No estan en `EventName` por diseño (ver TECHNICAL.md §1.2.1). No agregarlos al enum sin abrir ticket de cambio.
- **ScreenManager eventos fuera del bus legacy.** `OnScreenPushed`, `OnScreenPopped`, `OnPauseChanged` no estan en `EventName` — viven en `IScreenManager` (§17.D).
- **Regla transversal de `args[0]`.** Todo evento legacy cuyo payload referencie una entidad concreta debe pasar `Guid` como `args[0]` (InstanceId de la entidad primaria). La infra no valida — es contrato del publisher.
- **Sin `.asmdef`.** El worktree no introduce assembly definitions. Si se decide adoptarlas, que sea un worktree de refactor dedicado.

## Como validar manualmente que la infra funciona (opcional)

Solo si se quiere un smoke test sin esperar a Foundation#0005:

1. Crear temporalmente un MonoBehaviour en una scene vacia que en `Start()` haga:
   ```csharp
   using Patterns;
   using UnityEngine;

   public class FoundationSmokeTest : MonoBehaviour
   {
       private void Start()
       {
           // ServiceLocator
           ServiceLocator.AddService<string>("hello", ServiceScope.Global);
           Debug.Log(ServiceLocator.GetService<string>()); // "hello"
           ServiceLocator.ClearScope(ServiceScope.Run);    // no-op, no habia Run
           Debug.Assert(ServiceLocator.HasService<string>()); // true
           ServiceLocator.Clear();
           Debug.Assert(!ServiceLocator.HasService<string>()); // true

           // EventManager
           EventManager.EventReceiver cb = args => Debug.Log($"turno termino: {args[0]}");
           EventManager.Subscribe(EventName.OnTurnFinished, cb);
           EventManager.Trigger(EventName.OnTurnFinished, System.Guid.NewGuid());
           EventManager.UnSubscribe(EventName.OnTurnFinished, cb);
           EventManager.ResetEventDictionary();

           // TypedEvent
           System.Action<DamageResolvedPayload> listener = p =>
               Debug.Log($"dmg {p.FinalDamage} weakness={p.WeaknessHit}");
           TypedEvent<DamageResolvedPayload>.Subscribe(listener);
           TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
           {
               SourceGuid   = System.Guid.NewGuid(),
               TargetGuid   = System.Guid.NewGuid(),
               FinalDamage  = 10,
               WeaknessHit  = true,
           });
           TypedEvent<DamageResolvedPayload>.Unsubscribe(listener);
           TypedEvent<DamageResolvedPayload>.Clear();
       }
   }
   ```
2. Correr Play Mode, ver los logs en consola.
3. Borrar el MonoBehaviour (no se commitea).

## Contratos publicados (referencia rapida)

| Tipo | Archivo | API |
|---|---|---|
| `ServiceScope` (enum) | `Assets/Scripts/Rollgeon/Patterns/ServiceScope.cs` | `Global`, `Run` |
| `ServiceLocator` (static) | `Assets/Scripts/Rollgeon/Patterns/ServiceLocator.cs` | `AddService<T>`, `GetService<T>`, `TryGetService<T>`, `RemoveService<T>`, `HasService<T>`, `ClearScope`, `Clear` |
| `EventName` (enum) | `Assets/Scripts/Rollgeon/Patterns/EventName.cs` | Familia minima §1.2 con XML-docs de schema por entry |
| `EventManager` (static) | `Assets/Scripts/Rollgeon/Patterns/EventManager.cs` | `Subscribe`, `UnSubscribe`, `Trigger`, `ResetEventDictionary` + delegate `EventReceiver` |
| `TypedEvent<T>` (static, `T : struct`) | `Assets/Scripts/Rollgeon/Patterns/TypedEvent.cs` | `Subscribe`, `Unsubscribe`, `Raise`, `Clear` |
| Payload structs | `Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs` | `DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload` |

Referencia master: `TECHNICAL.md` §1.1, §1.2, §1.2.1. Plan del worktree: `plan.md`.
