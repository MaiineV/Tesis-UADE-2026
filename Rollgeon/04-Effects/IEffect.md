---
title: IEffect
type: interface
domain: 04-Effects
status: done
tags: [effects, interface]
---

# IEffect

> Minimal marker interface [[BaseEffect]] implements. Lets
> [[EffectData]]`.Effects` hold polymorphic effect lists with
> `[SerializeReference]` + `[OdinSerialize]`.

## Shape

```csharp
public interface IEffect {
    string GetEffectName();
    SelectionSettings GetSelection();
    bool HasSelectionRequirement();
    bool RequiresSelectionAt(SelectionTiming timing);
    bool Apply(EffectContext context);
    bool ValidateSelection(TargetSelectionResult result,
                           Guid ownerGuid, out string error);
}
```

## Dependencies

- **Used by:** [[BaseEffect]] and every concrete effect.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/IEffect.cs`

## External references

- TECHNICAL.md: §8.3 IEffect
