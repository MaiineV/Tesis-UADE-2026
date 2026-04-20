---
title: ActionDefinitionSO
type: so
domain: 02-Combat/Actions
status: done
tags: [combat, actions, so]
---

# ActionDefinitionSO

> `ScriptableObject` describing a single action: its id, type, energy
> cost, repetition flag, reroll budget, and the [[Effects-MOC|Effect]]
> (or backing SO reference) it triggers.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Actions/Action Definition")]
public class ActionDefinitionSO : SerializedScriptableObject {
    public string ActionId;        // "combo.full_house", "attack.basic", ...
    public ActionType Type;        // see ActionType enum

    public string DisplayName;     // UI label (localizable)
    public ScriptableObject BackingAsset; // ComboSO / ItemSO for typed dispatch

    public int  EnergyCost = 0;
    public bool BlockOnRepeat = true;
    public int  FreeRollCount = 1;
    public bool AllowsEnergyReroll = true;

    public EffectData Effect = new EffectData();
}
```

## BackingAsset vs Effect

- If `Effect.Effects` is non-empty → [[TurnManager]]`.TryExecute` runs
  the effect pipeline in-place.
- If `Effect.Effects` is empty and `BackingAsset` is set → "permit
  no-op": charge energy, mark used, and let the external dispatcher (combo
  executor, item system, AI) run the backing asset.

## `ActionId` convention

`<type>.<subtype>.<name>`, case-sensitive. Examples:
`combo.full_house`, `attack.basic`, `move`, `skill.heal`.

## Dependencies

- **Uses:** [[ActionType]], `EffectData` ([[04-Effects]]),
  [[ActionCatalogSO]] (dropdown source).
- **Used by:** [[TurnManager]], [[ActionCatalogSO]], combat HUD action
  buttons, combo executor.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Actions/ActionDefinitionSO.cs`

## External references

- Setup: `docs/setup/System#0100b_ActionEconomyRepetition.md`
- TECHNICAL.md: §12.6.0 ActionDefinitionSO
