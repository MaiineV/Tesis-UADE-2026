# Setup — System#0100b: Action Economy + Repetition Constraint

Engine-side setup instructions post-merge para poner en marcha el
`ActionCatalogSO` + `TurnManager` del sprint #100. Todo el codigo C# ya vive
en `Assets/Scripts/Rollgeon/Combat/Actions/`; este documento cubre los pasos
manuales de Unity (crear `.asset`, wiring del `ServiceBootstrap`).

---

## 1. Crear `ActionCatalog.asset`

**Donde**: `Assets/ResourcesData/Catalogs/ActionCatalog.asset`.

**Como**: en Project view, boton derecho > `Create > Rollgeon > Actions > Action Catalog`.

> La carpeta `ResourcesData/Catalogs/` ya existe por Foundation#0005. Si por
> algun motivo no esta, crearla con el mismo path.

Dejar la lista `_entries` vacia por ahora — la poblamos en el paso 3.

---

## 2. Crear los 6 `ActionDefinitionSO` del FP

**Donde**: `Assets/ResourcesData/Actions/`.

**Como**: boton derecho > `Create > Rollgeon > Actions > Action Definition`.

Crear uno por fila:

| File                          | ActionId          | Type       | DisplayName    | EnergyCost | BlockOnRepeat | AllowsEnergyReroll | BackingAsset |
|-------------------------------|-------------------|------------|----------------|------------|---------------|--------------------|--------------|
| `Action_Move.asset`           | `move`            | Move       | Move           | 1          | **false**     | true               | null         |
| `Action_AttackBasic.asset`    | `attack.basic`    | Attack     | Attack         | 1          | true          | true               | null         |
| `Action_AttackSpecial.asset`  | `attack.special`  | Attack     | Special Attack | 2          | true          | true               | null         |
| `Action_Heal.asset`           | `skill.heal`      | SkillCheck | Heal           | 2          | true          | true               | null         |
| `Action_ForceDoor.asset`      | `skill.force_door`| SkillCheck | Force Door     | 1          | true          | true               | null         |
| `Action_EndTurn.asset`        | `defend`          | Defend     | End Turn       | 0          | true          | true               | null         |

Notas:

- **`move`** declara `BlockOnRepeat = false` — es la unica manera de escapar
  segun el GDD (§12.6 "Consecuencias del diseño"). Si tiene la energia, el
  jugador puede moverse varias veces en un turno.
- **`defend`** (el "End Turn" del sprint) mapea al `ActionType.Defend` del
  §12.6.0; no existe un valor `EndTurn` en el enum por diseño. Ver plan §10 R6.
- El campo `Effect` se deja con su default (`new EffectData()` con listas
  vacias). El dispatch real de combos vive en `BaseComboSO` (T97a) + T97b;
  el del movimiento / skill checks llega con sus propios worktrees.
  `TurnManager` trata los effects vacios como "permit no-op" (cobra energia +
  marca usada, delega al dispatcher externo si hay `BackingAsset`).
- `BackingAsset` se deja null en todos por ahora. Cuando T97b mergee su
  `ComboDispatcher`, las entradas `Type = Combo` del catalogo podran apuntar
  al `BaseComboSO` correspondiente — pero eso es un followup.

---

## 3. Popular `ActionCatalog.asset` con los entries

1. Abrir `Assets/ResourcesData/Catalogs/ActionCatalog.asset` en el inspector.
2. En la seccion **Entries**, drag-and-drop los 6 `.asset` creados en el paso
   2 a la lista `_entries`.
3. Odin valida en vivo — si hay `ActionId` duplicado o un entry null, veras
   el error en el inspector. Resolver antes de seguir.

---

## 4. Registrar el catalogo en `ServiceBootstrap`

1. Abrir `Assets/ResourcesData/Bootstrap/ServiceBootstrap.asset` (o donde
   este el `ServiceBootstrapSO` del proyecto — Foundation#0005 lo creo).
2. En la seccion **Catalogs**, agregar un slot y arrastrar
   `ActionCatalog.asset`.
3. El `RegisterAll()` del bootstrap lo publicara en `ServiceLocator` bajo
   `typeof(ActionCatalogSO)`.

---

## 5. Crear `TurnManagerBootstrap.asset`

**Donde**: `Assets/ResourcesData/Services/TurnManagerBootstrap.asset`.

**Como**: `Create > Rollgeon > Combat > Turn Manager Bootstrap`.

No tiene campos configurables — es un thin wrapper que, en su `Register()`,
instancia `new TurnManager()` y delega.

---

## 6. Registrar `TurnManagerBootstrap` en `ServiceBootstrap.ExtraServices`

1. Abrir `ServiceBootstrap.asset`.
2. En la seccion **Extra Runtime Services**, agregar un slot.
3. Drag `TurnManagerBootstrap.asset` al slot.
4. Verificar el orden por `Priority` — el sort es ascendente:
   - `EnergyServiceBootstrap` — Priority **50**
   - `TurnOrderServiceBootstrap` — Priority **100**
   - `TurnManagerBootstrap` — Priority **60**
5. El `Priority=60` garantiza que `TurnManager.Register()` corre **despues**
   de `EnergyServiceBootstrap` (Priority=50) — que es la precondicion para
   que `ServiceLocator.TryGetService<IEnergyService>` tenga un service
   registrado. Ver plan §10 R9.

---

## 7. Smoke test del bootstrap

1. Abrir la escena `00_Bootstrap` (Foundation#0005).
2. Entrar a Play Mode.
3. En la consola, verificar:
   - `RegisterAll() invoked`
   - `Registered N catalogs, M settings, K extra services` — los numeros
     deben reflejar **+1 catalog** y **+1 extra service** vs el estado
     pre-merge (por el ActionCatalog y el TurnManagerBootstrap).
4. Si aparece `[TurnManager] IEnergyService no esta registrado en
   ServiceLocator...`, significa que el orden de Priority esta roto —
   verificar el paso 6.

---

## 8. Quick sanity check en codigo (opcional)

Si queres validar desde un script de debug sin gameplay real:

```csharp
using Patterns;
using Rollgeon.Combat.Actions;

// En algun Start():
if (ServiceLocator.TryGetService<ActionCatalogSO>(out var cat))
{
    Debug.Log($"ActionCatalog tiene {System.Linq.Enumerable.Count(cat.AllIds)} acciones.");
    foreach (var id in cat.AllIds) Debug.Log($" - {id}");
}

if (ServiceLocator.TryGetService<TurnManager>(out var tm))
{
    Debug.Log("TurnManager registrado OK.");
}
```

---

## Followups pendientes

- **T97b (`ComboDispatcher`)**: cuando mergee, actualizar los entries
  `Type = Combo` del catalog apuntando `BackingAsset -> BaseComboSO` para
  habilitar el lookup `catalog.GetBackingAsset<BaseComboSO>(id)`.
- **Balance#0101 (`RulesetSO.ForbiddenActionIds`)**: hoy el hook
  `TurnManager.IsForbiddenByRuleset` es stub y devuelve false. Cuando
  Balance#0101 agregue el campo al `RulesetSO`, cambiar el return del stub
  sin tocar la firma de `CanExecute`.
- **T104 (Re-roll budget)**: el flag `AllowsEnergyReroll` de
  `ActionDefinitionSO` ya existe pero nadie lo consume. T104 lo leera para
  decidir si una accion permite re-roll de dados por energia.
- **Tool#0100T (ActionCatalog Editor)**: designer-facing tool con validacion
  de naming, auto-populate de wrappers para combos, y orphan detection
  cross-catalog.
