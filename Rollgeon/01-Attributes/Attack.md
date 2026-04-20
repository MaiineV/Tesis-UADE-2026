---
title: Attack
type: system
domain: 01-Attributes
status: done
tags: [attributes, stat, combat]
---

# Attack

> Concrete Attack stat (`int`). Base damage value that enemies use when
> resolving strikes. `Attack = 0` marks pure supports like the Auditor.

## Shape

```csharp
public sealed class Attack : BaseAttribute<int> {
    public Attack();
    public Attack(int initial);
    public override string GetAttributeName() => "Attack";
    protected override BaseAttribute<int> CreateDuplicate() => new Attack(_rawValue);
}
```

## Clamp contract

- Raw value is unclamped.
- `BasicEnemyAI` skips the attack step entirely when
  `Attack <= 0`, so support archetypes never trigger the damage pipeline.

## Dependencies

- **Uses:** [[BaseAttribute]].
- **Used by:** `BasicEnemyAI`, boss attack behaviors, [[DamagePipeline]]
  (reads attacker's Attack), HUD enemy panel.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Stats/Attack.cs`

## External references

- Setup: `docs/setup/System#0012b_EnemyAIReal.md`
- TECHNICAL.md: §7.1 Entities — Attack
