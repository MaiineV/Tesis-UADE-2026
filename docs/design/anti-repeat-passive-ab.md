# Anti-Repeat Passive (A/B) — Implementation Plan

> Feature: an A/B-selectable anti-repeat "passive" with two mutually exclusive modes.
> - **Mode COMBO (default)**: repeating the LAST combo deals **0 damage**, and the
>   damage-preview UI shows `"Combo repetido: 0 daño"` instead of the number.
> - **Mode DICE**: block one of the player's dice each turn (existing lock-icon mechanic),
>   independent of boss AI.
> Switchable via a config ScriptableObject read at runtime + a dev console command.
>
> Status: PLAN ONLY — nothing implemented.

---

## 1. The removed repeat-combo-zero rule (commit `558b493c`)

Commit `558b493c` (`fix(combat): remove global repeat-combo-deals-zero rule`,
Thu Jul 23 2026) deleted the guard from
`Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs`, its tests
(`Combat/Pipelines/Tests/DamagePipelineTests.cs`), and a fixture
(`.../Tests/EffDealDamage_RepeatComboTests.cs`). The `IComboLogService` recording in
`CombatHandoffService` was intentionally kept (comment reworded only).

### Deleted guard in `Resolve()` (was between the zero-guard and the outgoing-mult stage, ~old line 65)

```csharp
// ── 0b. Repeat-combo guard (no repetir el mismo combo 2 veces seguidas) ──
// Record() ya corrió para este golpe (CombatHandoffService, antes de llegar
// acá) — comparamos contra el anterior al que acaba de empujar al frente.
if (IsRepeatOfPreviousCombo(ctx.ComboId, alreadyRecordedThisAttack: true))
{
    ctx.FinalDamage = 0;
    ctx.WeaknessMultiplier = 1f;
    ctx.WasLethal = false;
    return ctx;
}
```

### Deleted mirror in `Preview()` (was after the zero-guard, ~old line 189)

```csharp
// Repeat-combo guard: a diferencia de Resolve, acá el jugador todavía está
// eligiendo dados — Record() para ESTE intento todavía no corrió, así que
// comparamos directo contra el último combo ya confirmado.
if (IsRepeatOfPreviousCombo(ctx.ComboId, alreadyRecordedThisAttack: false))
{
    ctx.FinalDamage = 0;
    ctx.WeaknessMultiplier = 1f;
    ctx.ShieldAbsorbed = 0;
    ctx.BlockedByShield = false;
    return ctx;
}
```

### Deleted helper

```csharp
// "Combo repetido = 0 daño" — memoria de un solo paso contra IComboLogService
// (ya existe, poblado por CombatHandoffService en cada ataque primario con tirada).
// Resolve() corre DESPUÉS de que Record() ya empujó el combo de este golpe al
// frente del historial — el "anterior real" queda en el índice 1. Preview() corre
// ANTES de que Record() confirme el intento — el "anterior real" es directamente
// LastCombo (índice 0). ComboId vacío (ataques sin combo / no-jugador) nunca activa
// la regla.
private static bool IsRepeatOfPreviousCombo(string comboId, bool alreadyRecordedThisAttack)
{
    if (string.IsNullOrEmpty(comboId)) return false;
    if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null) return false;

    if (alreadyRecordedThisAttack)
    {
        var lastTwo = log.Last(2);
        return lastTwo.Count >= 2 && lastTwo[1] == comboId;
    }

    return log.LastCombo == comboId;
}
```

Also removed the `using Rollgeon.Combat.ComboLog;` import (DamagePipeline.cs old line 5).

**This is exactly what Mode COMBO must restore — but gated behind the toggle**
(the current file has NONE of this; see `Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs:50-211`).

---

## 2. Combo repeat detection

- Interface: `Assets/Scripts/Rollgeon/Combat/ComboLog/IComboLogService.cs`
  - `void Record(string comboId)` — null/empty normalized to `NoComboMarker` (`"combo.none"`).
  - `string LastCombo` — most recent combo, or `null` if empty (line 32).
  - `IReadOnlyList<string> Last(int count)` — most-recent-first window (line 38).
  - `string NoComboMarker` (line 23).
- Impl: `Assets/Scripts/Rollgeon/Combat/ComboLog/ComboLogService.cs`
  - `History[0]` is most recent; `Record` inserts at 0 (line 67-72); `LastCombo` = `History[0]` (line 75).
  - Run-scoped: cleared on `OnCombatEnd` / `OnRunEnd` (line 41-42, 88, 94). Registered global.
