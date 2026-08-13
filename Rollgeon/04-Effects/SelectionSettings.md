---
title: SelectionSettings
type: system
domain: 04-Effects
status: done
tags: [effects, selection, aoe]
---

# SelectionSettings

> Embedded settings object on every [[BaseEffect]] that declares whether
> the effect needs target selection, which tiles are valid (SlotState +
> EntityFilter + Range), how many targets (constant or dynamic via
> [[ISelectionCountReader]]), and whether the effect is Single-target or
> AoE (anchor + area expansion).

## Shape

```csharp
[Serializable]
public class SelectionSettings {
    public SlotState        SlotState;        // Self | Occupied | Empty | Both
    public SelectionTiming  Timing;           // BeforeRoll | AfterRoll
    public EntityFilterMask EntityFilter;     // Allies | Enemies | ... (si Occupied/Both)
    public bool             IsGlobal;         // toda la sala vs Range desde el owner
    public int              Range;            // 1..20
    public RangeMode        RangeMode;        // Manhattan | PathReachable

    public TargetMode       TargetMode;       // Single | Aoe (default Single)
    public AoeShape         AoeShape;         // Radius | Custom (solo Aoe)
    public int              AoeRadius;        // diamante Manhattan desde el ancla
    public int              PatternRows, PatternCols;   // patrón bool-grid (Custom)
    public Vector2Int       PatternCenter;    // celda del patrón apoyada en el ancla
    public bool[]           PatternFlat;      // [BoolGrid] — grilla editable en inspector

    public bool             IsConstantSelectionCount;
    public int              SelectionCount;   // count constante (1..16)
    [OdinSerialize, SerializeReference]
    public ISelectionCountReader SelectionCountReader;  // count dinámico

    public bool             AutoResolve;      // random entre válidos, sin interacción
    public bool             AutoAccept;       // auto-confirma al llegar al count

    public int GetSelectionCount(ReadInfo info);        // PICKS requeridos (AoE => 1)
    public List<TargetRef> ResolveValidTiles(GridCoord ownerPos, Guid ownerGuid);
    public List<GridCoord> ResolveRangeTiles(GridCoord ownerPos, Guid ownerGuid);
    public List<TargetRef> ExpandAoe(GridCoord anchor, Guid ownerGuid);
    public TargetSelectionResult AutoResolveTargets(GridCoord ownerPos, Guid ownerGuid);
}
```

## Single vs AoE

- **Single** — el jugador hace N picks individuales
  (N = `GetSelectionCount`). Comportamiento histórico.
- **Aoe** — el jugador elige UNA celda ancla entre las válidas
  (`ResolveValidTiles`, igual que Single) y el efecto se expande alrededor:
  - `AoeShape.Radius`: celdas a distancia Manhattan ≤ `AoeRadius` del ancla.
  - `AoeShape.Custom`: patrón bool-grid relativo al ancla (offset
    `(col - PatternCenter.x, fila - PatternCenter.y)`), editable con el
    drawer `BoolGridAttribute`.
  - El área se **clipea a la grilla, NO al `Range` del caster** (una
    explosión en el borde del alcance derrama más allá), y cada celda
    re-aplica los filtros `SlotState` + `EntityFilter` (un heal AoE no
    incluye enemigos). El ancla entra siempre.

La expansión ocurre en **exactamente 2 puntos**: `SelectionController.Complete()`
(flujo manual) y `SelectionSettings.AutoResolveTargets()` (flujo auto), ambos vía
`ExpandAoe`. Los consumidores ([[EffDealDamage]], chain, FSM) reciben
`SelectedTargets` ya expandidos.

## Cantidad de targets

`GetSelectionCount(ReadInfo)` = **picks requeridos**, no targets finales:

- `TargetMode.Aoe` → siempre `1` (el ancla).
- `IsConstantSelectionCount` → `SelectionCount`.
- dinámico → `SelectionCountReader?.Read(info) ?? 1` — ver
  [[ISelectionCountReader]] (`StatCountReader`, `AliveEnemiesCountReader`).

## Timing

- `BeforeRoll` — el jugador elige el target antes de la tirada de dados
  (típico de ataques).
- `AfterRoll` — la selección se resuelve después de la tirada.

## UI

El hover de un ancla válida en modo AoE pinta el área afectada con el
estilo `"aoe"` (naranja, configurable en `TileHighlightServiceBootstrap`).
Como el área puede exceder el rango pintado, la limpieza del overlay usa
`ClearAll` + repintado (no alcanza con repintar las celdas válidas).

## Dependencies

- **Uses:** [[SlotState]], [[EntityFilterMask]], [[SelectionTiming]],
  [[TargetSelectionResult]], [[ISelectionCountReader]], `IGridManager`,
  `IMovementService` (RangeMode.PathReachable), `IEntityQueryService`.
- **Used by:** [[BaseEffect]], [[EffectData]], `SelectionController`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Selection/SelectionSettings.cs`
- Sibling types: `TargetMode.cs`, `AoeShape.cs`, `BoolGridAttribute.cs`,
  `ISelectionCountReader.cs`, `Readers/StatCountReader.cs`,
  `Readers/AliveEnemiesCountReader.cs`, `SelectionTiming.cs`,
  `TargetRef.cs`, `TargetSelectionResult.cs`, `EntityFilterMask.cs`
- Editor: `Assets/Scripts/Editor/Drawers/BoolGridAttributeDrawer.cs`
- Tests: `SelectionSettingsAoeExpansionTests.cs`,
  `SelectionCountReaderTests.cs`, `SelectionControllerAoeTests.cs`,
  `SelectionSettingsRangeModeTests.cs`

## External references

- TECHNICAL.md: §11 Selection pipeline (§11.2)
- Referencia de diseño: SelectionSettings de Bot-Game (TargetMode,
  count reader polimórfico, patrón AoE)
