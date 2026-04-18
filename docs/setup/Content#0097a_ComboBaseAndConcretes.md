# Setup — Content#0097a Combo base + concretes

Esta tarea entrega `BaseComboSO`, 8 combos concretos y el `ComboCatalogSO`. El codigo ya
compila tras merge; el usuario debe crear los `.asset` en Engine y registrar el catalogo en
el `ServiceBootstrapSO`. Sigue los pasos en orden.

## 1. Estructura de carpetas

1. Abrir Unity. Esperar la recompilacion (0 errores esperados).
2. Crear la carpeta `Assets/Rollgeon/Combos/` si no existe.
3. Crear la subcarpeta `Assets/Rollgeon/Combos/Instances/`.

## 2. Crear las 8 instancias de combo

En `Assets/Rollgeon/Combos/Instances/`, usar el menu **Create -> Rollgeon -> Combos -> <Nombre>** para cada uno:

| Asset | Menu | ComboId a escribir | BaseDamage (referencia Sprint #97) |
|---|---|---|---|
| `Combo_Par.asset` | Rollgeon/Combos/Par | `combo.par` | 10 |
| `Combo_DoblePar.asset` | Rollgeon/Combos/Doble Par | `combo.double_pair` | 18 |
| `Combo_Sum4.asset` | Rollgeon/Combos/Suma X | `combo.sum_x` | 25 (X=4) |
| `Combo_Trio.asset` | Rollgeon/Combos/Trio | `combo.triple` | 28 |
| `Combo_Escalera.asset` | Rollgeon/Combos/Escalera | `combo.straight` | 35 |
| `Combo_FullHouse.asset` | Rollgeon/Combos/Full House | `combo.full_house` | 40 |
| `Combo_Poker.asset` | Rollgeon/Combos/Poker | `combo.poker` | 60 |
| `Combo_Generala.asset` | Rollgeon/Combos/Generala | `combo.generala` | 100 |

### Para cada `.asset`

- `ComboId` -> el ID canonico de la tabla. **Primera vez**: el dropdown esta vacio porque el
  catalogo aun no esta registrado. Tipear el ID a mano (Odin permite escribir en el dropdown).
  Despues del paso 4 el dropdown se puebla solo para futuras ediciones.
- `DisplayName` -> nombre legible ("Par", "Doble Par", "Suma 4", "Trio", "Escalera",
  "Full House", "Poker", "Generala").
- `Description` -> opcional, util para UI de seleccion de clase (T98).
- `Icon` -> dejar vacio por ahora (art pipeline separado).
- `BaseDamage` -> el valor de la tabla (o el que decida balance).
- `ValueMultipliers` -> dejar los 6 en 0 (combo plano).
- `GeneralMultiplier` -> 1.
- `ExtraEffects` -> vacio.

### Extra para `Combo_Sum4.asset`

- `X` -> 4.
- `BaseDamageConfigurable` -> 25 (piso plano del GD).

> Nota: Para "Suma-5" / "Suma-6" futuras, clonar `Combo_Sum4.asset` y cambiar `X` + `ComboId`.

## 3. Crear el catalogo

1. En `Assets/Rollgeon/Combos/`, **Create -> Rollgeon -> Catalogs -> Combo Catalog** ->
   `ComboCatalog.asset`.
2. Arrastrar los 8 `.asset` al campo `Entries` del catalogo, en el orden sugerido
   (Par -> DoblePar -> Sum4 -> Trio -> Escalera -> FullHouse -> Poker -> Generala).
3. Verificar que:
   - El InfoBox "Los IDs aqui listados..." no muestra error.
   - El validador `ValidateNoDuplicateIds` no dispara.
   - El validador `ValidateNoNullEntries` no dispara.

## 4. Registrar el catalogo en el ServiceBootstrap

1. Abrir `Assets/Rollgeon/ServiceBootstrap.asset` (creado por Foundation#0005).
2. En el campo `Catalogs` (lista polimorfica `List<BaseCatalogSO>`), agregar
   `ComboCatalog.asset`.
3. Guardar el asset.

Con esto, al iniciar la scene bootstrap, el `ComboCatalogSO` queda registrado en el
`ServiceLocator` y el `[ValueDropdown]` de `BaseComboSO._comboId` se alimenta correctamente.

## 5. Validacion final

- Reabrir cualquier `Combo_*.asset` y confirmar que el dropdown del `ComboId` ahora muestra
  los 8 IDs.
- Correr los edit-mode tests:
  **Window -> General -> Test Runner -> EditMode -> Run All** -> 100% PASS en
  `Rollgeon.Combos.Tests`.
- No se crean scenes, prefabs ni inputactions en esta tarea.

## 6. Out-of-scope (tareas futuras)

- `ContractWarriorSO` + flag de debilidad de enemigo -> T97b.
- Contadores tipo Balatro / combos "strike" -> T97c.
- Tool del contrato con probability calc + UI -> Tool#0097T.
- Pipeline de combate que consume el combo (`AttackResolver`) -> T100b.
