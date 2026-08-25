# A3 — Candado del Croupier: `AnnounceOnce`

## Archivos tocados

- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_RotateBlock.cs` — flag `AnnounceOnce` + latch runtime.
- `Assets/Scripts/Rollgeon/Combat/AI/Tests/AINode_RotateBlockAnnounceOnceTests.cs` (nuevo) — EditMode, 3 casos.

Nada más se tocó. No se editó `CroupierAssetBuilder.cs` ni sus tests (integración a cargo de otro agente).

## Cómo quedó el latch

Campo nuevo `public bool AnnounceOnce = false;` (serializado, en la sección "Presentación") +
estado runtime `[NonSerialized] private bool _hasAnnounced;`. `[NonSerialized]` porque el estado
vive sólo en la copia runtime del árbol (`EnemyDataSO.CreateRuntimeAIRoot` →
`SerializationUtility.CreateCopy`), nunca en el asset — mismo patrón que
`AINode_SpawnReinforcements._hasSpawnedOnce`. Una pelea nueva arranca sin latchear.

Los tres caminos, sin tocar su forma:

- **`Tick`** (síncrono, EditMode / sin `CoroutineHost`): no cambió. Nunca llamó a la presentación
  (VFX/Feel), sólo aplica el bloqueo — así que no hay nada que latchear acá.
- **`TickCoroutine`** (play mode): tampoco cambió su estructura. Sigue llamando a `Tick` y, sólo si
  `BlockedSomething(context)` es cierto, entra a `PlayConfiscation`. El latch vive **dentro** de
  `PlayConfiscation`, que es el único lugar del nodo que efectivamente dispara feedback:
  ```csharp
  bool silent = AnnounceOnce && _hasAnnounced;
  _hasAnnounced = true;
  // ...steps sólo se arman si !silent...
  ```
  Como `PlayConfiscation` sólo se invoca cuando el tick bloqueó algo de verdad, el latch se cierra
  "cuando el aviso salió", no cuando el nodo tickeó: un turno sin bloqueo no gasta el único aviso.
- **`Opening`** (`IAIOpeningNode`): sigue llamando a `Tick` directamente, nunca a `TickCoroutine` ni
  a `PlayConfiscation`. Por diseño ya existente, la apertura nunca mostró VFX/Feel — así que no
  consume el latch, y el primer turno real del jefe (que sí pasa por `TickCoroutine`) es el que
  dispara el único aviso. Esto es justamente lo que evita que "Opening se coma el único aviso".

Con `AnnounceOnce = false` (default) el comportamiento es idéntico al de antes: `_hasAnnounced`
nunca se consulta como gate real porque `silent` siempre da `false`.

## Tests

`AINode_RotateBlockAnnounceOnceTests` (EditMode, `DiceBlockService` real + `IPlayerService` y
`IFeedbackService` fakeados, sin `TurnManager` registrado — mismo patrón que
`CroupierConfiscationTests` / `AINode_RotateBlockDirectedTests`):

1. `AnnounceOnce_ThreeTicks_AnnouncesOnce_ButBlocksAllThree` — 3 ticks con `AnnounceOnce = true`:
   el candado bloquea 1 dado los 3 turnos, pero `IFeedbackService.RequestFeedbackBlocking` se
   invoca una sola vez.
2. `AnnounceOnceFalse_ThreeTicks_AnnouncesEveryTime` — default: 3 ticks, 3 avisos.
3. `FirstEmissionBlocksNothing_DoesNotConsumeTheAnnounce` — con un `DirectedIndex` que devuelve -1
   en el turno 1 (nada que bloquear) y un índice válido desde el turno 2: el aviso sale recién en
   el turno 2 (el primer bloqueo real), y el turno 3 ya queda mudo.

Se corre `TickCoroutine` (no `Tick`) porque es el único camino que dispara feedback; se drena el
`IEnumerator` a mano con un `while (routine.MoveNext())` — no hace falta `TurnManager` porque sin
él la corutina hace `yield break` apenas dispara el request.

## Abierto / pendiente

- No verifiqué en Unity (sin `Library/` en este worktree, según instrucción). Falta correr el
  Test Runner en el checkout principal.
- La ficha del Croupier (`CroupierAssetBuilder.cs`) todavía no setea `AnnounceOnce = true` en el
  nodo de bloqueo — eso queda para la integración que hace el otro agente, junto con el resto del
  wiring del jefe.
