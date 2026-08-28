---
name: passive-item
description: "Crea un item pasivo de Rollgeon conversando: hace las preguntas, descubre combos/eventos/efectos en runtime y arma el item completo (identidad + catalogo + precio + localizacion + hook + efectos) por Unity MCP. Usar para: nuevo item pasivo, crear item, item que haga X cuando Y."
category: "gamedev"
argument-hint: "[idea suelta del item, opcional]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash, AskUserQuestion, mcp__UnityMCP__execute_code, mcp__UnityMCP__read_console
---

# passive-item — alta conversada de items pasivos

Convierte una idea suelta ("un item que cuando saco generala me cure") en un `ItemSO`
funcional dentro de Unity, sin abrir el editor a mano.

## Regla que no se negocia

**Nunca escribas ni edites el archivo `.asset`.** `ItemSO` es un `SerializedScriptableObject`
de Odin: el stream de `SerializationNodes` renumera indices de tipo por orden de aparicion y
un edit manual (Write, Edit, sed, `manage_scriptable_object`) lo desincroniza **en silencio**.
El repo ya se comio esto dos veces — por eso existen `EgoistaComboBonusReauthorTool` y
`AfiladoFaceFilterFixTool`. Ver `docs/tools/item-editor-spec.md` §6.7.

El unico camino valido es **`mcp__UnityMCP__execute_code`**: C# que corre dentro del Editor,
arma los objetos en memoria con la API real y deja que Odin re-serialice al guardar.

Tampoco crees scripts nuevos bajo `Assets/Scripts/` para esto. Todo va inline por `execute_code`.

## Gotchas de `execute_code` (leer antes de escribir C#)

- El codigo corre **como cuerpo de metodo**: no se pueden poner `using` arriba. Usa **nombres
  totalmente calificados** siempre (`Rollgeon.Items.ItemSO`, `Rollgeon.Effects.Concretes.EffHeal`,
  `Rollgeon.Editor.Tools.Item.ItemAuthoring`, ...).
- Devolve datos con `return` (string). Para listas, armá un string con `string.Join`.
- `compiler: "auto"` esta bien. Si el snippet usa sintaxis moderna y falla a compilar,
  reintentá con `compiler: "roslyn"`.
- Si un `execute_code` devuelve error de compilacion, **no lo parchees a ciegas**: releé el
  tipo real con Grep en `Assets/Scripts/Rollgeon/` y corregí el nombre.

---

## Paso 0 — Preflight

1. Verificá que el MCP de Unity este conectado (`claude mcp list`, o un `execute_code` trivial
   `return UnityEditor.EditorApplication.isPlaying.ToString();`).
   **Si no responde, frená y avisá al usuario** — no asumas que nada se aplico.
2. Si el Editor esta en Play Mode, pedí que salga: crear assets en Play es una fuente de perdida
   de trabajo.

## Paso 1 — Descubrimiento (nunca hardcodear las opciones)

Los ids de combo, los `EventName` y los efectos concretos cambian con el proyecto. Descubrilos
en la sesion con **un solo** `execute_code` y ofrecé esas listas al usuario:

```csharp
var sb = new System.Text.StringBuilder();

sb.AppendLine("COMBOS:");
foreach (var id in Rollgeon.Combos.BaseComboSO.GetKnownComboIds())
    sb.AppendLine("  " + id);

sb.AppendLine("EVENTOS (enum EventName completo):");
foreach (var n in System.Enum.GetNames(typeof(EventName)))
    sb.AppendLine("  " + n);

sb.AppendLine("EFECTOS (IEffect concretos):");
sb.AppendLine("PRECONDICIONES (BasePreCondition concretas):");
sb.AppendLine("READERS (EffectIntReader concretos):");
var eff = typeof(Rollgeon.Effects.IEffect);
var pre = typeof(Rollgeon.PreConditions.BasePreCondition);
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    System.Type[] types;
    try { types = asm.GetTypes(); } catch { continue; }
    foreach (var t in types)
    {
        if (t.IsAbstract || t.IsInterface) continue;
        if (eff.IsAssignableFrom(t)) sb.AppendLine("  EFF " + t.FullName);
        else if (pre.IsAssignableFrom(t)) sb.AppendLine("  PRE " + t.FullName);
        else if (t.BaseType != null && t.BaseType.Name.Contains("EffectIntReader"))
            sb.AppendLine("  RDR " + t.FullName);
    }
}
return sb.ToString();
```

