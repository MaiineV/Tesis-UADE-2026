# Content#0099 — Support Enemy (Auditor) — Setup

Instructivo manual de post-merge. Todo el codigo ya vino con el worktree;
lo que sigue crea los SOs `.asset` en Unity (los `.asset` NO se commitean —
los crea el usuario).

## 1. Verificar compile + tests

1. Abrir el proyecto en Unity.
2. Esperar recompile — expectativa 0 errores, 0 warnings nuevos.
3. `Window > General > Test Runner > EditMode`.
4. Correr el grupo `Rollgeon.Entities.Tests` y `Rollgeon.Entities.Behaviors.Tests`
   — todos deben pasar.

Troubleshooting:
- `CS0246` sobre `BaseCatalogSO`: F#0005 (Catalogs + Bootstrap) no mergeo.
- `CS0246` sobre `Energy` / `Speed`: F#0003 (Attributes) no mergeo.
- `CS0246` sobre `BaseBehavior`: el stub F#0004 fue removido antes de tiempo.

## 2. Crear los SO assets

### 2.1 BehaviorLibrary (opcional — solo si se decide modo FromLibrary)

`Assets > Create > Rollgeon > Behavior Library`
- Ubicar en `Assets/Rollgeon/Catalogs/BehaviorLibrary.asset`.
- Opcion A: dejar el diccionario vacio (usar Inline en cada enemy).
- Opcion B: agregar entry `support.heal` → `SupportHealBehavior`
  con `BaseHealAmount = 6`, `Trigger = OnTurnStart`, `AllowedPhases = Combat`.

### 2.2 EnemyCatalog

`Assets > Create > Rollgeon > Catalogs > Enemy Catalog`
- Ubicar en `Assets/Rollgeon/Catalogs/EnemyCatalog.asset`.
- El campo `_entries` lo poblamos con el Auditor despues de crearlo.

### 2.3 EnemyData Auditor

`Assets > Create > Rollgeon > Entities > Enemy Data`
- Renombrar a `Auditor.asset` en `Assets/Rollgeon/Entities/Enemies/`.
- Llenar los campos:

| Seccion | Campo | Valor |
|---|---|---|
| Identity | EntityId | `enemy.support.auditor` |
| Identity | DisplayName | `Auditor de Mesa` |
| Identity | Description | `Esqueleto en traje que cura a sus aliados. No ataca directamente.` |
| Weakness | WeaknessComboId | `combo.par` (o el que decida balance) |
| Weakness | WeaknessMultiplierOverride | `0` (usar global) o `1.5` |
| Base Stats | BaseHP | `20` |
| Base Stats | BaseAttack | `0` |
| Base Stats | BaseHealStrength | `5` |
| Base Stats | BaseSpeed | `4` |
| Base Stats | MaxEnergy | `3` |
| Behaviors | Behaviors[0] | `SupportHealBehavior` con `Trigger=OnTurnStart`, `AllowedPhases=Combat`, `BaseHealAmount=6` |

Notas:
- `HP=20 / Attack=0 / HealStrength=5 / Speed=4 / MaxEnergy=3` son los defaults
  propuestos. El usuario confirma en play y ajusta via Balance#0101.
- Si se quiere usar la BehaviorLibrary: `Behaviors` queda vacio y en vez de
  inline se referencia el template por id. Por ahora el flujo recomendado es
  inline (mas simple para debug).

### 2.4 Agregar el Auditor al catalogo

- Abrir `EnemyCatalog.asset` → arrastrar `Auditor.asset` a `_entries`.

## 3. Registrar el catalogo en el Bootstrap

Abrir `ServiceBootstrap.asset` (creado por F#0005) en el inspector y:

- Agregar `EnemyCatalog.asset` a la lista `Catalogs`.
- (Opcional) Agregar `BehaviorLibrary.asset` a `Catalogs` o `SettingsAssets`
  si se decide usar modo FromLibrary — el `ServiceBootstrapSO.RegisterAll`
  lo registra por Type runtime.

## 4. Smoke manual en Play

No hay scene de smoke todavia (lo trae T100d con el TurnManager real). Para
validar manualmente:

1. Crear scene temp con un `MonoBehaviour` que:
   - Instancie `AttributesManager` y lo registre via `ServiceLocator.AddService`.
   - Instancie un fake `IEntityQueryService` (ver `FakeEntityQueryService` de los tests).
   - Cree `owner` + 2 "allies" con `Health` en 10/20, 5/20.
   - Instancie `SupportHealBehavior` con `BaseHealAmount = 6`, `MaxHpResolver = _ => 20`.
   - Construya un `BehaviorContext` con `SourceEntity.Guid = owner`.
   - Llame `behavior.Execute(ctx)` → logea `FloatingHeal` generado.
2. Expectativa: el ally con HP mas bajo termina con HP + (6 + 5) = HP + 11.

## 5. Regla no-negociable

- El dev NO commitea `.asset` / `.prefab` / `.unity` — solo `.cs` y este markdown.
- El usuario crea los assets en Unity tras el merge, siguiendo esta guia.
- No hacer `git push` sin aprobacion del owner.
