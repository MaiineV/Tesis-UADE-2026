# Foundation#0004 — Effects + PreConditions pipeline — Setup

Branch: `sprint03/foundation/0004-effects-preconditions`.
Plan: repo-local `plan.md` (gitignored — no viaja en el merge).
Spec: `TECHNICAL.md` §8 (Effects + PreConditions), §9 (Stored Values), §11 (Selection / Targeting), §13.6.1 (regla de serialización polimórfica).

Upstream dependencies merged on `develop` al momento de este dev:

- **Foundation#0001** — `Patterns.ServiceLocator`, `Patterns.EventManager`, `Patterns.EventName`, `Patterns.TypedEvent<T>`.
- **Foundation#0003** — `Rollgeon.Attributes` (IAttribute / IModifiable / Modifier&lt;T&gt; / AttributesManager).
- **Foundation#0005** — `Rollgeon.Patterns.Catalogs.BaseCatalogSO`, `Rollgeon.Patterns.Bootstrap.ServiceBootstrapSO`.

Esta foundation **no** necesita Foundation#0002 (FSM).

---

## 1. Compile check

1. Abrir el proyecto en Unity (versión del proyecto — ver `ProjectSettings/ProjectVersion.txt`).
2. Esperar a que termine el import de assets y la recompilación de scripts.
3. **Expectativa**: **0 errores** y 0 warnings nuevos introducidos por esta tarea.
4. Si hay errores:
   - CS0246 sobre `Patterns.ServiceLocator` / `Rollgeon.Attributes.*` → Foundation#0001 / #0003 no mergearon; revisar `git log --all` en `develop`.
   - CS0246 sobre `Sirenix.OdinInspector.*` / `Sirenix.Serialization.*` → los DLLs de Odin no están en `Assets/Plugins/Sirenix/Assemblies/`. Re-ejecutar el merge.

## 2. Test pass

1. `Window → General → Test Runner` (Unity).
2. Tab **EditMode**.
3. Buscar el nodo `Rollgeon.Effects.Tests` y ejecutar **Run All** sobre él.
4. **Expectativa**: todos los tests verdes. En particular, el test **`EffectData_PolymorphicRoundTrip_WithOdin`** debe pasar — si falla, hay un bug silencioso de serialización que debe corregirse antes de construir cualquier sistema sobre esta foundation (ver §13.6.1).

Lista de tests incluidos:
- `EffectData_TryExecute_PreConditionsFail_DoesNotExecuteEffects`
- `EffectData_TryExecute_AllPreConditionsPass_ExecutesAllInOrder`
- `EffectData_TryExecute_EffectReturnsFalse_ShortCircuitsRemainingEffects`
- `BaseTargetQuery_TQSelf_ReturnsOwnerAsTarget`
- `BaseEffect_ApplySelectionValidation_FailsWhenCancelledAndNotSkippable`
- `PCComposite_AndOrNot_EvaluationMatrix`
- `EffectContext_TryGetTriggerContext_ReturnsTrueOnlyForMatchingSubtype`
- `EffectData_PolymorphicRoundTrip_WithOdin` **(crítico)**
- `BaseBehavior_StoredValues_SetGetClearRoundTrip`
- `EffDamage_Example_WritesFloatingDamageToBehavior`

## 3. No hay SOs que crear

Esta foundation **no** introduce nuevos `ScriptableObject` concretos. `EffectData` es una clase plana que vive inline dentro de otros SOs (BehaviorSO, AbilitySO, ComboSO, ItemSO, …) que crean los sistemas downstream — acá sólo se entrega la columna vertebral.

## 4. Smoke test manual (opcional)

Para validar el flujo end-to-end en una escena de pruebas:

1. Crear un `MonoBehaviour` temporal (p. ej. `TempEffectSmoke : MonoBehaviour`) con el siguiente `Start()`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.PreConditions;

public class TempEffectSmoke : MonoBehaviour
{
    void Start()
    {
        var data = new EffectData
        {
            PreConditions = new List<BasePreCondition>
            {
                new PCComposite { Mode = CompositeMode.And }
            },
            Effects = new List<IEffect> { new EffDamage(), new EffHeal() }
        };

        var ctx = new EffectContext
        {
            SourceGuid = Guid.NewGuid(),
            TargetGuid = Guid.NewGuid(),
            lastResult = true,
        };
        var preCtx = new PreConditionContext
        {
            OwnerGuid = ctx.SourceGuid,
            OpponentGuid = ctx.TargetGuid,
        };

        bool ok = data.TryExecute(ctx, preCtx);
        Debug.Log($"Smoke test TryExecute → {ok}");
    }
}
```

2. Agregar el componente a un GameObject en una escena de pruebas, entrar a Play mode.
3. **Expectativa en consola**:
   - `[STUB DamagePipeline] <src> → null: 10 damage` (EffDamage stub).
   - `[EffHeal example] source <src> heals <src> for 10` (EffHeal).
   - `Smoke test TryExecute → True`.
4. Borrar el componente y el script cuando termine — no forma parte de la foundation.

## 5. Verificación post-merge del dev

Checks rápidos que el orquestador (o el reviewer) puede hacer:

- `git -C D:/GitHub/TesisUade log --oneline -5 develop` debe mostrar el merge commit de `sprint03/foundation/0004-effects-preconditions`.
- `Assets/Scripts/Rollgeon/Effects/` contiene:
  - `IEffect.cs`, `BaseEffect.cs`, `BaseEffect.Generic.cs`, `EffectData.cs`, `EffectContext.cs`, `CapabilityInterfaces.cs`
  - `Readers/IEntityReader.cs`, `Readers/IPlayerReader.cs`
  - `Selection/…` (8 archivos — ver plan §3.2).
  - `Concretes/EffDamage.cs`, `Concretes/EffHeal.cs`, `Concretes/DamageArgs.cs`, `Concretes/HealArgs.cs`
  - `Stubs/…` (9 archivos — ver plan §3.5).
  - `Tests/EffectsPipelineTests.cs` + `Tests/Rollgeon.Effects.Tests.asmdef`.
- `Assets/Scripts/Rollgeon/PreConditions/` contiene `BasePreCondition.cs`, `PreConditionContext.cs`, `PCComposite.cs`.

## 6. Wiring en scenes / prefabs / input maps

**Nada.** La foundation es 100% código — no hay scenes, prefabs, ScriptableObjects serializados, ni input actions que registrar. Los consumers downstream (T100b, T97a, T99, T103) hacen su propio wiring cuando armen sus SOs.

---

## Notas para el merger / reviewer

- Los archivos bajo `Assets/Scripts/Rollgeon/Effects/Stubs/` están **marcados explícitamente como STUBS** con un comentario `// [STUB] — reemplazado por <worktree>` en el header de cada uno. Viven en namespace `Rollgeon.Effects.Stubs` para aislarlos. Cuando las foundations downstream (Entities, Behaviors, Combat) mergeen, el dev de cada una debe eliminar el stub correspondiente y re-apuntar los `using`.
- `ServiceLocator` actualmente vive en el namespace `Patterns` (no `Rollgeon.Patterns`) — así lo mergeó Foundation#0001. Si se renombra en un futuro commit, hay que actualizar el `using Patterns;` en `TQ_AllEnemies.cs`.
- `plan.md` del worktree está excluido por `.gitignore` (regla global del repo) — el merger lo ignora intencionalmente.
