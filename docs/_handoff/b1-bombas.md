# B1 — Bombas del Croupier (AINode_BombField)

## Archivos tocados

- `Assets/Scripts/Rollgeon/Combat/Rooms/AINode_BombField.cs` — nodo nuevo.
- `Assets/Scripts/Rollgeon/Combat/Rooms/Tests/AINode_BombFieldTests.cs` — tests EditMode.

No se tocó `Assets/Scripts/Editor/` ni ningún otro jefe.

## Campos autorables (para cablear el builder)

- `RoomObjectDefinitionSO Definition` — la bomba. **Tiene que traer `RespawnDelayTurns = 0`**: es
  lo que deja que la siembra del mismo tick repare tanto lo detonado como lo roto a mano, en la
  misma pasada. `OnDeathHazard` en null (romperla a mano no deja fuego).
- `SpecialTileDefinitionSO FireTile` — el fuego que deja la detonación.
- `int Count` — cantidad de bombas por ciclo. Default 5.
- `int Spacing` — separación mínima autorada entre bombas. Default 2. El nodo le suma 1 antes de
  pasarla al sorteo interno (ver "Cómo se resolvió la cruz").
- `int FireDurationRounds` — rondas que arde el fuego. 0 = usa el `DefaultDurationRounds` del SO de
  `FireTile`.
- `int IgnitionDamage` — daño de la detonación a quien siga parado en la cruz cuando prende.
  También es el número que viaja en la marca de `IThreatenedAreaService` (cosmético/telegraph).
  Default 20.
- `string ChannelPrefix` — prefijo del canal de amenaza por bomba (prefijo + guid). Default
  `"bomb."`. Sólo importa si el mismo jefe usa `AINode_BombField` más de una vez con canales que
  puedan chocar.

No se agregaron campos de feedback (spawn/detonación) en esta pasada — quedan afuera a propósito,
ver "Abierto" más abajo.

## Cómo se resolvió la detonación

El nodo asume que el árbol del jefe lo tickea **una vez por ciclo** (los 3 turnos del Croupier).
Cada `Tick()`:

1. **Detona** lo que sobrevivió del ciclo anterior: por cada bomba que el nodo venía trackeando
   (guid → cruz), chequea su `Health` actual. Si sigue viva, planta `FireTile` en su cruz vía
   `ISpecialTileService.Place`, cobra `IgnitionDamage` si el jugador está parado adentro, despawnea
   su pawn (`IEntityVisualService.Despawn`), la saca del grid (`IGridManager.Unregister`) y le fuerza
   `Health = 0` — este último paso es lo que le avisa al `AINode_SpawnRoomObjects` interno que esa
   ranura está rota, ya que su propio `CollectBroken` sólo mira `Health`. Si ya estaba rota (el
   jugador la reventó a mano durante el ciclo), no prende nada: sólo limpia su marca de amenaza.
2. **Siembra** `Count` bombas nuevas delegando a un `AINode_SpawnRoomObjects` interno (no
   serializado, armado una vez en runtime) configurado en `Placement.ScatteredFree` +
   `ResolveSlotsEachSpawn = true`. Como `RespawnDelayTurns = 0`, este mismo `Tick()` repone tanto las
   ranuras que acabamos de detonar como las que el jugador rompió a mano — sin distinción.
3. **Marca** la cruz de cada bomba nueva bajo su propio canal (`AINode_TelegraphMark.SourceKey`,
   reusado tal cual) en `IThreatenedAreaService` + `IThreatOverlayService`, y la guarda en un
   diccionario interno `Guid → cruz`.

