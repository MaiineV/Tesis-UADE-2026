# Una sala por jefe

> Estado al 2026-08-14. Las seis salas se generan por código desde el editor.
> Lo que queda a mano: cablear cada sala a su jefe (ver §5 — hoy **no hay** campo
> donde vive ese vínculo).

Hasta ahora los tres jefes de un piso compartían prefab de sala. Si el terreno es
una palanca de diseño, no puede ser el mismo para tres peleas distintas: el
NavGraph se hornea en el editor y queda serializado en `RoomLayout.NavGraph`, así
que "misma sala" significa literalmente "mismo grafo, mismos caminos, mismo
presupuesto de pasos". Por eso son seis prefabs y no un prefab con variantes.

## 1. Generar las salas

`Rollgeon → Bosses → Build Boss Rooms`.

Por cada jefe el tool:

1. Abre la sala base del piso (`LoadPrefabContents`) — arranca siempre de cero,
   nunca de la sala derivada anterior.
2. Cuelga un grupo `BossRoomBlockers` con un prop por casilla bloqueada del plano.
   Cada prop lleva un `TileMarker` con `IsBlocker = true`, `Footprint 1×1×1`,
   `Layer 0` — el mismo autorado que produce el Room Editor al pintar.
3. Mueve `EnemySpawnPoints[0]` a la casilla del jefe (es de ahí que el resolver de
   combate saca su celda: `WorldToGrid(EnemySpawnPoints[0].position)`).
4. Hornea el NavGraph con `NavGraphBaker`.
5. Valida las tres reglas de §4 y guarda con `SaveAsPrefabAsset` sobre el mismo
   path.

**Re-correrlo es seguro.** Cada corrida reconstruye la sala entera desde la base y
la reescribe sobre su path, que **preserva el GUID** — no duplica props y no rompe
referencias. Corolario importante: ver §6.

## 2. Qué sala sale por jefe

| Jefe     | Piso | Sala base                                 | Sala generada                                             |
|----------|------|-------------------------------------------|-----------------------------------------------------------|
| Croupier | 1    | `FloorOne/Boss_Room01.prefab`             | `FloorOne/Boss_Room_Croupier.prefab`                      |
| Bandida  | 1    | `FloorOne/Boss_Room01.prefab`             | `FloorOne/Boss_Room_Bandida.prefab`                       |
| Cajero   | 2    | `FloorTwo/Boss_Room_FloorTwo01.prefab`    | `FloorTwo/Boss_Room_Cajero.prefab`                        |
| Anotador | 2    | `FloorTwo/Boss_Room_FloorTwo01.prefab`    | `FloorTwo/Boss_Room_Anotador.prefab`                      |
| Generala | 3    | `FloorThree/Boss_Room_FloorThree.prefab`  | `FloorThree/Boss_Room_Generala.prefab`                    |
| Tahúr    | 3    | `FloorThree/Boss_Room_FloorThree.prefab`  | `FloorThree/Boss_Room_Tahur.prefab`                       |

Todas bajo `Assets/Prefabs/Rooms/`. Quedan en la carpeta del piso a propósito:
`Rollgeon → Tools → Rebake Room NavGraphs` barre esas tres carpetas, así que las
salas de jefe se rebakean con las demás cuando cambia la lógica del baker.

## 3. Los planos

El documento dibuja los planos sobre una grilla de **11 × 7 con `y = 0` arriba**.
La sala real es **11 × 11 centrada en (0,0)**. El builder centra el plano:

```
celdaSala.X = celdaPlano.x - 5
celdaSala.Y = 3 - celdaPlano.y
```

⇒ el centro del plano `(5,3)` es la `(0,0)` de la sala, y las filas `y = ±4, ±5`
de la sala quedan fuera del plano y llegan como estén en la base.

`B` = jefe · `#` = blocker · `·` = piso.

**Croupier** — dos columnas en las costuras del paño. Cruzar de tramo obliga a
pisar sector.

```
· · · · · · · · · · ·
· · · · · · · · · · ·
· · · · · · · · · · ·
· · · # · B · · # · ·
· · · · · · · · · · ·
· · · · · · · · · · ·
· · · · · · · · · · ·
```
Blockers en sala: `(-2,0)` `(3,0)` · jefe en `(0,0)`.

**Bandida** — dos bancos de tragamonedas que abren tres calles verticales. Ella va
contra la pared izquierda: está atornillada, no camina.

```
· · · · · · · · · · ·
· · · · # # · · # # ·
· · · · · · · · · · ·
B · · · · · · · · · ·
· · · · · · · · · · ·
· · · · # # · · # # ·
· · · · · · · · · · ·
```
Blockers en sala: `(-1,2)` `(0,2)` `(3,2)` `(4,2)` `(-1,-2)` `(0,-2)` `(3,-2)`
`(4,-2)` · jefe en `(-5,0)`.

