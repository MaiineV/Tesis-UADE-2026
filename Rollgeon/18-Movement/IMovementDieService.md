---
title: IMovementDieService
type: interface
domain: 18-Movement
status: done
tags: [movement, dice, service, interface]
---

# IMovementDieService

> Dado de Movimiento (TECHNICAL.md §6.6): tirada propia, separada de los 5 dados
> de la build, cuya cara define el rango de casillas alcanzables del Movimiento en
> combate.

## Overview
Run-scoped. `Roll` computa la cara ya mismo y difiere el reveal (callback +
`OnMovementDieRolled` + rango activo) al `IMovementDiePresenter` si hay uno
(`MovementDieView`), o revela sincrónico si no. El rango activo se publica
**en el reveal** para que el hover preview no spoilee la cara.
`ClearActiveRange` invalida reveals tardíos (cancel, fin de acción, fin de combate).

## API / Shape

```csharp
public interface IMovementDieService {
    DiceType CurrentType { get; }           // override → ClassHeroSO.StartingMovementDie → D4
    int LastFace { get; }
    void SetTypeOverride(DiceType? type);
    void Roll(Guid playerGuid, Action<int> onRevealed);
    bool TryGetActiveRange(Guid playerGuid, out int range);
    void ClearActiveRange();
    void SetPresenter(IMovementDiePresenter presenter);
    event Action<Guid, int> OnRolled;
    event Action OnCleared;
}

public interface IMovementDiePresenter {
    bool TryPresent(DiceType type, int face, Action onRevealed);
    void Abort();
}
```

## Dependencies
**Uses:** [[DiceType]], [[MovementDieSO]]
**Used by:** `CombatHandoffService` (roll + cobro + gate post-reveal),
`SelectionSettings.ResolveEffectiveRange`, `MovementDieView`.

## Code
`Assets/Scripts/Rollgeon/Movement/Die/IMovementDieService.cs`,
`Assets/Scripts/Rollgeon/Movement/Die/IMovementDiePresenter.cs`

## External references
- TECHNICAL.md: §6.6