- Recording sites in `Assets/Scripts/Rollgeon/Combat/Handoff/CombatHandoffService.cs`:
  - Primary attack path: `comboLog.Record(combo != null ? combo.ComboId : null)` at **line 471-472**.
  - Chain phase-0 path: same call at **line 1527-1530** (`DetectChainCombo`).
- Bootstrap: `Assets/Scripts/Rollgeon/Combat/ComboLog/ComboLogServiceBootstrap.cs` (thin wrapper, registers global).

**API to check "current combo == last combo":** exactly the deleted `IsRepeatOfPreviousCombo`
logic — for `Resolve` (Record already ran for this hit) compare `Last(2)[1] == comboId`;
for `Preview` (Record not yet run) compare `LastCombo == comboId`. Empty `ComboId` never triggers.

---

## 3. The damage preview UI

Component: `Assets/Scripts/Rollgeon/UI/HUD/DamageFormulaView.cs`
(`Rollgeon/UI/HUD/Damage Formula View`, class `DamageFormulaView`).

- The on-screen "cuánto daño hacés" number is computed and rendered in
  `UpdateFormula()` — **lines 224-303**.
- The damage-mode branch (not ActionRoll / not defense phase) starts at **line 245**:
  - combo presence gate at **line 268** (`if (string.IsNullOrEmpty(_lastComboId))`).
  - `comboName` resolved at **line 274**.
  - pre-mitigation via `PlayerComboDamage.Resolve(...)` at **line 283-284**.
  - item bonus preview `inventory.GetComboDamageBonusPreview(_lastComboId)` at **line 291**.
  - mitigation via `Mitigate(...)` at **line 295-297**, which calls
    `pipeline.Preview(ctx)` — **line 313-332** (`Mitigate()`), the actual `Preview` call at
    **line 328**.
  - final text built at **line 299-302** and drawn via `RenderLabel(text, value)` (line 384-388).
- `_lastComboId` is set by `OnComboMatched` (line 213-222) from `ComboMatchedPayload`.
- Localization already imported/used here: `Rollgeon.Localization.LocalizedContent.Name(...)` at line 352.

**Injection point for the warning:** inside `UpdateFormula()`, in the damage-mode branch,
right after `comboName` is resolved (after line 274) and BEFORE the mitigation/number
computation. If Mode COMBO is active AND the current `_lastComboId` repeats the last logged
combo, render the warning text and return early:

```csharp
// after line 274
if (antiRepeat.Mode == AntiRepeatMode.Combo && IsRepeatOfLastCombo(_lastComboId))
{
    RenderLabel(LocalizedContent.Ui("combat.combo_repeated_zero", "Combo repetido: 0 daño"), 0);
    return;
}
```

Note: even without touching the UI, restoring the `Preview` guard would make `Mitigate`
return 0 and the label would read `"{comboName}: 0"` — but the spec wants the explicit
warning string, so the UI needs its own repeat check (it must not just rely on the number).

---

## 4. The dice-block system (Mode DICE)

- Interface: `Assets/Scripts/Rollgeon/Combat/DiceBlock/IDiceBlockService.cs`
  - `Block(int index)`, `Unblock(int)`, `IsBlocked(int)`, `BlockedIndices`, `Clear()`.
- Impl: `Assets/Scripts/Rollgeon/Combat/DiceBlock/DiceBlockService.cs`
  - `Block` fires `EventName.OnDiceBlockChanged` (line 82-87).
  - Auto-release: subscribes to `OnTurnFinished` filtered by player guid → `Clear()` (line 114-120),
    plus `OnCombatEnd`/`OnRunEnd`.
  - Registered global by `DiceBlockService/DiceBlockServiceBootstrap.cs`.
- Lock-icon UI: `Assets/Scripts/Rollgeon/UI/HUD/DiceZoneView.cs`
  - subscribes `OnDiceBlockChanged` (line 157) → `RefreshDiceBlock()` (line 281-296),
    which calls `_resolvedSlots[i]?.SetBlocked(blocked)` (line 296) — the greyed slot + candado.
  - Blocked dice are also excluded from combos/keep (line 420-421, 479-484).
- Current trigger (boss only): `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_RotateBlock.cs`
  - `Target = Dice` → `RotateDice()` (line 53-81): `dice.Clear()` then Fisher-Yates picks
    `Count` random slot indices and `dice.Block(index)`. Runs at the boss's turn end.

**How Mode DICE would block a die each turn, independent of the boss:**
`DiceBlockService` already auto-clears on the player's `OnTurnFinished`. There is currently
**no player-combat-wide trigger** that blocks a die at the start of each player turn — only
`AINode_RotateBlock` does, and only when a boss with that node is present.