**Cajero** — el mostrador, con las dos aberturas en `x = 2` y `x = 8`. Él vive del
lado de arriba: entrar por una puerta te compromete con ese lado.

```
· · · · · · · · · · ·
· · · · · B · · · · ·
· · · · · · · · · · ·
# # · # # # # # · # #
· · · · · · · · · · ·
· · · · · · · · · · ·
· · · · · · · · · · ·
```
Blockers en sala: `(-5,0)` `(-4,0)` `(-2,0)` `(-1,0)` `(0,0)` `(1,0)` `(2,0)`
`(4,0)` `(5,0)`; aberturas en `(-3,0)` y `(3,0)` · jefe en `(0,2)`.

**Anotador** — cuatro escritorios de 2×1 y un corredor central de pared a pared.
El corredor es el camino corto, y lo que su estela de hielo va a tapar.

```
· · · · · · · · · · ·
· # # · · · · · # # ·
· · · · · · · · · · ·
· · · · · B · · · · ·
· # # · · · · · # # ·
· · · · · · · · · · ·
· · · · · · · · · · ·
```
Blockers en sala: `(-4,2)` `(-3,2)` `(3,2)` `(4,2)` `(-4,-1)` `(-3,-1)` `(3,-1)`
`(4,-1)` · jefe en `(0,0)`.

**Generala** — sin obstáculos fijos. Sus cinco dados son el terreno, y son
móviles: un obstáculo fijo competiría por la misma lectura. Igual tiene prefab
propio: que su sala sea la única limpia es una decisión de diseño, y el grafo
horneado tiene que decirlo.

**Tahúr** — cuatro columnas que encarecen el eje vertical, justo donde el Castigo
y La Mesa pierden intersección. Peleálo de costado.

```
· · · · · · · · · · ·
· · · # · · · # · · ·
· · · · · · · · · · ·
· · · · · B · · · · ·
· · · · · · · · · · ·
· · · # · · · # · · ·
· · · · · · · · · · ·
```
Blockers en sala: `(-2,2)` `(2,2)` `(-2,-2)` `(2,-2)` · jefe en `(0,0)`.

## 4. Las tres reglas que el builder valida

Se chequean contra el **grafo horneado**, no contra el plano — el plano está
dibujado sobre una grilla limpia y la sala real tiene muebles propios. Cualquier
violación sale como `LogError` con el nombre del jefe y la celda:

| Regla | Qué exige | Por qué |
|-------|-----------|---------|
| **a** | El jefe queda con ≥ 2 casillas adyacentes caminables | El jugador pega a distancia 1. Encerrarlo es prohibir la pelea. |
| **b** | Ninguna casilla de piso aislada del resto | Una isla es mapa que el jugador ve y no puede usar. |
| **c** | La casilla de spawn del jugador queda caminable | Entrar a la sala y aparecer dentro de un mueble. |

Más dos chequeos que no son reglas de diseño sino de que el tool hizo su trabajo:

- **Cada blocker del plano quedó realmente no-caminable.** Si un prop no llega a
  la banda de walk clearance (0.5 sobre el piso) el `NavGraphBaker` no le mata el
  nodo y la casilla sigue siendo camino. El finding dice qué celda y qué revisar.
- **Puertas.** Corre el mismo `RoomDoorBakeValidator` que el rebaker, pero
  descontando los findings que la sala base ya traía: así, si aparece uno, es de
  un blocker del plano y no ruido heredado.

Al 2026-08-14 los seis planos pasan las tres reglas contra los grafos reales de
las salas base.

## 5. Cablear sala ↔ jefe (PENDIENTE — no hay dónde)

Los prefabs se generan, pero **hoy no hay campo que los ate a su jefe**, y el tool
no los cablea a nada. El motivo es estructural, no de alcance:

- La sala se elige en la **generación del piso**: `FloorLayoutSO.Slots[].Pool` es
  una lista de `RoomSO`, y `DungeonManager.InstantiateRoomPrefab` instancia
  `RoomSO.RoomPrefab`.
- El jefe se elige **al entrar a la sala**: `DefaultEnemySpawnResolver` rolea
  `FloorLayoutSO.BossPool` con un RNG sembrado por `roomInstanceId`.

Son dos tiradas independientes y en momentos distintos. Crear seis `RoomSO` y
tirarlos al pool del piso **empeoraría** las cosas: saldría una sala al azar
peleando contra un jefe al azar.

El lugar natural del vínculo es `WeightedBoss` — la entry del pool ya es donde
vive lo que ese jefe tiene *en ese piso* (peso, enabled). Parche mínimo:

