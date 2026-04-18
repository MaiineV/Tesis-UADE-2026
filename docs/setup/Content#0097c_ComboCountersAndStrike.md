# Content#0097c — Combo Counters (Strike DEFERRED)

Instructivo manual de setup Engine tras el merge de T97c. Cubre registro del servicio,
configuración del `RulesetSO` y smoke tests mínimos.

> **Alcance.** Esta tarea implementa **únicamente** Combo Counters (TECHNICAL.md §5.5).
> El sub-sistema de Strike (§5.6) quedó **diferido** (Revisión 2 del plan — TECHNICAL.md
> §5.6 marcado como TBD). Todo archivo / knob referente a Strike no existe en este merge.

---

## 1. Registrar el servicio en el bootstrap

1. Abrir el `ServiceBootstrap.asset` (típicamente en `Assets/Rollgeon/Bootstrap/`).
2. En la sección **Extra Runtime Services**, agregar una entrada nueva.
3. Crear un asset nuevo vía menú `Create → Rollgeon → Bootstrap → Combo Counters Service`
   (archivo por default: `ComboCountersServiceBootstrap.asset`, colocar p.ej. en
   `Assets/Rollgeon/Bootstrap/`).
4. Drag-and-drop el `ComboCountersServiceBootstrap.asset` al slot nuevo de
   `ExtraServices`.
5. La Priority es `80` (heredada de `ComboCountersService.DefaultPriority`) — queda
   después de Energy (50), TurnManager (60) y RerollBudget (70).

El servicio, al registrarse, se suscribe automáticamente a:

- `EventName.OnRunStart` — instancia un `RunComboCounterState` fresco y lo pone en
  `ServiceScope.Run`.
- `EventName.OnRunEnd` — no-op (el state se libera cuando `BootstrapHooks.OnRunEnd`
  hace `ServiceLocator.ClearScope(Run)`).
- `TypedEvent<ComboMatchedPayload>` — cada match incrementa el contador correspondiente.

---

## 2. Configurar el `RulesetSO`

Abrir el `Ruleset.asset` activo (`Assets/Rollgeon/Balance/Ruleset.asset` o equivalente).
Verificar que aparece la sección nueva **Combo Counters (§5.5 — T97c)** con dos sliders:

| Campo | Default | Rango | Semántica |
|---|---|---|---|
| `PerUseBonus` | `0.02` (2%) | `[0, 1]` | Bonus añadido al multiplicador por cada match |
| `MaxBonus` | `0.20` (20%) | `[0, 10]` | Techo del bonus acumulado (independiente del count) |

Fórmula aplicada: `multiplier = 1 + min(MaxBonus, Count * PerUseBonus)`.

- Con defaults, llega al techo tras 10 matches del mismo combo.
- Para desactivar el sistema sin tocar código: `PerUseBonus = 0` **o** `MaxBonus = 0`
  (cualquiera de los dos fuerza el multiplicador a `1.0`).

---

## 3. Validar que los contadores persisten durante la run

1. Abrir la escena `00_Bootstrap` y ponerla en Play.
2. El runtime de combate (T100b) debe disparar `TypedEvent<ComboMatchedPayload>` cada
   vez que resuelve un match. **Nota.** Mientras el `AttackResolver` real no esté
   merged, el counter queda silencioso en play-mode (no hay publisher). Los tests
   EditMode cubren el flujo completo.
3. Para verificar manualmente en play-mode (pre-resolver), disparar el evento desde
   cualquier script con:

   ```csharp
   TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload {
       SourceGuid = Guid.NewGuid(),
       ComboId = "combo.par",
       BaseDamage = 10,
   });
   ```

4. Leer el contador:

   ```csharp
   var svc = ServiceLocator.GetService<IComboCountersService>();
   Debug.Log($"par count = {svc.GetCount("combo.par")}, mult = {svc.GetBonusMultiplier("combo.par")}");
   ```

5. Terminar la run (`EventManager.Trigger(EventName.OnRunEnd, runId, null);`) y
   confirmar que `GetCount("combo.par") == 0` — el state se liberó con
   `ClearScope(Run)` y el contador arranca limpio en la próxima run.

---

## 4. Smoke test — disparar `ComboMatchedPayload`

En EditMode (Window → General → Test Runner) correr la suite
`Rollgeon.Combos.Counters.Tests`. Todos los tests (state, config, service) deben pasar.

En play-mode, para un smoke test rápido sin el AttackResolver real, usar un script
editor que dispare la run y los matches:

```csharp
// Smoke: iniciar run y generar 11 matches → llega al cap.
EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "smoke");
for (int i = 0; i < 11; i++) {
    TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload {
        SourceGuid = Guid.Empty, ComboId = "combo.par", BaseDamage = 10
    });
}

var svc = ServiceLocator.GetService<IComboCountersService>();
Debug.Log($"par count = {svc.GetCount("combo.par")}");           // 11
Debug.Log($"par mult  = {svc.GetBonusMultiplier("combo.par")}"); // 1.20 (capped)
```

---

## 5. Rollback

Si se decide desactivar el sistema sin remover el servicio:

- Setear `Ruleset.Counters.PerUseBonus = 0` → `GetBonusMultiplier` siempre devuelve `1`.
- Los contadores siguen corriendo (inocuo) — consumidores downstream obtienen un
  multiplicador neutro.

Para remoción completa: quitar el `ComboCountersServiceBootstrap.asset` del
`ServiceBootstrap.ExtraServices` y recompilar. El compile sigue limpio (el consumidor
que lea `IComboCountersService` debe usar `ServiceLocator.TryGetService` con fallback).

---

## 6. Strike — diferido

El sub-sistema de Strike (§5.6 — elegir 1 combo "tachado" al inicio de run) quedó
fuera de scope en esta tarea (ver Revisión 2 del plan). Cuando se decida reabrir,
el diseño original del plan v1 sirve como referencia, pero:

- **No existen** en este merge: `IStrikeService`, `StrikeService`, `RunStrikeState`,
  `IStrikableCombo`, `StrikeConfig`, `EffStrikeCombo`.
- `ContractSheet.EvaluateRoll` **no** fue modificado — el comportamiento de T97b
  queda intacto.
- Los combos concretos (`Combo_Par`, `Combo_Trio`, ...) **no** implementan ningún
  marker interface nuevo.
