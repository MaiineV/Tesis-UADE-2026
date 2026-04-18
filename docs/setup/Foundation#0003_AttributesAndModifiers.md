# Setup — Foundation#0003 Attributes + Modifiers

## Qué hay que hacer ahora

**Nada en Unity**, aside del smoke check. Este worktree entrega únicamente código en
`Assets/Scripts/Rollgeon/Attributes/`. Tras mergear a `develop`:

1. Abrir Unity (versión fijada en `ProjectSettings/ProjectVersion.txt`, actualmente Unity 6).
2. Esperar la recompilación automática.
3. Verificar 0 errores / 0 warnings en la consola.
4. Abrir `Window → General → Test Runner → EditMode` y correr `Run All`. Los tests de
   `Rollgeon.Attributes.Tests` deben pasar en verde. Si alguno falla, reportar al reviewer
   con los logs.

**Dependencia previa.** Foundation#0001 (ServiceLocator + EventManager + `EventName`) ya
debe estar mergeado en `develop`. Si no, este worktree no compila.

Fin del setup obligatorio: **sin scenes, sin prefabs, sin ScriptableObjects**.

## Qué queda pendiente para otros worktrees

- **Registro del servicio en bootstrap** → `Foundation#0005_CatalogsAndBootstrap`. Ese
  worktree va a instanciar `new AttributesManager()` y registrarlo en el
  `ServiceLocator`. Hasta entonces, si querés probar manualmente en una scene temporal,
  armar un MonoBehaviour `TempBootstrap.cs` (no se commitea) con:

  ```csharp
  using Patterns;
  using Rollgeon.Attributes;

  public class TempBootstrap : UnityEngine.MonoBehaviour
  {
      private void Awake()
      {
          var attrs = new AttributesManager();
          ServiceLocator.AddService<AttributesManager>(attrs);
      }
  }
  ```

- **Stats concretos** → T100a (y equivalentes). Crear clases `Health : BaseAttribute<int>`,
  `Energy : BaseAttribute<int>`, `Speed : BaseAttribute<int>`, `IncomingDamageMultiplier :
  BaseAttribute<float>`, etc. Cada una solo necesita implementar `CreateDuplicate()`.
- **Pipeline de daño / heal** → §12, §17.M. Consume los modifiers `Outgoing` / `Incoming`
  desde `BaseAttribute` via las helpers `GetRawModifiers()`.
- **Save de `Entity.Attributes`** → §15. El `ISaveable.RestoreState` debe llamar
  `mod.OnLoad()` sobre cada modifier deserializado para re-hookear sus eventos (ver
  nota en `Modifier<T>.OnLoad` — es **idempotente**, seguro llamarlo múltiples veces).

## Decisiones congeladas en este worktree (Revisión 2)

- **`Modifier<T>.CarrierId`** = entidad que CARGA el modificador (ex `OwnerId`).
- **`Modifier<T>.SourceId`** = entidad/efecto que ORIGINÓ el mod. `Guid.Empty` si no aplica.
- Constructor: `Modifier<T>(amount, op, duration, carrierId, sourceId, dir, lifetime, tickEvent)`.
- `OnTickTriggered` hace gating por `CarrierId == args[0]` (sólo tickea en el turno de quien
  lo lleva puesto).
- `RemoveAndNotify` dispara `OnModifierRemoved(CarrierId, ModifierId)`.
- **`AttributesManager.RemoveAllModifiersBySource(Guid.Empty)` es no-op** — regla de
  seguridad para evitar borrado masivo accidental de mods anónimos.
- `RemoveModifierBySource<TAttribute, TValue>(entityId, sourceId)` queda disponible para
  cleanups per-entidad-per-atributo.

## Variables expuestas para el audit de §101