1. `WeightedBoss` gana un campo `RoomSO Room` (o `GameObject RoomPrefab`).
2. `BossPoolAssetInstaller.Entry(...)` lo carga junto al `EnemyDataSO`.
3. La tirada del jefe se adelanta a la generación del piso (o se cachea en el
   `RoomInstance` con el mismo seed) y `InstantiateRoomPrefab` usa la sala de la
   entry rolada cuando `RoomSO.Type == Boss`.

El punto 3 es el que tiene filo: hoy el seed del roll de jefe sale del
`roomInstanceId`, así que hay que rolar **una sola vez** y guardar el resultado —
si se rolea dos veces con RNGs distintos, la sala y el jefe se desincronizan.

Mientras tanto, para probar una sala a mano: apuntá el `RoomPrefab` del `RoomSO`
del piso (`Room_Boss01` → piso 1, `Room_Boss02` → piso 2, `Room_Boss03` → piso 3,
en `Assets/Rollgeon/Rooms/`) al prefab que quieras, y forzá el jefe con
`boss <entityId>` de la dev console — `boss list` muestra los ids del pool del
piso.

## 6. Limitaciones conocidas

- **Lo que edites a mano en una sala derivada se pierde en el próximo rebuild.**
  El tool reconstruye desde la base cada vez; ese es el precio de que sea
  idempotente de verdad. La decoración compartida (los `DecalProjector` de
  `docs/setup/boss-room-decals.md`) va en la sala **base** del piso, que sí
  propaga a las tres. Lo que sea propio de un jefe va en el builder.

- **La sala base ya trae muebles que el plano no dibujó.** El plano se dibujó
  sobre una grilla limpia; la sala real tiene la mesa de pool en `x = 3..4,
  y = 1..3` (pisos 1 y 2), un barril en `(-5,-3)` y, en piso 3, cuatro barriles
  más. El builder los respeta y avisa por consola cuántas celdas del plano se
  comen:

  | Sala     | Celdas del plano ya bloqueadas por la base |
  |----------|--------------------------------------------|
  | Croupier | 7 — `(-5,-3)` + el bloque `(3..4, 1..3)`   |
  | Bandida  | 5 (+2 blockers del plano omitidos, ver abajo) |
  | Cajero   | 7                                          |
  | Anotador | 5 (+2 omitidos)                            |
  | Generala | 11 — suma `(-4,2)` `(-3,3)` `(-2,-3)` `(3,-3)` |
  | Tahúr    | 11                                         |

  Sacar esos muebles es una decisión de diseño (cambia el mapa útil de la pelea),
  así que el builder **no los toca**: sólo los reporta.

- **Blockers del plano que caen sobre un mueble base se omiten**, con warning.
  Pasa con `(3,2)` y `(4,2)` en Bandida y Anotador: la mesa de pool ya bloquea
  ahí y apilar un segundo prop encima sólo duplica geometría.

- **Los props son placeholders.** El arte que pide el documento (mármol bajo,
  mostrador con reja de latón, escritorios con papeles y lámpara) no está
  modelado. Se usó lo que existe y ya funciona como blocker en estas salas:

  | Sala               | Prop hoy                          | Lo que pide el doc      |
  |--------------------|-----------------------------------|-------------------------|
  | Croupier, Tahúr    | `Props/barrilv01.prefab`          | Columna de mármol baja  |
  | Bandida            | `Props/slotv02.prefab`            | Tragamonedas ✓ exacto   |
  | Cajero             | `Props/Tablev02.prefab`           | Mostrador con reja      |
  | Anotador           | `Props/Tablev02.prefab`           | Escritorio con lámpara  |

  Para cambiarlos: editá `PropPrefabPath` del plano en `BossRoomBuilder.Plans` y
  re-corré el menú. `PropEuler` y `PropScale` son palancas de encuadre visual: **en
  XZ** el bloqueo sale del `Footprint` autorado, no del renderer, así que rotar o
  escalar no cambia qué casilla cae. En **Y** sí manda el renderer — un prop
  achatado a casi nada deja de llegar a la banda de walk clearance y su casilla
  vuelve a ser caminable. Eso lo caza el chequeo de §4 en la misma corrida.

- **Retunear un plano** es mover celdas en `BlockerPlanCells` (coordenadas del
  documento, 11 × 7) y re-correr. Si el cambio rompe una de las tres reglas, el
  menú lo grita en consola en la misma corrida.

- Si después de un rebuild algún `RoomSO` aparece con `RoomPrefab` en `None`,
  re-asignalo: `SaveAsPrefabAsset` conserva el GUID del asset, pero la referencia
  apunta a `guid + fileID` del root y ese segundo dato lo maneja Unity.
