# Item List — Propuesta de contenido nuevo

> **Origen:** pedido de Sebastián (sprint04, 2026-07-14) — "hacerme listado de
> items". **Estado:** propuesta para balancear/aprobar, nada de esto está
> implementado todavía. **Autores:** Sebastián + Claude.
> **Relacionado:** `docs/balance/item-inventory.html` §4 (inventario real
> actual — generado leyendo assets, sin valores inventados).

---

## Punto de partida

Hoy el pool de items reales tiene **un solo item cargado**:
`Item_HealingPotion` (`potion.healing`, Common, Active). Además existen 3
assets huérfanos (`D20Die`, `D20DieEnchantmentPlus`, `D20DieEnhancent`) —
mismo `ItemId="Item01"` los tres, ninguno en `ShopPool.asset`, restos de
experimentación (ver nota en `item-inventory.html`). En la práctica: **el
sistema de items existe y funciona, pero casi no tiene contenido.**

Esta propuesta usa **solo building blocks que ya existen en el código**
(`ItemSO`, `PassiveItemHook`, `EffectData`/`IEffect` concretos, valores reales
de `EventName` y `StatType`) para que sea implementable directo en el
Inspector sin escribir C# nuevo. Cada fila cita la clase/enum real que hay
que usar.

## Cómo leer las tablas

- **ItemId**: sugerido, formato `item.<slug>` (los existentes usan
  `potion.healing` — sin prefijo `item.`; seguí el patrón que prefieras).
- **Efecto**: el/los `IEffect` concretos y sus parámetros clave. Todos los
  valores numéricos son **placeholders de arranque**, no balance final.
- **Trigger** (solo pasivos): valor real de `Rollgeon.Patterns.EventName`
  (`Assets/Scripts/Rollgeon/Patterns/EventName.cs`) — no inventé eventos que
  no existen en el enum.

---

## Items Activos (`ItemType.Active`)

| ItemId | Nombre | Rareza | Cooldown | Consumed­OnUse | Consumes­Action | Efecto (`OnActivate`) | Nota |
|---|---|---|---|---|---|---|---|
| `item.bandage` | Vendaje | Common | 0 | true | false | `EffHeal`: `_baseAmount=5`, `_useDiceRoll=false` | A propósito **no** usa dice roll — evita el bug ya documentado en `Item_HealingPotion` donde `_useDiceRoll=true` descarta `_baseAmount` por completo. Cura chica y predecible, contraste con la Potion (grande y random). |
| `item.throwing_knife` | Cuchillo Arrojadizo | Common | 1 turno | false | true | `EffDealDamage`: `_damageSource=Constant`, `_baseAmount=6`, `_attackKind=BasicAttack` | Poke barato de rango 1 sin gastar el ataque base del slot — pensado como utilidad, no como daño principal. |
| `item.emergency_shield` | Escudo de Emergencia | Uncommon | 3 turnos | false | true | `EffAddShield`: `_shieldSource=Constant`, `_baseAmount=6` | Mismo effect que usa el `Base Attack`/`Special Attack` del warrior (`EffAddShield`), pero como acción propia sin depender de combo. Recordar el cap duro `ESCUDO_CAP=8` (memoria del proyecto) al balancear el número. |
| `item.adrenaline` | Adrenalina | Rare | 0 | true | false | `EffModifyIntAttribute`: `TargetStat=Energy`, `Operation=Add`, `_baseAmount=2` | Consumible de un solo uso, no gasta acción del turno (como la Potion) — devuelve 2 de energía para des-trabar un turno con mal roll. Riesgo de balance: revisar contra el pool de energía típico antes de aprobar. |

## Items Pasivos (`ItemType.Passive`)

Cada fila es un `PassiveItemHook` (o dos, si el item necesita más de un
trigger). `Effect` vive en `PassiveItemHook.Effect`; los modificadores
persistentes (`PersistentModifierDef`, mismo patrón que `CharacterRewardSO`)
van en `PassiveItemHook.PersistentModifiers`.

| ItemId | Nombre | Rareza | Trigger (`EventName`) | Efecto / Modifiers | Nota |
|---|---|---|---|---|---|
| `item.iron_will` | Voluntad de Hierro | Legendary | `OnRunStart` | `PersistentModifiers`: `Health +15` (`Operation=Add`, `Direction=Intrinsic`) | Dispara una sola vez al arrancar la run, sin `Effect` propio — mismo patrón que `char_rew.hp_plus_5` pero como item de inventario en vez de reward de boss. |
| `item.warriors_charm` | Amuleto del Guerrero | Uncommon | `OnRunStart` | `PersistentModifiers`: `Attack +2` | Flat, siempre activo mientras el item esté en inventario (se remueve automático si se pierde, per doc de `ItemSO`). |
| `item.glass_cannon` | Cañón de Cristal | Legendary | `OnRunStart` | `PersistentModifiers`: `Attack +5`, `Health -5` | Build-defining, alto riesgo/recompensa — hoy no hay ningún item/reward con downside, esto llenaría ese hueco de diseño (ver el análisis de risk/reward en `docs/design/pas-defensa-pura.md` para la misma lógica de identidad push-your-luck). |
| `item.spiked_armor` | Armadura con Púas | Rare | `OnDamageIncoming` | `EffDealDamage` de vuelta al atacante: `_baseAmount=3` | **Verificar antes de implementar**: hay que confirmar que el `EffectContext` en `OnDamageIncoming` resuelve el `sourceGuid` del ataque como `TargetGuid` del efecto de contraataque — no lo confirmé en código, es el único punto de esta lista que necesita chequeo previo. |
| `item.momentum` | Impulso | Uncommon | `OnRoomCleared` | `EffModifyIntAttribute`: `TargetStat=Energy`, `Operation=Add`, `_baseAmount=1` | Recompensa pequeña por limpiar cuartos, se siente en runs largas. |

---

## Lo que quedó afuera (y por qué)

- **Nada que dé oro directo.** `EffModifyIntAttribute` solo toca
  `Health/Attack/Speed/Energy/Shield/HealStrength` (ver
  `Assets/Scripts/Rollgeon/Attributes/Stats/`) — Gold se maneja aparte, vía
  `ModifyResourceTrigger` (exclusivo de encantamientos, no está expuesto a
  `ItemSO`/`PassiveItemHook`). Si querés un item "da oro", hace falta un
  effect nuevo o extender `PassiveItemHook` — eso sí es trabajo de código,
  avisame si lo querés.
- **Nada que toque `HealStrength`, `Speed` puro, ni combos.** Dejé esos ejes
  libres a propósito por si Maiine/Bocco ya tienen ideas para esas ramas —
  no quise ocupar todo el espacio de diseño de una.

## Próximos pasos

1. Vos (Sebastián): revisar/podar/re-balancear los números — son
   placeholders de arranque, no un pase de balance real.
2. Confirmar `item.spiked_armor` (el único con duda técnica) antes de
   cargarlo en Unity.
3. Cuando haya luz verde: puedo crear los `ItemSO.asset` vía Unity MCP
   (mismo patrón que usé para `CP_Warrior`/`ServiceBootstrap` esta sesión) —
   avisame cuáles aprobás y en qué orden.