| Variable | Ubicación | Tipo | Default | Propósito |
|---|---|---|---|---|
| `AttributesManager.LogMissingEntityAsWarning` | `AttributesManager.cs` (public static bool) | bool | `true` | Si un `Guid` no está registrado, loguear warning (on) o lanzar `KeyNotFoundException` (off). `true` por defecto para no tumbar runs por bugs de orden de registro. |
| `Modifier<T>.Duration` | inspector (via efectos que crean mods, Foundation#0004) | int | — | Turnos hasta expirar. Sólo relevante si `Lifetime == Turns`. |
| `Modifier<T>.Amount` | inspector (via efectos) | `T` genérico | — | Monto del modificador. El rango lo valida el efecto que lo crea. |

Los valores numéricos de balance (HP base, max stacks, curvas de regen) **no viven en
este worktree** — viven en los stats concretos y en `ClassHeroSO._baseStats` que crea
T100a en adelante.

## Cómo validar manualmente la infra (opcional, sin esperar a Foundation#0005)

Sólo si se quiere un smoke test rápido:

```csharp
using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using UnityEngine;

public class AttributesSmokeTest : MonoBehaviour
{
    // Asumiendo que T100a o el dev definió un Health : BaseAttribute<int>.
    // Para el smoke sin stats concretos, reutilizar TestIntAttribute del asm de tests.

    private void Start()
    {
        var mgr = new AttributesManager();
        var playerId = Guid.NewGuid();

        // 1. Registrar atributos
        // (Reemplazar TestIntAttribute por el stat concreto que exista al momento del smoke.)
        // var attrs = new ModifiableAttributes();
        // attrs.SetAttribute<Health>(new Health(100));
        // mgr.Register(playerId, attrs);

        // 2. Leer valor modificado
        // int hp = mgr.GetAttributeModifiedValue<Health, int>(playerId);

        // 3. Agregar un modificador temporal
        // var buff = new Modifier<int>(
        //     amount: 10, op: ModifierOperation.Add, duration: 3,
        //     carrierId: playerId, sourceId: Guid.Empty,
        //     dir: ModifierDirection.Intrinsic,
        //     lifetime: ModifierLifetime.Turns,
        //     tickEvent: EventName.OnTurnFinished);
        // mgr.AddModifier<Health, int>(playerId, buff);

        // 4. Tick un turno
        // EventManager.Trigger(EventName.OnTurnFinished, playerId);

        mgr.Dispose();
    }
}
```

## Contratos publicados (referencia rápida)

| Tipo | Archivo | Notas |
|---|---|---|
| `IAttribute` / `IAttribute<TValue>` | `Assets/Scripts/Rollgeon/Attributes/IAttribute.cs`, `IAttributeT.cs` | Contrato estático del valor + tipo + clone. |
| `IModifiable` / `IModifiable<TValue>` | `Assets/Scripts/Rollgeon/Attributes/IModifiable.cs`, `IModifiableT.cs` | Atributo con stack de modifiers. |
| `BaseAttribute<TValue>` | `Assets/Scripts/Rollgeon/Attributes/BaseAttribute.cs` | Base abstracta para stats concretos. |
| `ModifiableAttributes` | `Assets/Scripts/Rollgeon/Attributes/ModifiableAttributes.cs` | Contenedor por tipo, Odin-serializado. |
| `Modifier<T>` | `Assets/Scripts/Rollgeon/Attributes/Modifiers/Modifier.cs` | Estructura con CarrierId + SourceId + `OnLoad` idempotente. |
| `ModifierOperation` / `ModifierDirection` / `ModifierLifetime` | `Assets/Scripts/Rollgeon/Attributes/Modifiers/*.cs` | Enums. |
| `IModifier` / `IModifier<T>` | `Assets/Scripts/Rollgeon/Attributes/Modifiers/IModifier.cs`, `IModifierT.cs` | Metadata + apply. |
| `OperationResolver` | `Assets/Scripts/Rollgeon/Attributes/Modifiers/OperationResolver.cs` | Cachea `(Type, Op) → Func<T,T,T>`. |
| `AttributesManager` | `Assets/Scripts/Rollgeon/Attributes/AttributesManager.cs` | Servicio indexado por `Guid`, con `RemoveAllModifiersBySource`. |

Referencia master: `TECHNICAL.md` §2, §3. Plan del worktree: `plan.md` (Revisión 2 al inicio).
