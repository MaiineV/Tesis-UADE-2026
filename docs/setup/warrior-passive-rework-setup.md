# Setup — Fuerza Bruta + nueva pasiva del Warrior

> Rama `sprint04/feature/warrior-passive-rework`. Unity MCP figuraba conectado
> (`claude mcp list`) pero sus tools no estaban expuestas en la sesión que hizo
> este trabajo — por regla del proyecto (`CLAUDE.md`), el código quedó escrito
> pero el wiring de dos ScriptableObjects Odin-serializados **queda pendiente
> de aplicar a mano en el Inspector**. Sin esto, el fix de combo funciona solo
> a medias y la pasiva nueva no hace nada (queda con `Hooks: []`).

## 1. Sacar `Combo_HighNumber` del Warrior

`Assets/Rollgeon/Classes/CH_Warrior.asset` todavía referencia dos combos de
"número alto": `Combo_FuerzaBruta` (ya fixeado, ver `Combo_FuerzaBruta.cs`) y
`Combo_HighNumber` (un clon de `Combo_SumaX` con `X=4`, base `10` — el "viene
de 10" que mencionó Santi). Con Fuerza Bruta corregido, tener los dos pisa la
prioridad y confunde. Se decidió sacar `Combo_HighNumber` del Warrior (no
borrar el asset — puede servir para otra clase).

Pasos:
1. Seleccionar `Assets/Rollgeon/Classes/CH_Warrior.asset` en el Project.
2. En el Inspector, bajo `Sheet` → `Combos`, quitar la entrada
   `Combo_HighNumber` de la lista (queda con 8: Par, DoblePar, Trio, Escalera,
   FullHouse, Poker, Generala, FuerzaBruta).
3. Bajo `Sheet` → `ShieldBaseTable`, quitar la fila con `ComboId: combo.higher_number`.
4. Guardar (Ctrl+S).

## 2. Wirear la pasiva nueva en `CP_Warrior.asset`

`Assets/Rollgeon/Classes/CP_Warrior.asset` ya tiene actualizados `PassiveId`,
`DisplayName` y `Description` (texto plano, se editó directo). Falta agregar
el `Hooks` real — antes estaba vacío (`Hooks: []`), por eso el heal-on-turn
nunca hizo nada.

Pasos:
1. Seleccionar `Assets/Rollgeon/Classes/CP_Warrior.asset`.
2. En `Hooks`, agregar un elemento nuevo:
   - **Trigger Event**: `OnAttributeChanged`.
   - **Effect** → dentro de `Effects` (lista polimórfica), agregar
     `EffLowHpAttackBuff` (nuevo effect en
     `Assets/Scripts/Rollgeon/Effects/Concretes/EffLowHpAttackBuff.cs`).
     - `Hp Threshold`: 3 (default, no hace falta tocar).
     - `Attack Bonus`: 5 (default, no hace falta tocar).
3. Guardar (Ctrl+S).

## 3. Badge "pasiva activa" en el HUD de combate

Nuevo `PassiveBadgeView` (`Assets/Scripts/Rollgeon/UI/HUD/PassiveBadgeView.cs`)
para que se vea en juego cuando el buff de HP bajo está prendido — pedido del
usuario. El componente C# está listo; falta crear el GameObject del badge en
el prefab del HUD y cablearlo.

Pasos:
1. Abrir `Assets/Prefabs/UI/Canvas.prefab` (o la escena `Assets/Scenes/02_Gameplay.unity`
   si el Combat HUD vive ahí directo) y ubicar el `HealthBarView` del jugador.
2. Crear un GameObject chico al lado (ej. un `TextMeshProUGUI` con el texto
   "Pasiva activa" o vacío — `PassiveBadgeView.ResolvePassiveLabel` le pone el
   `DisplayName` de la pasiva del hero actual si el campo `_text` está
   asignado). Dejarlo desactivado por default (arranca oculto).
3. Agregar el componente `Passive Badge View` (menú `Rollgeon/UI/HUD/Passive
   Badge View`) a ese GameObject o a un padre — el campo `_container` debe
   apuntar al GameObject que se prende/apaga (puede ser el mismo GameObject
   del texto), `_text` opcional apunta al `TextMeshProUGUI`.
4. En el `CombatHUDView` de la escena/prefab, arrastrar ese componente al
   campo nuevo `_passiveBadge` (al lado de `_shieldBar` en el Inspector).
5. Guardar (Ctrl+S).

Si no se hace este paso, el badge simplemente no aparece — no rompe nada más
(el campo es opcional, `CombatHUDView` chequea null antes de bindear).

### 3.1 Overlay de debug (mientras el badge real no esté wireado)

`Assets/Scripts/Rollgeon/UI/HUD/PassiveActiveDebugOverlay.cs` es un
componente que se auto-crea solo al arrancar el juego (`RuntimeInitializeOnLoadMethod`,
cero wiring, cero prefab) y dibuja un cartel amarillo "PASIVA ACTIVA - Furia
del Guerrero" con `OnGUI` mientras el buff de HP bajo esté prendido en
cualquier entidad. Sirve para testear el feature ya mismo, sin esperar el
paso 3. **Borrarlo** una vez que el badge real esté wireado en el HUD (o
dejarlo, es inofensivo, pero es redundante).

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
6. Con el paso 3 aplicado: bajar la vida a 3 o menos → el badge aparece al
   lado de la vida. Curarse a 4+ → el badge desaparece. Reabrir el Combat HUD
   a mitad de combate con el buff ya activo → el badge aparece de entrada
   (no hace falta esperar el próximo cambio de HP).

## Tests automáticos (ya corren sin necesitar este setup)

- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectionTests.cs` →
  `Combo_FuerzaBruta_Tests` (reescrita para "todos los 5 dados").
- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectFlatBaseOverrideTests.cs`
  (casos de Fuerza Bruta actualizados al mismo criterio).
- `Assets/Scripts/Rollgeon/Effects/Tests/EffLowHpAttackBuffTests.cs` (incluye
  `IsActiveFor_TrueWhileBuffed_FalseAfterHeal`, el helper que usa el badge).

Correr EditMode desde el Test Runner de Unity (`Window → General → Test
Runner`) — no se pudieron correr desde esta sesión porque el MCP de Unity no
tenía sus tools expuestas.
