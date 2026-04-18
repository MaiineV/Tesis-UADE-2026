# Content#0103 — Boss Floor Manager ("Gerente de Piso") — Setup

Instructivo manual de post-merge. Todo el codigo ya vino con el worktree;
lo que sigue crea los SOs `.asset` + prefab en Unity (los binarios NO se
commitean — los crea el usuario).

Referencias: `plan.md` (worktree), TECHNICAL.md §7.1 / §13.

## 1. Verificar compile + tests

1. Abrir el proyecto en Unity.
2. Esperar recompile — expectativa 0 errores, 0 warnings nuevos.
3. `Window > General > Test Runner > EditMode`.
4. Correr:
   - `Rollgeon.Entities.Bosses.Tests` (BossFloorManagerSO + Boss*Behavior)
   - `Rollgeon.Combat.ComboBlock.Tests` (ComboBlockService)
   - `Rollgeon.Heroes.Tests` (ContractSheetBlockedComboTests + pre-existentes)
   Todos deben pasar.

Troubleshooting:
- `CS0246` sobre `EnemyDataSO`: T99 no mergeo.
- `CS0246` sobre `IPlayerService`: UI#0098 (o F#0008) no mergeo.
- `CS0246` sobre `IEnergyService`: T102 no mergeo.
- Log `ComboBlockService — IComboBlockService no esta registrado`: falta registrar el
  bootstrap en `ServiceBootstrapSO.ExtraServices` (ver §3).

## 2. Crear el SO del Boss

`Assets > Create > Rollgeon > Entities > Bosses > Floor Manager`

- Renombrar a `Boss_FloorManager.asset` y ubicar en `Assets/Rollgeon/Entities/Bosses/`.
- Llenar los campos:

| Seccion | Campo | Valor |
|---|---|---|
| Identity | EntityId | `boss_floor_manager` |
| Identity | DisplayName | `Gerente de Piso` |
| Identity | Description | `Boss del primer piso. Bloquea combos del contrato y escala dano con energia.` |
| Weakness | WeaknessComboId | `` (vacio — opcional) |
| Weakness | WeaknessMultiplierOverride | `0` (global) |
| Base Stats | BaseHP | `120` |
| Base Stats | BaseAttack | `12` |
| Base Stats | BaseHealStrength | `0` |
| Base Stats | BaseSpeed | `6` |
| Base Stats | MaxEnergy | `0` (el Boss usa su propia barra interna) |
| Boss — Combo Block | ComboBlockIntervalTurns | `3` (spec) |
| Boss — Combo Block | ComboBlockDurationTurns | `2` (spec) |
| Boss — Energy Buildup | BossEnergyMax | `4` |
| Boss — Energy Buildup | BossEnergyGainPerTurn | `1` |
| Boss — Energy Buildup | DoubleDamageChanceDefault | `0.0` |
| Boss — Energy Buildup | DoubleDamageChanceWhenEnergyFull | `0.5` (spec) |

Behaviors (lista `Behaviors`, polimorfica inline):
1. `BossComboBlockBehavior` — `Trigger=OnTurnStart`, `AllowedPhases=Combat`, `BossDataOverride = Boss_FloorManager.asset` (self-reference).
2. `BossEnergyBuildupBehavior` — `Trigger=OnTurnStart`, `AllowedPhases=Combat`, `BossDataOverride = Boss_FloorManager.asset`.
3. `BossAttackBehavior` — `Trigger=OnTurnStart`, `AllowedPhases=Combat`, `BossDataOverride = Boss_FloorManager.asset`, `BaseAttackPower = 12`.

Notas:
- Los `BossDataOverride` apuntan al mismo SO. Esto permite overrides per-instance si
  mas adelante se quisiera reusar los behaviors con otros tunings.
- El `SheetResolver` y `EnergyProbe` los inyecta el spawner runtime (fuera de scope
  del asset — el dev del DungeonManager lo engancha en el PR futuro).

## 3. Bootstrap del `ComboBlockService`

Abrir `ServiceBootstrap.asset` (F#0005) en el inspector:

- Crear un bootstrap MonoBehaviour / ScriptableObject que instancie
  `new ComboBlockService()` y lo agregue a `ServiceBootstrapSO.ExtraServices`.
  Patron identico al `RerollBudgetServiceBootstrap` (ver Feature#0104).

Ejemplo (follow-up, no mandatorio si el user prefiere registrar manualmente):

```csharp
public sealed class ComboBlockServiceBootstrap : ScriptableObject, IPreloadableService
{
    public int Priority => 80;
    public void Register() => new ComboBlockService().Register();
}
```

## 4. Agregar al catalogo de enemies

- Abrir `EnemyCatalog.asset`.
- Arrastrar `Boss_FloorManager.asset` a `_entries`.

## 5. Prefab del Boss (opcional — para combate real)

1. `Create > 3D Object > Cube` (placeholder) o modelo propio.
2. Agregar `EntityBehaviour` (o el MonoBehaviour equivalente que expone `Entity.Guid` al runtime).
3. Settear `PrefabRef` en `Boss_FloorManager.asset` (campo heredado de `BaseEntitySO`).

## 6. Prop de salida (stub)

Este worktree entrega `FloorExitInteractable` (MonoBehaviour stub). Para el FP:

1. Crear prefab `Prop_FloorExit.prefab` con un collider trigger + el script
   `Rollgeon.Dungeon.FloorExitInteractable` pegado.
2. Setear `InteractLabel = "Avanzar al siguiente piso"`.

El spawning del prop lo hace el `BossDeathBehavior` — fuera de scope de este
worktree. En el smoke manual, instanciar el prop a mano y validar que su
metodo `Interact()` loggea.

## 7. Smoke manual en Play

No hay scene de smoke en este worktree (la orquestacion con TurnManager real
vino con T100d). Para validar manualmente:

1. Crear scene temp con un `MonoBehaviour` que:
   - Inicialice `ServiceLocator` con `AttributesManager` + `IPlayerService` (fake)
     + `new ComboBlockService().Register()`.
   - Cree player con `ContractSheet` + 3 combos + `Health` en 100.
   - Cree boss con `Health` en 120 + instancia de los 3 behaviors.
   - Llame `BossComboBlockBehavior.Execute(ctx)` 3 veces — al 3ro debe bloquear un combo
     y disparar `EventName.OnComboBlocked`.
   - Llame `EventManager.Trigger(OnTurnFinished, playerGuid)` 2 veces — el bloqueo debe
     expirar y disparar `OnComboUnblocked`.
   - Llame `BossAttackBehavior.Execute(ctx)` con `TargetGuid=playerGuid`, rng controlado
     → validar HP del player baja.
2. Log esperado:
   - Turno 3 del Boss → `OnComboBlocked(<combo>, 2)`.
   - Tras 2 `OnTurnFinished(player)` → `OnComboUnblocked(<combo>)`.
   - Turno 4 del Boss → energia llena → `BossAttackBehavior` aplica x2 damage con rng 0.1.

## 8. Regla no-negociable

- El dev NO commitea `.asset` / `.prefab` / `.unity` — solo `.cs` y este markdown.
- El usuario crea los assets en Unity tras el merge, siguiendo esta guia.
- No hacer `git push` sin aprobacion del owner.
