# Setup — Foundation#0008 DamagePipeline

Esta tarea reemplaza el stub `DamagePipelineStub` con una pipeline real que aplica dano
via `AttributesManager`, evalua weakness, y dispara los eventos del bus legacy y tipado.

## 1. Verificar compilacion

1. Abrir Unity. Esperar la recompilacion.
2. Verificar 0 errores en la consola.

## 2. Registrar la pipeline en el bootstrap

En el `ServiceBootstrapSO` (o el inicializador de run equivalente), agregar la registracion
de la pipeline **despues** de `AttributesManager` y `IWeaknessChecker` (si existe):

```csharp
// Dependencias ya registradas:
// ServiceLocator.AddService<AttributesManager>(...);
// ServiceLocator.AddService<IWeaknessChecker>(...);   // opcional

// Pipeline de dano:
var damagePipeline = new DamagePipeline();
ServiceLocator.AddService<IDamagePipeline>(damagePipeline, ServiceScope.Run);
ServiceLocator.AddService<DamagePipeline>(damagePipeline, ServiceScope.Run);
```

**Nota:** el constructor sin argumentos de `DamagePipeline` resuelve `AttributesManager` e
`IWeaknessChecker` desde `ServiceLocator`. Si `IWeaknessChecker` no esta registrado,
weakness siempre retorna multiplier 1.0 (no-op).

## 3. Correr tests

1. **Window -> General -> Test Runner -> EditMode**.
2. Buscar `DamagePipelineTests` (namespace `Rollgeon.Combat.Pipelines.Tests`).
3. Correr los 7 tests. Todos deben pasar en verde.

## 4. Archivos entregados

| Archivo | Descripcion |
|---|---|
| `Assets/Scripts/Rollgeon/Combat/Pipelines/AttackKind.cs` | Enum de clasificacion de fuente de dano |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/DamageContext.cs` | Data object que viaja por la pipeline |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/IDamagePipeline.cs` | Interface del contrato |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs` | Implementacion real |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/Tests/DamagePipelineTests.cs` | Tests EditMode |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/Tests/Rollgeon.Combat.Pipelines.Tests.asmdef` | Assembly definition para tests |
| `Assets/Scripts/Rollgeon/Effects/Stubs/DamagePipeline.Stub.cs` | Stub retirado — delega a la pipeline real |
| `Assets/Scripts/Rollgeon/Effects/Concretes/EffDamage.cs` | Actualizado para usar `IDamagePipeline` |

## 5. Stages pendientes (placeholders en el codigo)

Los siguientes stages estan comentados en `DamagePipeline.Resolve` porque dependen de
stats que aun no existen. Se habilitan cuando esos stats aterricen:

- **Outgoing multiplier** — requiere `OutgoingDamageMultiplier` stat (float).
- **Incoming multiplier** — requiere `IncomingDamageMultiplier` stat (float).
- **Shield absorption** — requiere `Shield` stat (int).