Si `EventName` no resuelve sin calificar, buscá su namespace con Grep antes de reintentar.

> **Los eventos usables son un subconjunto del enum.** El `InfoBox` de `PassiveItemHook` lista
> los que hoy disparan bien: `OnTurnStarted`, `OnTurnFinished`, `OnRollStarted`, `OnDiceRolled`,
> `OnRollResolved`, `OnDamageIncoming`, `OnDamageOutgoing`, `OnComboCrossed`, `OnWeaknessHit`,
> `OnPlayerHealthChanged`. Ofrecé esos primero y marcá el resto como "no verificado".

Para ver como se autora un efecto real, mirá items existentes en vez de inventar campos:

```csharp
var item = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Items.ItemSO>(
    "Assets/Rollgeon/Items/Item_Egoista.asset");
return Rollgeon.Editor.Tools.Item.ItemQuery.GetEffectTypes(item).Count.ToString();
```

## Paso 2 — Las preguntas, en este orden

Usá `AskUserQuestion`, **una decision por vez**, con las opciones descubiertas en el Paso 1.
Si el usuario ya dio la respuesta en su mensaje inicial, no la vuelvas a preguntar: confirmala
al pasar. Si el usuario dice "elegí vos", proponé un default razonable y seguí.

1. **Idea / fantasia** — que quiere que haga el item. Pregunta abierta, solo si arranco de cero.
2. **Cuando se dispara** — `ComboPlayed` (al jugar un combo) o `EventBus` (un evento del bus).
   Explicá la diferencia en una linea: ComboPlayed corre **pre-daño**, dentro de la ventana del
   golpe; EventBus corre cuando el bus lo emite y para bonos de daño suele llegar tarde.
3. **Filtro**:
   - Si `ComboPlayed`: ¿que combos? `AnyCombo` o lista de ids (mostrá los descubiertos).
   - Si `EventBus`: ¿que `EventName`?
4. **`ActionKindFilter`** (solo `ComboPlayed`): `Attack`, `Heal`, `Movement`, ... o `Unknown`
   para no restringir. **Default recomendado: `Attack` para cualquier bono de daño** — Heal y
   Movement comparten el mismo play scratch y el bono leakea (BUG-060/BUG-080).
5. **Efecto** — que hace. Ofrecé los `IEffect` descubiertos, agrupados por intencion.
   **Preguntá explicitamente el matiz de daño:**
   - `EffAddComboBonus` → **suma al daño del combo**, o sea **se multiplica con el combo**.
   - `EffDealDamage` → **golpe aparte**, no escala con el combo.
   El usuario casi siempre quiere el primero. No lo elijas por el: preguntá cual de los dos.
6. **Magnitud** — valor fijo o un `EffectIntReader` (ej. `ReadCurrentGold`,
   `ReadCurrentGoldSqrtScaled`, `ReadComboCounter`). Si es escalado, pedí el factor.
7. **Condiciones** (`PreConditions`, se evaluan en **AND**) — opcional. Ej. `PcOwnerHpBelow`,
   `PcChance`, `PcGoldCompare`. Default: ninguna.
8. **`PersistentModifiers`** — modificadores de stat mientras se tenga el item. Opcional,
   default ninguno. Si hay: stat, `ModifierOperation`, `Amount`, `ModifierDirection`.
9. **Identidad**: `DisplayName` (es) y `Description` (es).
10. **`DisplayNameEn` + `DescriptionEn`** — **obligatorio preguntarlos.** El proyecto valida por
    test que toda clave de localizacion tenga valor en **los dos idiomas y que difieran**. Si los
    dejas vacios, la suite queda roja. Si el usuario no quiere traducir, avisale que el test
    falla hasta que alguien lo complete.
11. **`Rarity`**: `Common | Uncommon | Rare | Legendary | God`.
12. **Precio**: `null` = se deriva de la rareza (default). Solo preguntá si quiere override.
13. **Familia** (opcional): `FamilyId` + `VariantIndex` si es variante de una familia existente.
    Si el usuario quiere varias variantes de una, usá `ItemAuthoring.CreateFamily` en vez de
    `CreateItem`.
14. **Icono**: opcional. Si el usuario da un path de sprite, cargalo; si no, queda `null`.

Antes de escribir nada, **mostrá un resumen de la especificacion completa y pedí confirmacion**.

## Paso 3 — Chequeo de id

