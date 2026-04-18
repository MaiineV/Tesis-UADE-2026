# Setup — Content#0097b Warrior Contract + Weakness Flag

Esta tarea entrega `ContractSheet`, el skeleton de `ClassHeroSO`, el stub de `EnemyDataSO`,
`WeaknessConfig` en `RulesetSO`, y el servicio `IWeaknessChecker` + `IWeaknessRegistry`.
El codigo compila tras merge; el usuario debe crear los `.asset` en Engine.

## 0. Dependencias

- Content#0097a merged (8 combos + `ComboCatalogSO`).
- Foundation#0005 merged (`ServiceBootstrapSO`, `BootstrapRunner`).
- T100a merged (`EnergyConfig` en `RulesetSO`).
- T100c merged (`TurnOrderConfig` en `RulesetSO`).

## 1. Extender `Ruleset_FP.asset`

1. Abrir `Ruleset_FP.asset` (o el ruleset del FP que se este usando).
2. En el inspector aparece una nueva seccion **Weakness (§5 — T97b)**.
3. Setear `DefaultMultiplier = 1.5` (GDD #97 default).
4. Guardar.

## 2. Crear el asset del Guerrero (`ClassHeroSO`)

1. Crear carpeta `Assets/Rollgeon/Heroes/` si no existe.
2. **Assets -> Create -> Rollgeon/Heroes/Class Hero**. Nombrar `ClassHero_Warrior.asset`.
3. Setear campos:
   - `EntityId` = `hero.warrior`
   - `DisplayName` = `Guerrero`
   - `Description` = copy corto del GD (ej. "Arquetipo balanceado — contrato completo de generala").
4. Ignorar los campos `[STUB]` (BaseMaxHp, BaseSpeed, Portrait, StartingDiceBagRef, PassiveRef) —
   los eleva la tarea futura de Hero Template.

## 3. Poblar el `ContractSheet`

En el mismo asset, seccion **Contract (§5.3)**:

1. Drag los 8 assets de combo creados en Content#0097a a `Sheet.Combos`, **en este orden
   exacto** (low-priority -> Generala ultimo):
   1. `Combo_Par.asset`
   2. `Combo_DoblePar.asset`
   3. `Combo_Sum4.asset`
   4. `Combo_Trio.asset`
   5. `Combo_Escalera.asset`
   6. `Combo_FullHouse.asset`
   7. `Combo_Poker.asset`
   8. `Combo_Generala.asset`
2. Setear `DisplayLabel` (private, via debug mode del inspector) = `Contrato del Guerrero`.
   Si el inspector no expone el campo, lo setea `ContractWarriorFactory.Build()` en codigo
   (ver seccion 7 abajo).

> Alternativa code-only: crear un script de editor que llame
> `ContractWarriorFactory.Build(comboCatalog)` y asigne el resultado al
> `ClassHero_Warrior.asset.Sheet`. Util si se automatizan setups en batch.

## 4. Registrar el catalogo de combos en el `ServiceBootstrapSO` (si no esta)

El `ValueDropdown` de `EnemyDataSO.WeaknessComboId` lee del `ComboCatalogSO` registrado en
`ServiceLocator`. Si el catalogo aun no esta en `ServiceBootstrap_Global.SettingsAssets`,
agregarlo ahora (es la misma instruccion del setup de Content#0097a).

## 5. Crear el enemy stub de ejemplo (`EnemyDataSO`)

1. Crear carpeta `Assets/Rollgeon/Entities/` si no existe.
2. **Assets -> Create -> Rollgeon/Entities/Enemy Data (STUB)**. Nombrar
   `EnemyData_Sample.asset`.
3. Setear campos:
   - `EntityId` = `enemy.sample.fullhouse_weak`
   - `DisplayName` = `Enemigo de prueba`
   - `WeaknessComboId` = `combo.full_house` (via dropdown; si esta vacio, la seccion 4 no se
     hizo — repetir).
   - `WeaknessMultiplierOverride` = `0` (= usa el default global del ruleset).
4. Guardar.

> Nota: el asset es un **STUB**. T99 lo va a extender con HP, Speed, behaviors, etc.
> El contrato entre este worktree y T99 es que los 4 campos de arriba **no** se renombran.

## 6. Registrar el bootstrap del weakness service

1. **Assets -> Create -> Rollgeon/Bootstrap/Weakness Service**. Nombrar
   `WeaknessServiceBootstrap.asset`.
2. Drag el asset a `ServiceBootstrap_Global.ExtraServices`.
3. Verificar que `Priority = 110` (post-RulesetSO / TurnOrder).

## 7. Verificacion (play mode)

1. Entrar a la scene `00_Bootstrap` y darle Play.
2. En Console, verificar que no hay errores del `BootstrapRunner` (ruleset registrado,
   weakness service Register() exitoso).
3. En un script de prueba (o consola de Odin), hacer:
   ```csharp
   var registry = ServiceLocator.GetService<IWeaknessRegistry>();
   var checker = ServiceLocator.GetService<IWeaknessChecker>();
   var target = Guid.NewGuid();
   registry.SetWeakness(target, "combo.full_house", 0f);
   float mult = checker.GetMultiplier(Guid.NewGuid(), target, "combo.full_house");
   // Esperado: mult == RulesetSO.Weakness.DefaultMultiplier (1.5f).
   ```
4. Los tests NUnit cubren los paths restantes:
   `Rollgeon.Heroes.Tests.ContractSheetTests` + `Rollgeon.Combat.Weakness.Tests.WeaknessCheckerTests`.

## 8. Rollback

Si algun asset queda corrupto:
- Borrar `Assets/Rollgeon/Heroes/ClassHero_Warrior.asset` y repetir seccion 2–3.
- Borrar `Assets/Rollgeon/Entities/EnemyData_Sample.asset` y repetir seccion 5.
- Los combos de Content#0097a NO se tocan.
