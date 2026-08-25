# A1 — AINode_SpawnRoomObjects: separación + re-sorteo + accesor de lectura

## Archivos tocados

- `Assets/Scripts/Rollgeon/Combat/Rooms/AINode_SpawnRoomObjects.cs`
  - `MinSpacing` (int, default 0) — separación mínima Chebyshev entre ranuras de
    `Placement.ScatteredFree`, y contra la casilla del jefe. Implementado dentro de
    `ScatteredSlots`: sorteo goloso, cada candidata elegida poda del pool todo lo que
    quede a Chebyshev < `MinSpacing`. Con `0` es exactamente el sorteo pelado de antes
    (no se aplica a ningún otro `Placement`).
  - `ResolveSlotsEachSpawn` (bool, default false) — implementado dentro de `RefillSlots`.
    Con `false` (default) el comportamiento no cambia: la ranura repone en su
    `slot.Coord` recordado. Con `true`, en cuanto alguna ranura necesita reponerse ese
    tick, se resuelve un `ResolveSlotCoords` fresco UNA sola vez (no por ranura) y se
    reasigna por índice a cada ranura vacía antes de spawnear — así una ola entera que
    muere junta se repone junta contra el mismo sorteo, sin sorteos sucesivos pisándose.
  - Accesor `LiveObjects()` — ver firma abajo.
- `Assets/Scripts/Rollgeon/Combat/Rooms/Tests/AINode_SpawnRoomObjects_ScatterSpacingTests.cs` (nuevo)
- `Assets/Scripts/Rollgeon/Combat/Rooms/Tests/AINode_SpawnRoomObjects_ResolveSlotsEachSpawnTests.cs` (nuevo)

No se tocó nada en `Assets/Scripts/Editor/` ni en ningún otro sistema.

## Firma exacta del accesor

```csharp
public IEnumerable<(Guid Guid, GridCoord Coord)> LiveObjects()
```

Vive en `Rollgeon.Combat.Rooms.AINode_SpawnRoomObjects`. Devuelve un elemento por cada
ranura ocupada (`ObjectGuid != Guid.Empty`); ranuras vacías o rotas no aparecen. No
expone la lista mutable interna (`_slots`) — es un iterator method armado sobre ella.
Antes del primer `Tick`/`Opening` devuelve una secuencia vacía (no null).

## Decisiones / cosas a tener en cuenta

- **Granularidad de `ResolveSlotsEachSpawn`**: el re-sorteo pasa por ranura vacía, no
  por "toda la ola siempre". Si en un tick dado sólo una ranura está vacía (p. ej. un
  jugador rompió una sola bomba y las demás siguen en pie), esa ranura sola recibe una
  coordenada nueva del `ResolveSlotCoords` fresco — las que siguen en pie no se mueven
  (no tiene sentido teletransportar un objeto vivo). Esto es consistente con el resto
  del nodo: `CollectBroken`/`RefillSlots` siempre operan ranura por ranura.
- El `ResolveSlotCoords` fresco no excluye las casillas que en ESE MISMO tick están
  siendo liberadas antes de llamarlo — como `CollectBroken` corre antes que
  `RefillSlots` dentro de `Tick`, cuando se llama al resolve fresco esas casillas ya
  están desocupadas en el grid, así que pueden volver a salir sorteadas (no hay
  penalización por "acabo de estar ahí"). No me pareció que la ficha pidiera lo
  contrario; si se quiere excluir la posición anterior explícitamente, hay que sumar
  ese filtro a mano en el llamador o acá.
- `RoomObjectArmorService`: no toqué `PublishArmor`. Sigue publicando `LastObjectGuid`
  por índice de ranura, que no cambia con ningún flag nuevo — la contabilidad de
  armadura no se ve afectada por dónde esté parado el objeto, sólo por su guid.
- `MinSpacing` sólo se declaró con `[ShowIf(Pattern, ScatteredFree)]` en el inspector;
  en código no hace nada si `Pattern` es otro (el resto de los métodos de patrón no lo
  leen).
- Tests corridos: no pude ejecutarlos (worktree sin `Library/`, tal como se indicó).
  Están escritos contra las mismas convenciones que
  `AINode_SpawnRoomObjects_DoorFrontsTests.cs` y `RoomObjectArmorTests.cs`
  (`GridManager` + `NavGraph.Rect` real, `AttributesManager` real, sin mockear nada).
  Falta que alguien con el editor abierto los corra una vez.