El id se **deriva del `DisplayName` y se congela al crear**: es clave de save, renombrarlo
rompe partidas guardadas. Verificá disponibilidad antes de crear:

```csharp
Rollgeon.Items.ItemSO owner;
var free = Rollgeon.Editor.Tools.Item.ItemAuthoring.IsIdAvailable("item_mi_id", out owner);
return free ? "LIBRE" : "OCUPADO por " + owner.name;
```

Si esta ocupado, pedí otro nombre. No inventes sufijos vos.

## Paso 4 (escritura A) — Crear la identidad

`ItemAuthoring.CreateItem` hace **en un solo paso de undo**: crea el `.asset`, lo registra en el
`ItemCatalog`, le pone precio en el `ShopPool` y escribe localizacion es+en. Valida antes de
escribir: si falla, no toca nada.

```csharp
var spec = new Rollgeon.Editor.Tools.Item.ItemCreationSpec
{
    DisplayName    = "Piedra Sangrienta",
    Description    = "Al jugar una Generala, curás 4 PV.",
    DisplayNameEn  = "Bloodstone",
    DescriptionEn  = "When you play a Generala, heal 4 HP.",
    Icon           = null,
    Rarity         = Rollgeon.Items.ItemRarity.Rare,
    Type           = Rollgeon.Items.ItemType.Passive,
    FamilyId       = null,
    VariantIndex   = null,
    BasePrice      = null,   // null = deriva de la rareza
    TargetFolder   = null,   // null = Assets/Rollgeon/Items

    // El CUANDO. Id de ItemTriggerCatalog, no un EventName crudo — ver "Elegir el disparador".
    TriggerId      = "combo.ids",
    TriggerComboIds = new System.Collections.Generic.List<string> { "combo.generala" },
};

var r = Rollgeon.Editor.Tools.Item.ItemAuthoring.CreateItem(spec);
if (!r.Success) return "FAIL: " + string.Join(" | ", r.Errors);
return "OK id=" + r.ItemId + " path=" + r.AssetPath;
```

**Si `Success` es false, frená y reportá los `Errors` al usuario.** No sigas al Paso 5.

### Elegir el disparador

**Nunca escribas un `EventName` a mano.** `PassiveItemHook.TriggerEvent` es del tipo `EventName`,
o sea el bus entero del juego: mas de cien miembros, de los que sirven ~una docena. Elegir uno que
no sirve **no da error**: el item simplemente no dispara nunca. Y hay trampas que el nombre no
delata — `OnCombatStart` lleva el id de la SALA en `args[0]`, `OnComboCrossed` manda `Guid.Empty`,
`OnPlayerHealthChanged` no lo emite nadie en produccion. Dos items del catalogo estan rotos hoy
exactamente por esto.

`ItemTriggerCatalog` es la lista curada de lo que si funciona. Leela antes de decidir:

```csharp
var sb = new System.Text.StringBuilder();
foreach (var o in Rollgeon.Editor.Tools.Item.ItemTriggerCatalog.All)
    sb.Append(o.Id).Append(" | ").Append(o.DisplayName).Append(" | ").Append(o.Help).AppendLine();
return sb.ToString();
```

- `TriggerId` vacio = el item nace sin hooks (comportamiento viejo).
- Un `TriggerId` que no esta en el catalogo **falla la creacion**, no crea un item mudo.
- `TriggerComboIds` solo aplica a la opcion que pide combos (`UsesComboIds`).
- Solo para `ItemType.Passive`. En un Activo es error.

**`Subject` distingue pegar de que te peguen.** `OnDamageIncoming` se dispara como
`[quienPega, quienRecibe, danio]` y el hook compara al jugador contra `args[0]` por defecto, asi
que colgarse de ese evento sin mas dispara **al pegar**. El catalogo tiene las dos entradas
separadas (`damage.dealt.final` y `damage.taken`) y pone el `Subject` correcto. Si lo autoras a
mano, `PassiveHookSubject.Target` es "cuando te pegan".

## Paso 5 (escritura B) — Autorar hook y efectos

Con `TriggerId`, `CreateItem` deja el hook armado pero **sin efectos**: ya sabe cuando dispara y
todavia no hace nada. Este segundo `execute_code` le pone los efectos. Patron de referencia:
`Assets/Scripts/Editor/Tools/Item/EgoistaComboBonusReauthorTool.cs` (79 lineas, hace exactamente
esto).

