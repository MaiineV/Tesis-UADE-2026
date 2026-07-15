# Setup — Fuerza Bruta + nueva pasiva del Warrior

> Rama `sprint04/feature/warrior-passive-rework`. El wiring de `CP_Warrior.asset`
> (Hook de la pasiva) y del badge en `Canvas.prefab` ya se aplicó vía Unity MCP
> y se verificó contra el `.asset`/`.prefab` en disco (2026-07-14) — ver
> secciones 2 y 3. `Combo_HighNumber` **no se toca** (ver sección 1, decisión
> revertida). No queda ningún wiring manual pendiente para este feature.

## 1. `Combo_HighNumber` se mantiene en el Warrior (decisión revertida)

`Assets/Rollgeon/Classes/CH_Warrior.asset` referencia dos combos de "número
alto": `Combo_FuerzaBruta` (ya fixeado, ver `Combo_FuerzaBruta.cs`) y
`Combo_HighNumber` (un clon de `Combo_SumaX` con `X=4`, base `10` — el "viene
de 10" que mencionó Santi). En un primer momento se había decidido sacar
`Combo_HighNumber` del Warrior por posible conflicto de prioridad con Fuerza
Bruta ya corregido — **pero Sebas confirmó con Bocco (2026-07-14) que hay
que mantenerlo**. No hacer ningún cambio en `Sheet.Combos` ni
`Sheet.ShieldBaseTable` de `CH_Warrior.asset`; quedan como estaban (9 combos,
8 filas de shield, `combo.higher_number` incluido).

## 2. Pasiva wireada en `CP_Warrior.asset` — HECHO

`Assets/Rollgeon/Classes/CP_Warrior.asset` tiene `PassiveId`, `DisplayName`,
`Description` y ahora también `Hooks` con un elemento:
- **Trigger Event**: `OnAttributeChanged`.
- **Effect** → `EffLowHpAttackBuff` (`Hp Threshold: 3`, `Attack Bonus: 5`,
  ambos default).

Aplicado vía Unity MCP (`manage_scriptable_object`) y verificado leyendo el
`.asset` directo del disco — confirmado `Hooks[0].TriggerEvent: 39` (=
`OnAttributeChanged`) y el effect con `_hpThreshold: 3`, `_attackBonus: 5`.
El umbral es **3** por pedido de GD (2026-07-15): con vida en 3 o menos se
activa, al curarse a 4+ se desactiva — coincide con la `Description` del
asset.

## 3. Badge "pasiva activa" en el HUD de combate — HECHO

`PassiveBadgeView` (`Assets/Scripts/Rollgeon/UI/HUD/PassiveBadgeView.cs`) está
wireado en `Assets/Prefabs/UI/Canvas.prefab`: GameObject `PassiveBadgeView`
(hijo de `CombatHUDView`, al lado de `HealthBarView`/`ShieldBarView`) con un
hijo `Content` (Image de fondo, arranca desactivado) que a su vez tiene
`Label` (`TextMeshProUGUI`, texto "Pasiva activa"). `_container` apunta a
`Content`, `_text` a `Label`. `CombatHUDView._passiveBadge` apunta al
componente `PassiveBadgeView`.

Aplicado vía Unity MCP (`manage_prefabs` + `manage_gameobject` +
`manage_components`, prefab stage guardado con `save_prefab_stage`) y
verificado en el `.prefab` en disco.

### 3.1 Overlay de debug — ELIMINADO

`PassiveActiveDebugOverlay.cs` (un `OnGUI` auto-bootstrapeado vía
`RuntimeInitializeOnLoadMethod` para testear la pasiva antes de que el badge
real estuviera wireado) se eliminó de la rama (2026-07-15): como no tenía
gate de `UNITY_EDITOR`/`DEVELOPMENT_BUILD`, se colaba también en builds de
release. El badge real de la sección 3 lo reemplaza por completo.

## Por qué `OnAttributeChanged` y no un evento de turno

El bind pasa por `Entity.BindPassive` (`Assets/Scripts/Rollgeon/Entities/Entity.cs`),
que solo filtra por `entityId` — no por qué atributo cambió. `EffLowHpAttackBuff`
relee el estado real (HP actual + si el modifier ya está puesto) en cada
invocación, así que dispara de más (Energy, Shield, el propio Attack al
aplicarse) sin problema: son no-ops baratos. Esto hace que el buff reaccione
en el momento exacto en que cambia la vida (daño o cura), no recién en el
turno siguiente.

## Verificación

1. Entrar a un run con Warrior (5 daño base).
2. Bajar la vida a 3 o menos (recibir daño) → el próximo Base Attack debe
   pegar como si `Attack` fuera 10 (5 base + 5 del buff).
3. Curarse a 4+ → el próximo ataque vuelve a pegar como si fuera 5.
4. Morir (o terminar la run) y arrancar de nuevo → el Warrior arranca en 5,
   nunca en 10 (esto es automático: `RunController` crea un `Attack` nuevo sin
   modifiers en cada `OnRunStart`, no depende de este setup).
5. Tirar 5 dados donde todos caigan en la mitad alta de su rango (ej. d6:
   4,5,6,4,5) → debe activarse Fuerza Bruta. Un solo dado por debajo del
   umbral (ej. d6: 4,5,6,4,**2**) → NO debe activarse (antes con 1 solo dado
   ya alcanzaba).
6. Bajar la vida a 3 o menos → el badge real (`PassiveBadgeView`, al lado de
   la vida) aparece. Curarse a 4+ → el badge desaparece. Reabrir el Combat
   HUD a mitad de combate con el buff ya activo → el badge aparece de
   entrada (no hace falta esperar el próximo cambio de HP).

## Tests automáticos (ya corren sin necesitar este setup)

- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectionTests.cs` →
  `Combo_FuerzaBruta_Tests` (reescrita para "todos los 5 dados").
- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectFlatBaseOverrideTests.cs`
  (casos de Fuerza Bruta actualizados al mismo criterio).
- `Assets/Scripts/Rollgeon/Effects/Tests/EffLowHpAttackBuffTests.cs` (incluye
  `IsActiveFor_TrueWhileBuffed_FalseAfterHeal`, el helper que usa el badge).

Suite EditMode completa corrida vía Unity MCP el 2026-07-15: **2052/2052
verdes** (incluye los tres archivos de arriba).
