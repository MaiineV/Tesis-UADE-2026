---
title: ClassHeroSO
type: so
domain: 06-Heroes
status: done
tags: [heroes, so, class]
---

# ClassHeroSO

> `ScriptableObject` describing a playable hero class. Minimal skeleton
> — identity + [[ContractSheet]]. Extra fields (stats, passives, dice
> bag, portrait) are declared as stubs for the future Hero Template
> task.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Heroes/Class Hero")]
public class ClassHeroSO : SerializedScriptableObject {
    public string EntityId;      // "hero.warrior"
    public string DisplayName;   // "Guerrero"
    public string Description;   // UI blurb

    [OdinSerialize, Required] public ContractSheet Sheet = new();

    // [STUB] — elevated by future Hero Template task:
    public int    BaseMaxHp;
    public int    BaseSpeed;
    public Sprite Portrait;
    public ScriptableObject StartingDiceBagRef;
    public ScriptableObject PassiveRef;
}
```

## Stub fields

The inspector shows a warning box on the stub block. Sprint 03 only
reads `EntityId`, `DisplayName`, `Description`, `Sheet`. Referencing the
stubs in gameplay is disallowed until the Hero Template task elevates
them.

## Dependencies

- **Uses:** [[ContractSheet]].
- **Used by:** [[IPlayerService]]`.CurrentHero`, class selection screen,
  [[RunBootstrapper]]`.StartRun`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/ClassHeroSO.cs`

## External references

- Setup: `docs/setup/UI#0098_ClassSelectionScreen.md`
- TECHNICAL.md: §4.1 / §5.3 Class hero
