# Modo de reroll (invertido / clásico) — toggle en Opciones

> Estado: implementado (2026-08-12). Opción "Reroll: …" en el panel de opciones —
> menú principal y pausa in-game.
>
> **Historia**: desde el QoL de input estilo Balatro (`9281b875`) el reroll es
> "invertido": los dados que el jugador **selecciona son los que se re-tiran**.
> Este toggle agrega la modalidad clásica para quien la prefiera: los
> seleccionados **se quedan** y vuelan los demás.

## El modelo

- **Fuente de verdad**: `Rollgeon.Dice.RerollSelectionPrefs` (static, PlayerPrefs
  `Rollgeon.RerollKeepSelected`, default `false` = invertido). El mapeo
  selección→keep vive en `RerollSelectionPrefs.SelectionToKeep`; los dos flujos
  de reroll lo consumen ahí (pull-at-use-time, sin eventos — el cambio aplica al
  próximo reroll).
- **Combate**: `CombatHandoffService.KeepFromSelection` delega en el helper.
  En clásico los holds **persisten** tras el reroll (los lockeados siguen siendo
  el pick de combo); en invertido el descarte consume la selección, como hasta ahora.
- **ActionRoll (Heal / Forzar Puerta)**: `ActionRollService.RequestReroll()` usa
  el mismo helper; `CanAffordReroll` gatea por modo (invertido: ≥1 seleccionado;
  clásico: ≥1 NO seleccionado re-tirable).
- **Guards compartidos** (`AllDiceHeld(keep)` / `AllTrue(keep)`): operan sobre el
  keep físico ya mapeado — "keep all-true ⇒ nada vuela ⇒ bail sin cobrar" vale en
  ambos modos, no cambiaron.

## Qué NO cambia con el toggle (a propósito)

- **Grab-to-reroll 2D**: lo que vuela es lo AGARRADO (keep físico del presenter),
  no la selección. En grab-mode el `OnRerollStarted` sigue consumiendo la
  selección aunque rija el clásico.
- **Detección de combos**: la selección es el pick de combo en ambos modos.
- **Dados bloqueados por boss**: nunca vuelan (`KeepForcingBlockedDice` /
  `ForceKeepBlocked` corren después del mapeo).
- **Reroll forzado del Torpe**: siempre re-tira toda la mano.

## UI / instalación

Botón `RerollModeButton` (flip de dos estados, patrón Tutorial/Analytics) entre
"Velocidad" y la fila de idioma. Labels:

| Estado | Key | ES | EN |
|---|---|---|---|
| default (invertido) | `menu.reroll_discard` | Reroll: vuelan los elegidos | Reroll: rerolls selected dice |
| clásico | `menu.reroll_keep` | Reroll: se quedan los elegidos | Reroll: keeps selected dice |

**Pasos de wiring (usuario, en el editor):**

1. `Rollgeon → Juicy Menu → 4 - Setup Options Panel` — regenera el panel en
   `01_MainMenu` (ahora 560×**800**, fila nueva incluida) y upsertea las keys.
2. `Rollgeon → Juicy Menu → 5 - Setup Pause Options Panel` — ídem para
   `Canvas_PauseMenu.prefab`.
3. Verificar en ambos que la fila nueva aparece sin solaparse (título 330,
   filas 230/150/70/−10, idioma −80/−140, borrar −240, volver −320).

DevConsole: `rerollmode` muestra el estado; `rerollmode discard|keep` lo setea.

## Checklist de smoke

- [ ] Toggle OFF (default): todo idéntico a hoy — seleccionar 2/5 y Reroll re-tira esos 2.
- [ ] Toggle ON, combate: seleccionar 2/5 → Reroll re-tira los otros 3; los 2 quedan
      lockeados (visual y combo) tras el reroll.
- [ ] Toggle ON, nada seleccionado → Reroll re-tira toda la mano y cobra budget/energía.
- [ ] Toggle ON, todo seleccionado → botón deshabilitado (y si se fuerza, no cobra).
- [ ] Toggle ON, ActionRoll (Heal / Forzar Puerta): misma matriz; Confirm sigue
      exigiendo ≥1 seleccionado.
- [ ] Grab-to-reroll 2D: idéntico en ambos modos.
- [ ] Boss 1: dados bloqueados nunca vuelan en ningún modo.
- [ ] El pref persiste tras reiniciar; el label refleja el estado al reabrir opciones.
