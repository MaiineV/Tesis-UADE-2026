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

## Tests automáticos (ya corren sin necesitar este setup)

- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectionTests.cs` →
  `Combo_FuerzaBruta_Tests` (reescrita para "todos los 5 dados").
- `Assets/Scripts/Rollgeon/Combos/Tests/ComboDetectFlatBaseOverrideTests.cs`
  (casos de Fuerza Bruta actualizados al mismo criterio).
- `Assets/Scripts/Rollgeon/Effects/Tests/EffLowHpAttackBuffTests.cs` (nuevo).

Correr EditMode desde el Test Runner de Unity (`Window → General → Test
Runner`) — no se pudieron correr desde esta sesión porque el MCP de Unity no
tenía sus tools expuestas.
