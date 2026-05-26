---
title: CombatHUDPayload
type: payload
domain: 12-UI/Screens
status: done
tags: [ui, screen, payload, combat]
---

# CombatHUDPayload

> Optional `IScreenPayload` passed by `CombatController` when pushing
> [[CombatHUDView]] on `OnCombatStart`. Carries the room instance id
> and an optional encounter display name for telemetry / UI.
> Plan §3.9 / §4.1.

## Shape

```csharp
public sealed class CombatHUDPayload : IScreenPayload {
    public Guid RoomInstanceId;
    public string EncounterDisplayName;
}
```

## Overview

Plain DTO. Currently informational — the HUD reads
`EncounterDisplayName` for header text and stamps `RoomInstanceId`
into telemetry events. The combat targeting / FSM does not read this
payload directly; it queries the [[CombatContext]].

## Dependencies

- **Uses:** [[IScreenPayload]].
- **Used by:** [[CombatHUDView]] (consumer), `CombatController`
  (producer).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/CombatHUDPayload.cs`