Mode DICE therefore needs a NEW tiny handler/service (player-global) that subscribes to
`EventName.OnTurnStarted` (arg[0] = entity guid; player turns fire with the player guid — see
`AnalyticsAggregationTests.cs:105-108` pattern) and, when the started turn is the player's and
Mode == Dice, picks one random un-blocked slot from the player's `DiceBag` and calls
`IDiceBlockService.Block(index)`. Reuse the bag-size resolution logic from
`AINode_RotateBlock.ResolveBagSize` (line 115-123) and RNG helper (line 125-131). The service
must no-op when Mode == Combo.

---

## 5. Where the A/B toggle should live

### (a) The enum + config SO
- New file `Assets/Scripts/Rollgeon/Combat/AntiRepeat/AntiRepeatMode.cs`:
  ```csharp
  namespace Rollgeon.Combat.AntiRepeat
  {
      public enum AntiRepeatMode { Combo, Dice } // Combo default (index 0)
  }
  ```
- New config SO `Assets/Scripts/Rollgeon/Combat/AntiRepeat/AntiRepeatConfigSO.cs`
  following `CameraConfigSO` pattern (`[CreateAssetMenu(menuName = "Rollgeon/Combat/Anti-Repeat Config")]`,
  a `SerializedScriptableObject`) with one authored field
  `public AntiRepeatMode DefaultMode = AntiRepeatMode.Combo;`. Dropped into
  `ServiceBootstrapSO.SettingsAssets` (registered global by runtime type — see
  `ServiceBootstrapSO.cs:109-122, 217-228`), exactly like `CameraConfigSO`.

### (b) Runtime state (so the dev command doesn't mutate the asset)
Because `SettingsAssets` registers the SO instance itself (line 119) and mutating it at
runtime in-editor dirties the asset, add a small runtime state service that seeds from the
config and is what everything reads/flips:
- `Assets/Scripts/Rollgeon/Combat/AntiRepeat/IAntiRepeatModeService.cs`:
  `AntiRepeatMode Mode { get; }` + `void SetMode(AntiRepeatMode mode)`.
- `Assets/Scripts/Rollgeon/Combat/AntiRepeat/AntiRepeatModeService.cs` (POCO,
  `IPreloadableService`) — on `Register()` reads `AntiRepeatConfigSO.DefaultMode` from the
  ServiceLocator (fallback `Combo`), stores it, registers itself global. `SetMode` fires an
  event (e.g. new `EventName.OnAntiRepeatModeChanged`) so the HUD/dice handler can refresh.
- `Assets/Scripts/Rollgeon/Combat/AntiRepeat/AntiRepeatModeServiceBootstrap.cs` — thin
  wrapper like `DiceBlockServiceBootstrap.cs`, added to `ServiceBootstrapSO.ExtraServices`.
- The Mode DICE per-turn handler (§4) can live in the same `AntiRepeatModeService` (subscribe
  `OnTurnStarted`), keeping the feature in one place.

### (c) How the readers consume it
- `DamagePipeline.Resolve/Preview` (`DamagePipeline.cs:50, 178`): restore
  `IsRepeatOfPreviousCombo` and wrap the zeroing guard in
  `if (mode == AntiRepeatMode.Combo && IsRepeatOfPreviousCombo(...))`. Resolve it via
  `ServiceLocator.TryGetService<IAntiRepeatModeService>` (pipeline already uses ServiceLocator
  in its parameterless ctor, line 42-47; add a nullable field or read lazily). If the service
  is absent, default to Combo-off/legacy behavior to keep tests green.
- `DamageFormulaView.UpdateFormula` (§3): read the same service for the warning branch.
- Mode DICE handler: reads `Mode == Dice` before blocking.

### (d) Dev console command `passive dice|combo`
Model on `DiceModeCommand` (`DevConsole/Commands/Concrete/DiceCommands.cs:126-169`):
- New `PassiveCommand : DevCommandBase` in a new file
  `Assets/Scripts/Rollgeon/DevConsole/Commands/Concrete/PassiveCommands.cs`:
  - `Name => "passive"`, one optional Choice arg `dice|combo` (no arg = report current).
  - `RequireService<IAntiRepeatModeService>(ctx, out var svc, out var e)` then
    `svc.SetMode(...)` and `CommandResult.Ok(...)`.
- Register in `Assets/Scripts/Rollgeon/DevConsole/Commands/DefaultCommands.cs` in the
  "Combate / extras" block (after line 47): `r.Register(new PassiveCommand());`.

---

## 6. Localization