```csharp
var path = "Assets/Rollgeon/Items/Item_Piedra_Sangrienta.asset";  // el AssetPath del Paso 4
var item = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Items.ItemSO>(path);
if (item == null) return "FAIL: no se encontro " + path;

// El hook ya existe con su disparador puesto por TriggerId: solo se le agregan los efectos.
// Si el item se creo SIN TriggerId, armá el hook con ItemTriggerCatalog.Apply en vez de
// escribir Kind/TriggerEvent/Subject a mano.
var hook = item.PassiveHooks[0];
hook.ActionKindFilter = Rollgeon.Combat.Rolls.RollActionKind.Attack;  // Unknown = sin restriccion
hook.Effect = new Rollgeon.Effects.EffectData
    {
        Label = "Effect Group",
        PreConditions = new System.Collections.Generic.List<Rollgeon.PreConditions.BasePreCondition>(),
        Effects = new System.Collections.Generic.List<Rollgeon.Effects.IEffect>
        {
            new Rollgeon.Effects.Concretes.EffAddComboBonus
            {
                Amount = new Rollgeon.Upgrades.Dice.Readers.ReadCurrentGoldSqrtScaled { Factor = 5f },
            },
        },
        TargetSelector = null,
    };

UnityEditor.EditorUtility.SetDirty(item);
UnityEditor.AssetDatabase.SaveAssets();
return "OK hooks=" + item.PassiveHooks.Count;
```

Notas:
- El namespace de `ComboFilter` es `Rollgeon.Upgrades.Dice` (vive en `Upgrades/Dice/Resources/`).
  Si no compila, confirmalo con Grep — no adivines.
- `item.PassiveHooks = ...` **pisa la lista entera**. Para agregar a un item existente, leé la
  lista actual y hacé `Add` en vez de reasignar.
- `SetDirty` + `SaveAssets` son obligatorios: sin eso Odin no re-serializa y el cambio se pierde
  al recargar el dominio.
- Los campos exactos de cada `IEffect` no estan hardcodeados aca. Antes de escribirlos, leé el
  archivo del efecto en `Assets/Scripts/Rollgeon/Effects/Concretes/`. Varios usan propiedades
  con backing field `[OdinSerialize, SerializeReference]` — si una propiedad no tiene setter
  publico, asignala por reflexion al campo privado o elegí otro efecto.

## Paso 6 — Verificar y reportar

1. `mcp__UnityMCP__read_console` para confirmar que no quedaron errores de compilacion ni
   excepciones.
2. Releé el item y reportá lo que quedo:

```csharp
var it = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Items.ItemSO>(path);
var sb = new System.Text.StringBuilder("hooks=" + it.PassiveHooks.Count + " efectos=");
foreach (var t in Rollgeon.Editor.Tools.Item.ItemQuery.GetEffectTypes(it))
    sb.Append(t.Name).Append(' ');
return sb.ToString();
```

3. Cerrá con estas advertencias, textuales:
   - **Ctrl+Z no borra el `.asset`.** Un undo despues de crear revierte catalogo, precio y
     localizacion, pero el archivo queda en disco (limitacion de Unity, spec §7.1). Si el
     usuario quiere deshacer, el camino correcto es `ItemAuthoring.DeleteItem(item)`, no Ctrl+Z.
     Un item huerfano aparece luego como hallazgo en `ItemQuery.CheckCatalogHealth`.
   - **El id esta congelado.** Se derivo del nombre y es clave de save. Cambiarlo mas adelante
     (`RenameItemId`) rompe compatibilidad de partidas guardadas.
   - Si la localizacion en ingles quedo vacia o igual a la castellana, **la suite de tests queda
     roja** hasta que se complete.
   - El item no esta commiteado. **No commitees vos** salvo que el usuario lo pida.

## Cuando NO usar esta skill

- Item **activo** (`ItemType.Active`): el flujo de identidad sirve, pero los hooks pasivos no son
  el canal correcto. Avisá y pará.
- Editar un item ya existente: se puede reautorar con el Paso 5, pero confirmá primero con el
  usuario que pisar `PassiveHooks` es lo que quiere.
- Varias variantes de una familia: usá `ItemAuthoring.CreateFamily` con `ItemFamilyCreationSpec`.

Detalles de dominio (semantica de cada efecto, readers, precondiciones, errores conocidos):
`references/domain-cheatsheet.md`.
