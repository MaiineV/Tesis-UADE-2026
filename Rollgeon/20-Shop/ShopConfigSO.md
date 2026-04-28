---
title: ShopConfigSO
type: so
domain: 20-Shop
status: done
tags: [shop, config, so]
---

# ShopConfigSO

> Global tuning SO for shops — pricing multiplier and variance, max slots, pedestal prefab, restock and discount knobs.

## Overview

Authority over **pricing**: `BasePrice` comes from the [[ShopPoolSO]] entry, this SO applies the multiplier and variance. Formula: `finalPrice = basePrice * PriceMultiplier * rng(1 - PriceVariance, 1 + PriceVariance)`, rounded and clamped to `>= 1`. Currently injected directly into the [[ShopManagerBootstrap]]; will be referenced from the [[RulesetSO]] (§14.7) once that lands.

Restock and first-purchase-discount fields are present but **not wired** in the MVP — follow-up §17.F.5.

## Serialized fields

- **Pricing:** `PriceMultiplier` (default `1.0`, `>= 0.1`), `PriceVariance` (`0..0.5`, default `0.1`).
- **Slots:** `MaxItemSlots` (`>= 1`, default `4`).
- **Restock (MVP no-op):** `AllowRestock`, `RestockCost`, `MaxRestocks`.
- **Discount (MVP no-op):** `FirstPurchaseDiscountPercent` (`0..100`).
- **Prefab:** `PedestalPrefab` (required) — must carry a [[ShopItemPedestalInteractable]]; `ItemVisualLocalOffset` (default `(0, 1.5, 0)`).

## API

```csharp
public int ResolvePrice(int basePrice, System.Random rng);
```

## Dependencies

**Uses:** [[ShopItemPedestalInteractable]] (via prefab contract).
**Used by:** [[ShopManagerService]] (price + slots + prefab), [[ShopManagerBootstrap]] (serialized ref), future [[RulesetSO]].

## Code

`Assets/Scripts/Rollgeon/Shop/ShopConfigSO.cs`

## External references

- TECHNICAL.md §17.F.3 (shop config), §14.7 (ruleset integration), §17.F.5 (restock follow-up).