Pattern: `Rollgeon.Localization.LocalizedContent.Ui(string key, string fallback)`
(`Assets/Scripts/Rollgeon/Localization/LocalizedContent.cs:59-60`) — looks up the `UI` table
by exact key, returns fallback if missing/not-loaded. Used across the codebase (e.g.
`EnchantmentAltarView.cs:224` `LocalizedContent.Ui("altar.title", "Altar de Encantamiento")`).

- Suggested key: **`combat.combo_repeated_zero`** → fallback `"Combo repetido: 0 daño"`.
- Add the entry to the `UI` localization table (see `docs/setup/localization-setup.md:121`
  for the `UpsertEntry("UI", ...)` flow). The fallback means the feature works even before
  the table entry exists.
- Optional: also localize the dev-console `Ok` strings, but console output is dev-only and the
  existing commands use raw Spanish literals — matching that is fine.

---

## Step-by-step implementation plan

1. **Enum + config SO + runtime service** (new folder `Combat/AntiRepeat/`):
   `AntiRepeatMode.cs`, `AntiRepeatConfigSO.cs`, `IAntiRepeatModeService.cs`,
   `AntiRepeatModeService.cs`, `AntiRepeatModeServiceBootstrap.cs`. Add
   `EventName.OnAntiRepeatModeChanged` to `Patterns/EventName.cs`.
2. **Restore the COMBO guard, gated** in `DamagePipeline.cs`:
   re-add `using Rollgeon.Combat.ComboLog;`, the `IsRepeatOfPreviousCombo` helper, and the two
   guards in `Resolve` (~line 64) and `Preview` (~line 191), each wrapped in a Mode==Combo check.
3. **UI warning** in `DamageFormulaView.UpdateFormula` (after line 274): repeat-check +
   `LocalizedContent.Ui("combat.combo_repeated_zero", "Combo repetido: 0 daño")` early-return.
4. **Mode DICE per-turn wiring**: in `AntiRepeatModeService`, subscribe `OnTurnStarted`,
   filter to player guid, and when Mode==Dice pick one random un-blocked slot and
   `IDiceBlockService.Block(index)`. (Bag-size + RNG lifted from `AINode_RotateBlock`.)
5. **Dev console command** `PassiveCommands.cs` + register in `DefaultCommands.cs`.
6. **Localization**: add `combat.combo_repeated_zero` to the `UI` table.
7. **Wiring assets** (Unity, needs the user/MCP): create `AntiRepeatConfig.asset`, add it to
   `ServiceBootstrapSO.SettingsAssets`, and add `AntiRepeatModeServiceBootstrap` to
   `ServiceBootstrapSO.ExtraServices`.
8. **Tests** (EditMode): re-add repeat-combo cases for `DamagePipelineTests` (now asserting they
   only zero when Mode==Combo, and full damage when Mode==Dice); a test for
   `AntiRepeatModeService` seeding + `SetMode`; a test that Mode==Dice blocks exactly one die on
   player `OnTurnStarted`.

---

## Design forks / risks to decide BEFORE implementing

- **Passive scope: player-global vs per-run vs unlockable.** The plan registers the config
  global and the state service global (survives across runs), matching the A/B-test framing.
  If this should instead be a per-run acquired passive (like items/enchants), it belongs
  Run-scoped and gated by inventory/upgrade acquisition — a different wiring. **Decide this.**
- **Mode DICE has no existing per-turn trigger.** Everything today drives dice-block through
  boss AI (`AINode_RotateBlock`). Mode DICE needs the NEW `OnTurnStarted` handler (§4). Confirm
  `OnTurnStarted` fires with the player guid on the player's turn *before* the player rolls
  (verify against `PlayerTurnState` / `TurnManager` at runtime — tests fire it manually, so the
  real firing order must be checked). Risk: if it fires after the roll, the block won't affect
  the current turn's dice.
- **Mode DICE + boss that also blocks dice.** If a floor-1 boss (`AINode_RotateBlock Target=Dice`)
  is present AND Mode DICE is on, both will call `Block` — additive, and both share the same
  `OnTurnFinished` clear. Probably fine, but decide whether Mode DICE should suppress itself in
  boss rooms to avoid double-blocking.
- **Config mutation vs runtime override.** Chosen: config SO holds the authored default, a
  runtime service holds the live value (so the dev command never dirties the asset). Confirm you
  don't instead want the command to persist across sessions (which would need PlayerPrefs or a
  settings save, not a config SO).
- **Original "double-pair 4-4-3-3 twice" bug.** The rule was removed precisely because it
  surfaced as a confusing bug with no UI. Mode COMBO now adds the UI warning, but confirm the
  repeat semantics you want: strictly the immediately-previous combo (one-step memory, as the
  deleted helper did) — `Par → DoblePar → Par` should NOT zero (a deleted test asserted this).
