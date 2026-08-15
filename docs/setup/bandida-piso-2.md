# La Bandida se muda al piso 2

> Estado al 2026-08-14. El cambio ya está en el código de los builders. Lo que falta
> es **re-correr dos menús** para que los assets lo reflejen — hasta entonces
> `BossRoomWiringTests` queda en rojo a propósito: el test afirma el diseño nuevo y
> los `.asset` todavía tienen el viejo.

## Por qué

`docs/design/bosses-seis-refinados.html` la titula **"Piso 2 · cruzar"**, junto al
Cajero y al Anotador. El piso 1 queda con el Croupier solo.

La razón está en la ficha: con cuatro blancos (tres rodillos + la máquina), un turno
para actuar y la jugada correcta siendo *no matar* (dañar cualquier rodillo cancela
la cuenta), la Bandida cruza dos palancas a la vez en vez de enseñar una. Y su
jackpot pega 25 en 7×7 — el 60% de la vida del jugador de piso 1. El piso 1 enseña
de a una palanca; el 2 es donde se cruzan.

## Qué cambió en código

| Archivo | Antes | Ahora |
|---|---|---|
| `Assets/Scripts/Editor/Tools/Rooms/BossRoomBuilder.cs` | `Floor = 1`, base `FloorOne/Boss_Room01.prefab`, salida `FloorOne/Boss_Room_Bandida.prefab` | `Floor = 2`, base `FloorTwo/Boss_Room_FloorTwo01.prefab`, salida `FloorTwo/Boss_Room_Bandida.prefab` |
| `Assets/Scripts/Editor/Tools/Enemy/Builders/BossPoolAssetInstaller.cs` | `BP_Floor1` = Sunken Grand + Croupier + Bandida | `BP_Floor1` = Sunken Grand + Croupier · `BP_Floor2` = **Bandida** + Cajero + Anotador (+ Security Boss desactivado) |
| `Assets/Scripts/Rollgeon/Dungeon/Tests/BossRoomWiringTests.cs` | esperaba `boss.one_armed` activo en el piso 1 | lo espera en el piso 2 |

Los blockers del plano y la celda del jefe **no cambian**: sigue contra la pared
izquierda con sus dos bancos de tragamonedas. Lo único que cambia es de qué sala base
se clona y dónde se escribe el resultado.

## Pasos

1. **Regenerar las salas** — `Rollgeon → Bosses → Build Boss Rooms`.

   Escribe `Assets/Prefabs/Rooms/FloorTwo/Boss_Room_Bandida.prefab` (nuevo asset,
   GUID nuevo). Mirar la consola: los blockers que caigan sobre un mueble de la sala
   base del piso 2 se omiten con warning, y la cuenta de "celdas del plano ya
   bloqueadas por la base" **va a dar distinta** que en el piso 1 — la sala base es
   otra, con otros muebles. Si alguna de las tres reglas de autoría falla
   (jefe alcanzable, sala conexa, spawn del jugador libre) sale como error y hay que
   ajustar el plano, no ignorarlo.

2. **Regenerar los pools** — `Tools → Rollgeon → Bosses → Build Floor Pools`.

   Reescribe `BP_Floor1` y `BP_Floor2` y los reasigna a sus `FloorLayoutSO`.

   > **No editar los `.asset` de pool a mano.** `BossPoolSO` es
   > `SerializedScriptableObject`: guarda las entries en los `SerializationNodes` de
   > Odin, no en el bloque Unity. Tocar el YAML corrompe el asset en silencio.

3. **Verificar** — correr `BossRoomWiringTests`. Los tres tests tienen que pasar:
   - piso 1 activo = `boss.sunken_grand` + `boss.croupier`
   - piso 2 activo = `boss.one_armed` + `boss.cashier` + `boss.scorekeeper`
   - ningún boss activo repetido entre pisos

   Si el editor estuvo sin foco mientras se tocaban los assets, hacer `Reimport` de
   `Assets/Rollgeon/Floor/` antes de dar el test por rojo: los SO deserializados
   viejos sobreviven a un refresh.

4. **Probarla en juego** (opcional): apuntar el `RoomPrefab` de `Room_Boss02`
   (`Assets/Rollgeon/Rooms/`) a `FloorTwo/Boss_Room_Bandida.prefab`, entrar al piso 2
   y forzar el jefe con `boss boss.one_armed` en la dev console. Ver
   `docs/setup/boss-rooms.md §5` — el vínculo sala↔jefe sigue sin existir como campo.

## Rastros que quedan desactualizados

- `docs/setup/boss-rooms.md` — la tabla de §2 sigue diciendo `Bandida | 1 |
  FloorOne/...`, y las cuentas de §6 ("5 celdas ya bloqueadas", "`(3,2)` y `(4,2)`
  omitidos") están medidas contra la sala base del **piso 1**. Hay que rehacerlas con
  la salida del paso 1.
- Si en algún momento se llegó a generar `FloorOne/Boss_Room_Bandida.prefab`, queda
  huérfano: ningún plano lo escribe ya. Borrarlo a mano después de confirmar que
  ningún `RoomSO` lo referencia.
