---
title: ItemType
type: enum
domain: 24-Items
status: done
tags: [items, enum]
---

# ItemType

> Enum that splits items into the two top-level categories the inventory tracks separately.

## API / Shape

```csharp
public enum ItemType { Passive, Active }
```

- `Passive` — auto-applied on pickup; binds [[PassiveItemHook]]s and persistent modifiers, lives in `PassiveItems`.
- `Active` — player-triggered, cooldown-gated; lives in `ActiveItems` with a max-slot cap.

## Dependencies

**Used by:** [[ItemSO]], [[InventoryService]], [[ItemCatalogSO]] (`GetByType`), inventory HUD.

## Code

`Assets/Scripts/Rollgeon/Items/ItemType.cs`
