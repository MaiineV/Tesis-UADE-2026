# Setup — Botón Continue en 01_MainMenu

> Feature Continue/resume (rama `Feature#0018_MainMenuContinue`).
> **El wiring ya está aplicado** (vía MCP, 2026-07-06): `ContinueButton` clonado
> del Play en `01_MainMenu` (anchored y=-35, label "Continue", sin handlers
> persistentes — el listener se agrega por código en `OnEnable`) y cableado al
> campo `_continueButton` de `MainMenuScreen`. Este doc queda como referencia y
> checklist de smoke.

## Ajustes opcionales de layout

El botón quedó 10px debajo del Play con el mismo tamaño (300×75). Si el diseño
del menú pide otro orden/espaciado, moverlo libremente — el código no depende
de la posición.

## Verificación (los assets ya editados por código)

- `Assets/Rollgeon/SaveSettings.asset` → `MaxSaveSlots = 1` (slot único).
- `Assets/Rollgeon/Services/ComboPassiveBootstrap.asset` → `_passivePool`
  apunta a `ComboPassivePool.asset` (resolver del save de pasivas).
- Si el Inspector no los muestra, forzar un refresh (Ctrl+R).

## Smoke test del feature completo

1. Play desde `00_Bootstrap` → menú: **Continue debe estar apagado** (sin save).
2. Empezar run, comprar algo / encantar un dado / recibir daño, bajar de piso.
3. Pausa → Quit run → menú: **Continue prendido**.
4. Continue → misma build (dados + enchantments), mismo oro y HP, mismo piso
   (regenerado idéntico — mismas rooms), inventario intacto. Arrancás al inicio
   del piso.
5. Morir o ganar → menú: **Continue apagado** (save borrado).
6. Extra: mid-run cerrar el juego (Alt+F4) → reabrir → Continue prendido
   (flush de Exit).
