---
title: PendingRunRequest
type: class
domain: 10-Run
status: done
tags: [run, bootstrap, scene-bridge]
---

# PendingRunRequest

> Static carrier for the data needed to start a run, set in
> `01_MainMenu` (BuildSelectionScreen) and consumed in `02_Gameplay`
> (GameplayBootstrapper).

## Overview

Not registered in [[ServiceLocator]] because [[ServiceScope]] `Run`
doesn't exist until [[RunBootstrapper]]`.StartRun` runs in the gameplay
scene. The static fields fill the cross-scene gap.

## Shape

```csharp
public static class PendingRunRequest {
    public static ClassHeroSO   SelectedHero  { get; }
    public static Guid          RunId         { get; }
    public static string        RulesetId     { get; }
    public static DiceBagSO     BuiltDiceBag  { get; }
    public static IReadOnlyList<ItemSO> StartingItems { get; }
    public static bool          HasRequest    { get; }

    public static void Set(ClassHeroSO hero, Guid runId, string rulesetId,
                           DiceBagSO builtDiceBag = null,
                           IReadOnlyList<ItemSO> startingItems = null);
    public static void Clear();
}
```

## Dependencies

- **Uses:** [[ClassHeroSO]], `DiceBagSO`, `ItemSO`.
- **Used by:** `BuildSelectionScreen` (writes), [[GameplayBootstrapper]]
  (reads + clears).

## Code

`Assets/Scripts/Rollgeon/Run/PendingRunRequest.cs`

## External references

- TECHNICAL.md: §1.1.3 Run lifecycle — scene bridge
