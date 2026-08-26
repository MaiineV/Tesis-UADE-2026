---
title: MovementDieService
type: service
domain: 18-Movement
status: done
tags: [movement, dice, service]
---

# MovementDieService

> Runtime impl de [[IMovementDieService]] (TECHNICAL.md §6.6).

## Overview
- **RNG propio** (`System.Random`, seed opcional para tests). No pasa por
  [[IDiceRoller]] a propósito: `EnchantedDiceRoller` aplica encantamientos por
  índice de slot del bag y [[DiceRoller]] consume la cola de rig del DevConsole —
  cualquiera acoplaría el dado a la build.
- **Generation counter**: `ClearActiveRange` / `OnCombatStart` / `OnCombatEnd`
  incrementan la generación; un reveal del presenter que llegue después queda
  como no-op.
- `CurrentType`: override runtime → `IPlayerService.CurrentHero.StartingMovementDie`
  → `MovementDieSO.DefaultType` (D4).
- Emite `EventName.OnMovementDieRolled [Guid, int face, DiceType]` en el reveal;
  nunca `OnDiceRolled`.

## API / Shape

```csharp
public sealed class MovementDieService : IMovementDieService, IDisposable {
    public MovementDieService(IPlayerService player, int? seed = null);
}
```

## Dependencies
**Uses:** `IPlayerService`, [[MovementDieSO]], `EventManager`
**Used by:** registrado por [[MovementDieServiceBootstrap]].

## Code
`Assets/Scripts/Rollgeon/Movement/Die/MovementDieService.cs`
Tests: `Assets/Scripts/Rollgeon/Movement/Tests/MovementDieServiceTests.cs`

## External references
- TECHNICAL.md: §6.6
- `docs/setup/movement-die.md`