**Decisión de diseño clave**: la autoridad de "¿sigue armada esta bomba?" es la `Health` real de la
entidad, consultada en el momento (`LiveCrosses(AttributesManager)`), no el estado persistido en
`IThreatenedAreaService`. Esto es lo que permite que romper una bomba a mitad de ciclo "levante su
cruz" **al instante** (sin esperar al próximo tick del jefe): no hay ningún listener de eventos de
muerte enganchado al nodo — se evitó a propósito, porque un `AINode` no tiene un hook de lifecycle
claro para des-suscribirse entre peleas, y el patrón establecido en el repo para eso son servicios
con `Initialize/Shutdown` (`CroupierWheelService`, `BandidaJackpotService`, etc.), no nodos de
árbol. El servicio de amenaza sigue existiendo sólo como capa visual/cosmética (overlay + estado
consultable por UI), y se limpia (`Clear`) recién en el siguiente `Tick()`, en el mismo paso que
detona o descarta cada bomba.

## Cómo se resolvió la cruz

La cruz de una bomba es su casilla + las 4 ortogonales (`GridCoord.Neighbors4()`), filtradas contra
`IGridManager.InBounds` + `IsWalkable` — los brazos que caen en pared o fuera del mapa no se marcan.
No se tocó `ThreatShape`/`ThreatAreaShape`: la forma se calcula a mano en el nodo (`ComputeCross`),
porque agregar un shape nuevo al enum solo para esto hubiera sido más código que la cuenta directa, y
el enum es serializado (agregar al medio rompe jefes ya autorados — este caso ni siquiera necesitaba
tocar la punta).

**Por qué `Spacing + 1` y no `Spacing` tal cual**: la separación mínima que ya existe en
`AINode_SpawnRoomObjects.MinSpacing` es Chebyshev. Con una cruz de 5 casillas, dos centros alineados
en la misma fila/columna a Chebyshev = 2 exacto (ej. `(0,0)` y `(2,0)`) siguen tocando cruces —
comparten `(1,0)`. Sumarle 1 a la separación autorada antes de pasarla al sorteo interno fuerza
Manhattan ≥ 3 entre centros, que sí garantiza cero solape sin importar cómo caiga el sorteo (se
verifica en `WithSpacingTwo_NoTwoCrossesShareATile`, determinístico, no depende del seed).

## Tests

`AINode_BombFieldTests.cs`, contra los servicios reales (`ThreatenedAreaService`,
`SpecialTileService`) más un `SpyDamagePipeline` y un `SpyThreatOverlay` no-op:

- Primer tick: siembra `Count`, marca `Count` cruces (3 a 5 casillas), no prende nada.
- Cruz recortada en una sala 2x2 (toda esquina, cruz de 3 exacto).
- Romper una bomba entre ticks: `LiveCrosses` la saca al instante, las demás quedan intactas.
- Segundo tick: las vivas prenden su cruz entera y salen del grid; la rota no prende nada y también
  sale del grid (vía el `CollectBroken` del spawner interno). El ciclo siguiente resiembra con guids
  nuevos.
- `Spacing = 2` ⇒ ninguna cruz comparte casilla con otra (6 bombas, sala 15x15).
- `Definition` o `FireTile` en null: no explota, log de warning, `Succeeded`.

## Abierto

- **Sin feedback de spawn/detonación todavía.** `AINode_SpawnRoomObjects.SpawnFeedbackId` no está
  expuesto por composición (el spawner interno no es autorable desde afuera) y no se agregó un
  gesto propio para la detonación. Si el jefe necesita animación acá, hay que decidir: ¿exponer un
  `DetonateFeedbackId` con `TickCoroutine` bloqueante (patrón de `AINode_IgniteArea`/
  `AINode_SpawnRoomObjects`), o dejarlo silencioso como está?
- **`IgnitionDamage` duplicado de intención**: es tanto el daño real que cobra `ChargeIgnitionDamage`
  como el número que viaja en la marca de `IThreatenedAreaService.Mark` (que hoy es puramente
  cosmético, nadie la consume vía `TryConsume`). Si en algún punto se quiere que el overlay muestre
  un número distinto al daño real, hace falta separar los dos campos.
- **No se validó en Unity real** (el worktree no tiene `Library/`): falta correr los tests en el
  checkout principal antes de dar esto por cerrado.
