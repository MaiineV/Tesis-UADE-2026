# Setup — Feature#0104: Energy x Reroll (RerollBudgetService)

Engine-side instructivo post-merge del presupuesto de rerolls por energia.
Todo el codigo C# vive en `Assets/Scripts/Rollgeon/Dice/`; este documento
cubre los pasos manuales de Unity (crear `.asset` del bootstrap, wiring al
`ServiceBootstrapSO`, ajustes a los `ActionDefinitionSO` existentes).

TECHNICAL.md §6.5.

---

## 1. Actualizar los `ActionDefinitionSO` existentes

`ActionDefinitionSO` gano un campo nuevo: **`FreeRollCount`** (int, default 1).

**Convencion**: cuenta las **tiradas TOTALES** incluidas gratis (la inicial +
los rerolls gratis). El servicio convierte internamente a rerolls gratis
como `max(0, FreeRollCount - 1)`.

Editar cada `.asset` en `Assets/ResourcesData/Actions/` y ajustar:

| File                          | FreeRollCount | AllowsEnergyReroll | Rationale                          |
|-------------------------------|---------------|--------------------|-----------------------------------|
| `Action_AttackBasic.asset`    | **3**         | true               | Generala-style: 1 roll + 2 free rerolls. |
| `Action_AttackSpecial.asset`  | **3**         | true               | Idem.                              |
| `Action_Heal.asset`           | **1**         | true               | Skill check: 1 roll gratis, luego pagar. |
| `Action_ForceDoor.asset`      | **1**         | true               | Idem.                              |
| `Action_Move.asset`           | 1             | false              | No aplica dice.                   |
| `Action_EndTurn.asset`        | 1             | false              | No aplica dice.                   |

> Si el catalogo mete mas `ActionDefinitionSO`s en el futuro, mantener la
> convencion: ataques = `3`, skill checks normales = `1`,
> cutscene/one-shot = `1` + `AllowsEnergyReroll = false`.

---

## 2. Crear `RerollBudgetServiceBootstrap.asset`

**Donde**: `Assets/ResourcesData/Bootstrap/RerollBudgetServiceBootstrap.asset`.

**Como**: Project view > boton derecho >
`Create > Rollgeon > Dice > Reroll Budget Service Bootstrap`.

El asset es un wrapper thin — no tiene campos para configurar. La unica
razon de ser un SO es para arrastrarlo al `ServiceBootstrapSO`.

---

## 3. Wire al `ServiceBootstrap.asset`

1. Abrir `Assets/ResourcesData/Bootstrap/ServiceBootstrap.asset`.
2. En **Extra Runtime Services**, agregar un slot y arrastrar
   `RerollBudgetServiceBootstrap.asset`.
3. Confirmar que el orden queda (por Priority ascendente):
   - `EnergyServiceBootstrap` (Priority 50)
   - `TurnManagerBootstrap` (Priority 60)
   - `RerollBudgetServiceBootstrap` (Priority 70) ← **nuevo**

> `RerollBudgetService.Register()` falla con `LogError` si no encuentra un
> `IEnergyService` ya registrado. Respetar el orden de Priority.

---

## 4. Runtime integration (opcional, pendiente de T95b)

Esta feature expone la API pero **no** wiring concreto al combat controller
ni al `DiceRoller`. Los callers futuros deben seguir el pattern:

```csharp
// Combat controller / TurnManager integration (pseudo)
var reroll = ServiceLocator.GetService<IRerollBudgetService>();

reroll.StartBudget(action);                       // al arrancar la accion
try
{
    // ... dice roll inicial ...
    while (playerWantsExtraRoll)
    {
        var q = reroll.QueryExtraRoll(player.Guid);
        if (!q.IsAvailable) break;               // HUD muestra q.BlockedReason

        // HUD renderiza "Reroll (gratis)" o "Reroll (1E)" segun q.IsFreeRoll / q.CostsEnergy
        if (await hud.WaitForConfirm())
        {
            if (reroll.TryExtraRoll(player.Guid))
            {
                diceRoller.Reroll(...);
            }
        }
        else break;
    }
}
finally
{
    reroll.EndBudget();                           // siempre, incluso en excepcion
}
```

### Para HUD authors (T95b)

Los 3 puntos de acoplamiento:

1. **`QueryExtraRoll(playerGuid)`** — puro, llamar cada vez que se abre el
   menu de reroll y cada vez que termina un reroll (para refrescar label).
2. **`TryExtraRoll(playerGuid)`** — disparar al click del boton "Reroll".
3. **`OnRerollStarted`** — suscribirse para feedback visual (shake, VFX).
   El payload trae snapshot post-consumo: `FreeRollsRemaining` y
   `PaidRollsUsed` ya actualizados.

Tambien se dispara el evento legacy `EventName.OnRerollStarted` con el
schema `[Guid sourceGuid, int rerollIndex]` para compatibilidad con el
schema documentado en TECHNICAL.md §1.2. Nuevo codigo deberia preferir el
callback tipado `IRerollBudgetService.OnRerollStarted`.

---

## 5. Politica de non-rollback (pit)

Si `IEnergyService.SpendEnergy` cobra exitosamente y el reroll subsecuente
falla por una razon externa (combat cancelado, error en `DiceRoller`, etc),
**la energia queda gastada**. El servicio no implementa rollback transaccional
— es el modelo simple y consistente con el resto del economy.

Impacto para el player: si hay un feel-bad moment en playtest, abrir un ticket
de follow-up a Sprint 04 para evaluar wrap transaccional o confirmacion explicita.

---

## 6. Verificacion

1. Reabrir Unity, esperar recompilacion — 0 errores.
2. En un EditMode runner, correr las tests de `Rollgeon.Dice.Tests` — 23 tests
   deben pasar.
3. Smoke test manual (opcional, requiere T95b / DiceRoller):
   - Entrar a combate.
   - Consumir los 2 rerolls gratis de un ataque.
   - Confirmar que el tercer reroll gasta 1 punto de energia.
   - Confirmar que el HUD muestra "no-energy" cuando la energia llega a 0.

---

## 7. Followups abiertos (no-blocking)

- **`RulesetSO.EnergyCostPerExtraReroll`** — hoy fallback a constante `1`.
  Balance#0101 / Sprint 04 debe publicar el campo en `RulesetSO`; el servicio
  lo leera automaticamente (ya tiene el hook comentado).
- **`RulesetSO.MaxExtraRerollsByEnergy`** — enforcement de cap superior.
  `RerollBudget.PaidRollsUsed` ya lleva la cuenta; solo falta el check + el
  campo en el SO.
- **Adapter `IRerollBudget`** (§6.5 TECHNICAL) — wrapper de ~20 lineas que
  proyecta `IRerollBudgetService` al contrato del `DiceRoller`. Lo autorea
  T95b cuando wirea el roll UI.
