---
title: IPlayerCombatActions
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, ui, interface]
---

# IPlayerCombatActions

> Surface the combat HUD calls when the player finishes an action or
> ends their turn. Maps directly to the [[CombatTurnFSM]] inputs
> `PlayerActionDone` / `PlayerEndTurn`.

## Overview

Kept narrow on purpose (Revision 2): separated from [[ICombatStarter]]
(lifecycle) and [[ICombatSignaller]] (AI) so each contract has the
minimum surface the consumer needs.

## API / Shape

```csharp
public interface IPlayerCombatActions {
    void SendPlayerAction();
    void EndPlayerTurn();
}
```

## Dependencies
**Used by:** [[PlayerActionButtonsView]], [[EndTurnButtonView]].
**Implemented by:** [[CombatHandoffService]] / [[CombatControllerAdapter]].

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/IPlayerCombatActions.cs`
