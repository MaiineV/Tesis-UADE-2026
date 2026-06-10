# Setup #164 — Meta-progresión entre runs (desbloqueos persistentes)

> Wiring de engine para el sistema de unlocks. El código C# y los assets de
> datos ya están en el repo; este doc cubre lo que se arma en escena vía
> Inspector. Rama: `Feature#0010_MetaProgression`.

## 1. Qué ya está hecho (no repetir)

El menú **Tools ▸ Rollgeon ▸ Setup Meta-Progression (#164)** ya se ejecutó
(es idempotente — re-correrlo no duplica nada). Eso dejó:

- `Assets/Rollgeon/Meta/UnlockCatalog.asset` — catálogo de definiciones.
- `Assets/Rollgeon/Meta/Unlocks/` — árbol base:

  | Asset | Target | Condición | Outcome |
  |---|---|---|---|
  | `Unlock_Dice_D8` | Dice `D8` | Ganar con 5×D6 exactos | Won |
  | `Unlock_Dice_D10` | Dice `D10` | Ganar con [D4,D4,D6,D6,D8] | Won |
  | `Unlock_Class_Berserker` | HeroClass `Berserker` | Ganar con ≥1 D8 en la build | Won |
  | `Unlock_Class_Gambler` | HeroClass `Gambler` | Ganar ejecutando todos los combos del Contrato | Won |

- `ServiceBootstrap.asset`: `UnlockCatalog` agregado a **Catalogs**, y
  `MetaProgressionService` + `UnlockProgressService` agregados a
  **ExtraServices** (ambos Global).

Las condiciones se editan con **Tools ▸ Unlock Condition Tool** (lista de
unlocks, bloques condicionales, AND/OR, outcome, pista).

## 2. Save file

`%USERPROFILE%/AppData/LocalLow/<company>/<product>/meta_progression.json`
(`Application.persistentDataPath`). Borrarlo resetea la meta-progresión a
estado inicial (Guerrero + D3/D4/D6 — todo lo no gateado por una definición
es pool base y está disponible siempre). El unlock se guarda write-through:
apenas se cumple la condición, no al cerrar el juego.

## 3. UnlocksScreen (01_MainMenu)

1. Crear bajo el Canvas un GameObject `UnlocksScreen` (inactivo lo maneja el
   ScreenHost) con el componente **Rollgeon/UI/Screens/Unlocks Screen**.
2. Estructura sugerida: título + `ScrollView` + botón **Volver**.
3. Crear el prefab de fila con **Unlock Entry Row View**: TMP nombre, TMP
   cuerpo, GameObject candado (icono). Bloqueado muestra `???` + pista +
   candado; desbloqueado muestra nombre + descripción.
4. Cablear en `UnlocksScreen`: `_entriesContainer` (Content del scroll),
   `_entryRowPrefab`, `_backButton`.
5. Registrar la screen en el **ScreenHost** de la escena (igual que las demás).
6. En `MainMenuScreen`: agregar botón **Desbloqueos** y arrastrarlo al campo
   `_unlocksButton` (opcional — sin cablear el menú sigue andando).

## 4. Toast de unlock (02_Gameplay y opcionalmente 01_MainMenu)

1. Hijo del Canvas, **siempre activo**: GameObject `UnlockToast` con
   **Unlock Toast View**.
2. Hijo `Panel` (inactivo al inicio) con dos TMP: título y cuerpo. Anclarlo a
   una esquina (sugerido: superior derecha) — notificación no intrusiva.
3. Cablear `_panelRoot`, `_titleLabel`, `_bodyLabel`. Duración default 3 s.

## 5. Sección de unlocks en Victory/Defeat (02_Gameplay)

1. Dentro del panel de `VictoryScreen` y de `DefeatScreen`, agregar un bloque
   `UnlocksSection` (header "Desbloqueado" + TMP lista) con
   **Unlock Results View**.
2. Cablear `_sectionRoot` (el bloque) y `_unlocksLabel` (el TMP).
3. Se auto-puebla al activarse la screen; se oculta si la run no desbloqueó nada.

## 6. Clases desbloqueables en ClassSelectionScreen

`ClassSelectionScreen` ahora tiene la lista **Unlockable Classes (#164)**:
entries `{ Hero, Button, SelectionIndicator, LockIndicator }`.

- Cuando existan los `ClassHeroSO` de Berserker y Gambler (tarea de contenido
  aparte — necesitan ContractSheet de 8 combos, pool de dados, behaviors),
  setear su `EntityId` **exactamente** a `Berserker` / `Gambler` (debe matchear
  el `TargetId` de las definiciones) y mapearlos a los botones (se pueden
  repurposear los botones Mago/Pícaro).
- El botón queda interactable solo si la clase está desbloqueada; el
  `LockIndicator` (candado) se muestra mientras esté bloqueada.

## 7. Checklist de playtest end-to-end (DoD)

- [ ] Primera sesión (sin save): solo Guerrero seleccionable; build solo
      ofrece D3/D4/D6 (D8/D10/D12 no aparecen... D12 no está gateado: si diseño
      lo quiere bloqueado, crear su definición en la tool).
- [ ] Ganar una run con 5×D6 → toast en resultados + D8 aparece en el builder
      y en la pantalla de desbloqueos.
- [ ] Cerrar y reabrir el juego → D8 sigue desbloqueado (save file).
- [ ] Ganar con ≥1 D8 → desbloquea Berserker (visible en pantalla de
      desbloqueos; seleccionable cuando exista el CH asset).
- [ ] Ganar con [D4,D4,D6,D6,D8] → desbloquea D10.
- [ ] Ejecutar los 8 combos del Contrato y ganar → desbloquea Gambler.
- [ ] Usar una poción y ganar → un unlock con "sin pociones" NO se otorga
      (invalidación inmediata).
- [ ] Morir → racha de victorias vuelve a 0; clases jugadas se mantiene.

## 8. Notas para diseño

- **Pool base**: gatear contenido = crearle una `UnlockDefinitionSO` que lo
  apunte (categoría + TargetId). Sin definición, el contenido está siempre
  disponible. Ítems de tienda usan `ItemId`/`EntryId`, encantamientos
  `UpgradeId`, salas `RoomId`, dados el nombre del enum (`D8`).
- **Mid-run**: solo unlocks con outcome **Any** pueden saltar durante la run;
  Won/Lost se evalúan en la pantalla de resultados.
- Los IDs de unlock (`unlock.*`) se persisten en el save: renombrarlos después
  de shippear rompe saves existentes.
