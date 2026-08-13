# Chain: consumo de rolls entre fases + entrada paga (1E)

> Estado al 2026-07-20. Fix de los bugs de rolls en chains (ej. Base Attack del
> warrior: fase daño → fase shield).

## Reglas

Al terminar una fase de chain, la fase siguiente entra según el pool de rolls
sobrante y la energía (`CombatHandoffService.ResolveChainPhaseEntry`):

| Rolls sobrantes | Energía | Resultado |
|---|---|---|
| ≥1 | — | **Free**: la fase abre con su tirada lista; el primer roll consume 1 del pool (sobraban 2 ⇒ tras tirar queda 1 reroll). |
| 0 | ≥1 (y el behavior permite energy-reroll) | **Paid**: la fase abre SIN dados, con el prompt "Shield Roll (1E)" en el centro del tablero. El botón Roll cobra 1 de energía y habilita el throw. Pass / End Turn salen sin cobrar. |
| 0 | 0 (o energy-reroll prohibido) | **Finish**: el chain corta (comportamiento previo). |

> Antes existía el gate BUG-019 ("la energía sola no habilita la phase") y un
> `+1` que regalaba el primer roll de la fase — ambos revertidos a pedido de
> diseño (2026-07-20).

## Piezas

- `CombatHandoffService`: helper puro `ResolveChainPhaseEntry` (testeado en
  `CombatHandoffServiceTests`), flag `_awaitingChainPaidRoll`, rama de cobro en
  `hud.OnRollRequested` (paid path del `RerollBudgetService`: 0 free ⇒ 1E).
  Confirm (Space) es no-op mientras el roll pago está pendiente.
- `ChainRollPromptView` (`Rollgeon.UI.HUD`): prompt del board. Formato
  serializado `"{0} Roll (1E)"`; `{0}` = `ChainPhase.Label`.
- `CombatHUDView.Show/HideChainRollPrompt`: passthrough cross-canvas
  (`_chainRollPrompt`), opcional — sin wiring la fase paga funciona sin prompt.

## Wiring (ya aplicado vía MCP, 2026-07-20)

- `Canvas_ActionRoll.prefab` → `DiceZoneView/ChainRollPrompt`: GO **inactivo**
  con TMP (m6x11plus + material MenuOutline, 24 pt, centrado, raycast off) +
  `ChainRollPromptView` con `_label` wireado.
- `02_Gameplay.unity` → `CombatHUDView._chainRollPrompt` = ese view (override
  de instancia, igual que `_boardSkin`).
- `CH_Warrior.asset`: `ChainPhase.Label` autorado — Base Attack `[0]="Attack"`,
  `[1]="Shield"`; Special Attack `[0]="Attack"` (editar labels vía código Odin,
  no por SerializedProperty).

## Verificación

Play → combate con Base Attack:

1. Dejando rerolls sin usar: al entrar la fase shield el contador muestra el
   pool sobrante menos el roll en curso.
2. Gastando todos los rolls con 0 energía: el chain corta tras el daño.
3. Gastando todos los rolls con ≥1 energía: prompt "Shield Roll (1E)", el botón
   Roll cobra 1E y deja tirar; los rerolls siguientes también salen 1E.
4. Pass o End Turn durante el prompt: salen sin cobrar energía.
