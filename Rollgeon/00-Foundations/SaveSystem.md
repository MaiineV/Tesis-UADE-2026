---
title: SaveSystem
type: system
domain: 00-Foundations
status: done
tags: [foundation, save, persistence, service]
---

# SaveSystem

> Single game-wide save system (§15): in-memory cache `saveKey → state` +
> JSON flush to disk on configurable triggers. Implemented 2026-07-06.

## Shape

- `SaveSystem` — static class. `Register/Unregister(ISaveable)` (WeakReference
  registry), `CaptureAll/CaptureDirty/RestoreAll`, `Flush(SaveTrigger)`,
  `LoadFromDisk()` (schema check + corrupt-save degradation), `Clear()`.
- `SaveSettingsSO` — editor-configurable flush triggers, slots, async
  threshold. Asset: `Assets/Rollgeon/SaveSettings.asset`.
- `SaveSystemBootstrap` — `IPreloadableService` (Priority 200, Global) wired in
  `ServiceBootstrap.asset` ExtraServices. Maps lifecycle events to triggers:
  `OnRunStart→RunStart`, `OnFloorChanged→FloorEnd`, `OnRoomEntered→RoomEnd`
  (cache-only by default), `OnRunEnd→RunEnd`, `Application.wantsToQuit→Exit`.
- `ISaveFileStore` / `FileSaveStore` — IO seam (atomic tmp+rename writes);
  tests inject an in-memory store.
- Serialization: Odin `SerializationUtility` JSON (polymorphic cache values).

## Key semantics

- **New run = clean cache**: `SaveSystem.Clear()` is the first statement of
  `RunBootstrapper.StartRun` — beats every auto-restoring `Register()`.
- **Run-scoped saveables must Unregister on Dispose** ([[RunComboCounterState]],
  [[RunUnlockState]], `RunContext`, `InventoryService`): WeakReference purge
  alone can't prevent same-key duplicates across runs.
- **Meta excluded**: [[MetaProgressionService]] keeps its own write-through
  `FileMetaSaveStore` — the run save never overwrites meta.
- `LoadFromDisk` is implemented + tested but not invoked at bootstrap — the
  "Continue run" UX is still TBD.

## Registered saveables

`run.floor_index` (RunContext), `run.inventory` (InventoryService),
`run.combo_counter_state`, `run.unlock_tracker`, `audio.volumes`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Save/`
- Tests: `Assets/Scripts/Rollgeon/Patterns/Save/Tests/` (20 EditMode tests)

## External references

- TECHNICAL.md §15; [[ISaveable]]; [[ServiceBootstrapSO]]
