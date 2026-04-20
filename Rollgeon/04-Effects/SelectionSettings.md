---
title: SelectionSettings
type: system
domain: 04-Effects
status: done
tags: [effects, selection]
---

# SelectionSettings

> Embedded settings object on every [[BaseEffect]] that declares whether
> the effect needs player / AI target selection and, if so, which query
> seeds it and when.

## Shape

```csharp
[Serializable]
public class SelectionSettings {
    public bool              RequiresSelection;
    public SelectionTiming   Timing;          // OnAnnounce | OnApply
    public BaseTargetQuery   DefaultQuery;    // seeds the candidate pool
    public bool              IsSkippable;
    public int               GetSelectionCount(ReadInfo info);
    // ... plus UI affordance flags (single / multi, min / max, etc.)
}
```

## Timing

- `OnAnnounce` — the player chooses the target before the ability fires
  (typical for attacks).
- `OnApply` — the target is chosen just-in-time inside `ApplyEffect`
  (used by follow-up effects).

## Dependencies

- **Uses:** [[BaseTargetQuery]], [[TargetQueries]], [[ReadInfo]],
  `SelectionTiming`, [[TargetSelectionResult]].
- **Used by:** [[BaseEffect]], [[EffectData]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Selection/SelectionSettings.cs`
- Sibling types: `SelectionTiming.cs`, `TargetRef.cs`,
  `TargetSelectionResult.cs`, `EntityFilterMask.cs`

## External references

- TECHNICAL.md: §11 Selection pipeline
