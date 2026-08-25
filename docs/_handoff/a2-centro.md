# A2 — PcOwnerAtRoomCenter

## Qué es

Precondición afirmativa "el owner está parado en el centro de la sala". La
negación (`PCComposite { Mode = Not }`) la arma quien wirea la fase 2 del
Croupier — no está en el scope de esta tarea.

## Archivos tocados

- `Assets/Scripts/Rollgeon/Grid/RoomCenterResolver.cs` (nuevo) — helper
  compartido, ver abajo.
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_TeleportToRoomCenter.cs`
  (editado) — `TryResolveDestination`/`IsFreeFor`/`IsBetter` salieron del
  nodo tal cual estaban y ahora vive como `RoomCenterResolver.TryResolve`.
  Sin cambio de comportamiento.
- `Assets/Scripts/Rollgeon/PreConditions/Concretes/PcOwnerAtRoomCenter.cs`
  (nuevo) — la precondición.
- `Assets/Scripts/Rollgeon/PreConditions/Tests/PcOwnerAtRoomCenterTests.cs`
  (nuevo) — EditMode.

## Dónde quedó el helper

`Rollgeon.Grid.RoomCenterResolver.TryResolve(IGridManager grid, Guid
selfGuid, GridCoord selfCoord, out GridCoord destination)`.

Es la misma matemática que ya tenía el nodo del teleport (bounding box de
`ThreatAreaShape.RoomTiles`, división entera, y si la casilla exacta del
centro está ocupada por otro, cae a la libre más cercana con desempate por
distancia al self). La muevo tal cual — cero cambio de comportamiento — para
que el nodo y la precondición no puedan divergir.

`PcOwnerAtRoomCenter.Evaluate` resuelve la posición del owner (`IGridManager`
vía `ServiceLocator`, igual que `PcTargetInRange`), llama al resolver con esa
misma coordenada como `selfCoord`, y compara el resultado contra la posición
actual. Si el grid no tiene sala, el owner no está registrado, o el
`OwnerGuid` es `Guid.Empty`, devuelve `false` sin excepciones.

## Test que ancla el requisito crítico

`PcOwnerAtRoomCenterTests.Evaluate_AfterTeleportNodeRuns_AgreesWithTeleportDestination`
corre `AINode_TeleportToRoomCenter.Tick` de verdad (con `GridManager` +
`MovementService` reales) y después evalúa la precondición sobre la posición
resultante — si el helper y el nodo alguna vez divergen, este test es el que
se rompe.

## Abierto / fuera de scope

- El wiring de la fase 2 del Croupier (`PcOwnerHpBelow` + `PCComposite{Not,
  [PcOwnerAtRoomCenter]}` en el árbol de "Pleno y color") lo hace quien
  integra — no toqué `CroupierAssetBuilder.cs` ni sus tests.
- No corrí Unity/tests (worktree sin `Library/`); falta correr el EditMode
  suite completo en el checkout principal.
