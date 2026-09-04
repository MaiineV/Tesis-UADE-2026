---
name: enchantment
description: "Crea encantamientos de dados de Rollgeon por Unity MCP — armando el encantamiento completo (identidad + categoria + catalogo + pool + localizacion + disparador + efectos). Funciona de dos maneras: contale la idea y lo construye, o pedile que te guie con preguntas. Usar para: nuevo encantamiento, crear encantamiento, encantamiento que haga X cuando Y, encantar dados."
category: "gamedev"
argument-hint: "[idea suelta del encantamiento, opcional]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash, Edit, AskUserQuestion, mcp__UnityMCP__execute_code, mcp__UnityMCP__read_console
---

# enchantment — alta conversada de encantamientos de dados

Convierte una idea suelta ("un encantamiento que dé oro cuando el dado entra en una escalera")
en un `EnchantmentSO` funcional dentro de Unity, sin abrir el editor a mano. Espejo de
`/passive-item` para el canal dados.

## Mantener esta skill al dia

**Si tocas el dominio de encantamientos, esta skill se actualiza en el mismo cambio.** Agregar,
renombrar o cambiar de campos un `IFaceFilter`, una `IEnchantmentCapability`, un miembro de
`EnchantmentHookEvent`, un `IEffect` usable en el canal scratch o una entrada de
`EnchantmentTriggerCatalog` deja desactualizado lo que esta skill le dice al proximo agente, y el
sintoma no es un error: es un encantamiento mal armado que nadie revisa.

| Cambiaste | Actualiza |
|---|---|
| Un `IFaceFilter` concreto (campos, semantica) | La tabla de filtros de `references/domain-cheatsheet.md` |
| Una `IEnchantmentCapability` (o su `[NotYetWired]`) | La tabla de capabilities del cheatsheet |
| `EnchantmentHookEvent` o `ExecuteEffectsOnDiceEvent` | "El catalogo de disparadores" del cheatsheet |
| `EnchantmentTriggerCatalog.All` | "Elegir el disparador" en este archivo y la tabla del cheatsheet |
| `EnchantmentCreationSpec` | El Paso 4 de este archivo |
| `EnchantmentCategory` / taxonomia GDD | El Paso 2 y la tabla de categorias del cheatsheet |

El Paso 1 descubre los tipos **en runtime** justamente para que un olvido no rompa el alta: la
lista de filtros/capabilities/efectos siempre sale del proyecto, nunca de este texto. Lo que si
envejece es la **semantica** — cual conviene, cual es farmeable, cual no esta cableada — y eso
es lo que hay que mantener a mano.

## Regla que no se negocia

**Nunca escribas ni edites el archivo `.asset`.** `EnchantmentSO` hereda de
`SerializedScriptableObject` (Odin): el stream de `SerializationNodes` renumera indices de tipo
por orden de aparicion y un edit manual (Write, Edit, sed, `manage_scriptable_object`) lo
desincroniza **en silencio**. El repo ya se comio esto dos veces con items — por eso existen
`EgoistaComboBonusReauthorTool` y `AfiladoFaceFilterFixTool`. Ver `docs/tools/item-editor-spec.md` §6.7.

El unico camino valido es **`mcp__UnityMCP__execute_code`**: C# que corre dentro del Editor,
arma los objetos en memoria con la API real y deja que Odin re-serialice al guardar.

Tampoco crees scripts nuevos bajo `Assets/Scripts/` para esto. Todo va inline por `execute_code`.

## Gotchas de `execute_code` (leer antes de escribir C#)

- El codigo corre **como cuerpo de metodo**: no se pueden poner `using` arriba. Usa **nombres
  totalmente calificados** siempre (`Rollgeon.Upgrades.Dice.EnchantmentSO`,
  `Rollgeon.Editor.Tools.Enchantment.EnchantmentAuthoring`,
  `Rollgeon.Effects.Concretes.EffAddComboBonus`, ...).
- Devolve datos con `return` (string). Para listas, armá un string con `string.Join`.
- `compiler: "auto"` esta bien. Si el snippet usa sintaxis moderna y falla a compilar,
  reintentá con `compiler: "roslyn"`.
- Si un `execute_code` devuelve error de compilacion, **no lo parchees a ciegas**: releé el
  tipo real con Grep en `Assets/Scripts/Rollgeon/Upgrades/Dice/` y corregí el nombre.

---

## Paso 0 — Preflight

1. Verificá que el MCP de Unity este conectado (`claude mcp list`, o un `execute_code` trivial
   `return UnityEditor.EditorApplication.isPlaying.ToString();`).
   **Si no responde, frená y avisá al usuario** — no asumas que nada se aplico.
2. Si el Editor esta en Play Mode, pedí que salga: crear assets en Play es una fuente de perdida
   de trabajo.

## Paso 0.5 — Elegí el modo

Hay dos formas de usar esto y la elige el usuario, no vos.

**Modo directo — "te cuento la idea y armalo".** El usuario describe el encantamiento en sus
palabras ("uno que dé escudo cuando el dado saca su máximo") y vos lo construis entero. Reglas:

- Completá con defaults todo lo que no dijo: peso del pool 1, `MinFloorDepth` 0, todos los tipos
  de dado, sin filtro de caras, sin capabilities, sin icono.
- **Preguntá solo lo que cambia el encantamiento de verdad** y no podes deducir. Tipicamente: la
  categoria GDD si la idea es ambigua, el matiz preview/apply (Paso 2.5), y el ingles si pensas
  dejarlo sin traducir.
- No preguntes de a una cosa por mensaje: juntá las dudas en un solo `AskUserQuestion`.
- **Siempre mostrá el resumen de la especificacion y esperá confirmacion antes de escribir.**

**Modo guiado — "hacéme las preguntas".** Recorrés el Paso 2 completo, una decision por vez, con
las opciones descubiertas en el Paso 1.

**Como elegir:** si el mensaje inicial ya trae una idea concreta, ofrecé el modo directo
resumiendo lo que entendiste. Si es vago ("quiero hacer un encantamiento"), andá al guiado.
Ante la duda, preguntá cual prefiere con `AskUserQuestion` — es una sola pregunta.

## Paso 1 — Descubrimiento (nunca hardcodear las opciones)

Los ids de combo, los filtros de caras, las capabilities y los efectos concretos cambian con el
proyecto. Descubrilos en la sesion con **un solo** `execute_code` y ofrecé esas listas:

```csharp
var sb = new System.Text.StringBuilder();

sb.AppendLine("COMBOS:");
foreach (var id in Rollgeon.Combos.BaseComboSO.GetKnownComboIds())
    sb.AppendLine("  " + id);

sb.AppendLine("DISPARADORES (EnchantmentTriggerCatalog):");
foreach (var o in Rollgeon.Editor.Tools.Enchantment.EnchantmentTriggerCatalog.All)
    sb.AppendLine("  " + o.Id + " | " + o.DisplayName + " | scratchOnly=" + o.ScratchOnly + " | " + o.Help);

sb.AppendLine("FILTROS DE CARAS / CAPABILITIES / EFECTOS / PRECONDICIONES / READERS:");
var filter = typeof(Rollgeon.Upgrades.Dice.IFaceFilter);
var cap = typeof(Rollgeon.Upgrades.Dice.IEnchantmentCapability);
var eff = typeof(Rollgeon.Effects.IEffect);
var pre = typeof(Rollgeon.PreConditions.BasePreCondition);
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    System.Type[] types;
    try { types = asm.GetTypes(); } catch { continue; }
    foreach (var t in types)
    {
        if (t.IsAbstract || t.IsInterface) continue;
        var wired = t.GetCustomAttributes(typeof(Rollgeon.Attributes.NotYetWiredAttribute), true).Length == 0
            ? "" : "  [NO CABLEADA]";
        if (filter.IsAssignableFrom(t)) sb.AppendLine("  FLT " + t.FullName);
        else if (cap.IsAssignableFrom(t)) sb.AppendLine("  CAP " + t.FullName + wired);
        else if (eff.IsAssignableFrom(t)) sb.AppendLine("  EFF " + t.FullName);
        else if (pre.IsAssignableFrom(t)) sb.AppendLine("  PRE " + t.FullName);
        else if (t.BaseType != null && t.BaseType.Name.Contains("EffectIntReader"))
            sb.AppendLine("  RDR " + t.FullName);
    }
}
return sb.ToString();
```

> **Las capabilities `[NO CABLEADA]` compilan y se configuran pero no hacen nada in-game.**
> No diseñes contenido nuevo sobre ellas — el encantamiento va a parecer roto en playtest y
> nadie va a saber por que. Si el usuario pide una, avisale y ofrecé una alternativa.

Para ver como se autora un efecto real, mirá encantamientos existentes en vez de inventar campos:

```csharp
var e = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Upgrades.Dice.EnchantmentSO>(
    "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Avaro.asset");
return string.Join(", ", System.Linq.Enumerable.Select(
    Rollgeon.Editor.Tools.Enchantment.EnchantmentQuery.GetEffectTypes(e), t => t.Name));
```

## Paso 2 — Las preguntas, en este orden

Usá `AskUserQuestion`, **una decision por vez**, con las opciones descubiertas en el Paso 1.
Si el usuario ya dio la respuesta en su mensaje inicial, confirmala al pasar. Si dice "elegí
vos", proponé un default razonable y seguí.

1. **Idea / fantasia** — que quiere que haga. Pregunta abierta, solo si arranco de cero.
2. **Categoria GDD** — **obligatoria** (la auditoria rechaza `None` y el alta falla sin ella):
   `Caos` (efecto negativo a cambio de ganancia), `Recursos` (oro/escudo al usar el dado),
   `Ataque` (daño condicionado), `Control` (restringe caras / modifica valores / altera combos),
   `Movimiento` (SOLO el dado de Movimiento: la categoria decide el destino, el altar los ofrece
   con el set de Movimiento visible; disparador tipico `player.moved` + `ReadTilesTraversed`).
   Casi siempre se deduce de la idea: confirmala al pasar.
3. **Cuando se dispara** — ofrecé los `DisplayName` de `EnchantmentTriggerCatalog.All`, no el
   enum crudo. Un encantamiento **sin disparador tambien es valido** si vive de un filtro de
   caras o una capability (ej. Par, Primo, Caras Centrales son solo-filtro).
4. **Combos** — solo si la opcion elegida los pide (`UsesComboIds`). Mostrá los ids del Paso 1.
   **Sin ningun combo elegido el encantamiento no dispara nunca.**
5. **¿El dado tiene que participar?** (`RequireCarrierParticipates`, solo disparadores de combo)
   — "cuando ESTE dado entra en el combo" vs "cuando jugás el combo, esté o no el dado".
   **Default recomendado: sí** — y es obligatorio si los efectos van a leer la cara del
   portador (`PcCarrierFace`), la auditoria lo exige.
6. **Tipos de dado** — `AllowedDiceTypes`. Default: todos (lista vacia). El altar filtra el
   pool con esto.
7. **Filtro de caras** — opcional. Ofrecé los `IFaceFilter` del Paso 1 (tabla en el cheatsheet).
   Es "en que caras existe el encantamiento": si la cara tirada no pasa el filtro, los triggers
   no disparan.
8. **Efecto** — que hace. Ofrecé los `IEffect` descubiertos, agrupados por intencion.
   Ver **Paso 2.5**: el matiz preview/apply se decide siempre, en los dos modos.
9. **Magnitud** — valor fijo (`ReadConstantInt`) o un `EffectIntReader` (ej. `ReadCarrierFace`,
   `ReadCurrentGoldSqrtScaled`, `ReadComboCounter`). Si es escalado, pedí el factor.
10. **Condiciones** (`PreConditions`, se evaluan en **AND**) — opcional. Ej. `PcCarrierFace`
    (la cara del portador), `PcSlotCounterCompare`, `PcChance`. Default: ninguna.
11. **Identidad**: `DisplayName` (es) y `Description` (es).
12. **`DisplayNameEn` + `DescriptionEn`** — **obligatorio preguntarlos.** El proyecto valida por
    test que toda clave tenga valor en **los dos idiomas y que difieran**. Si los dejas vacios,
    la suite queda roja hasta que alguien traduzca.
13. **Peso del pool** — default 1. Es el dial de balance (no hay precio por encantamiento: el
    costo del altar es global). `0` = registrado pero deshabilitado. **`MinFloorDepth`** —
    default 0, piso minimo desde el que se ofrece.
14. **Icono**: opcional. Si el usuario da un path de sprite, cargalo; si no, queda `null`.

Antes de escribir nada, **mostrá un resumen de la especificacion completa y pedí confirmacion**.

## Paso 2.5 — El matiz preview/apply (se decide en los dos modos)

Si el efecto reacciona a combos, **elegí bien la ventana**. Es la decision que separa un
encantamiento sano de uno farmeable:

- `combo.played.*` (**ComboPlayed**, apply) → el combo se confirmo, ventana pre-daño. Aca van
  los efectos de apply directo: oro, escudo, curacion, y tambien los bonos al combo.
- `combo.matched.*` (**ComboMatched**, preview) → se detecto un combo, **re-dispara en cada
  toggle de hold**. Solo admite efectos `IComboScratchWriter` (`EffAddComboBonus` y afines).
  Un `EffModifyGold` aca es **oro infinito con hold/unhold** (BUG-017) y la auditoria lo
  rechaza.

Regla practica: **todo va en ComboPlayed salvo que necesites que el bono se vea en el preview
del daño antes de confirmar.** Ante la duda, ComboPlayed.

Y el segundo matiz, heredado de items: `EffAddComboBonus` **suma al daño del combo** (se
multiplica con el combo); `EffDealDamage` es un golpe aparte que no escala. La diferencia en un
Full House es de varias veces el numero — no lo elijas vos.

## Paso 3 — Chequeo de id

El id se **deriva del `DisplayName`** (`"Múltiplo de 3"` → `ench.multiplo_de_3`) y se **congela
al crear**: es clave de save (los slots del `RuntimeDiceBag` se restauran por id y descartan los
desconocidos), clave de localizacion y clave del gate de meta-progresion. Verificá antes:

```csharp
Rollgeon.Upgrades.Dice.EnchantmentSO owner;
var free = Rollgeon.Editor.Tools.Enchantment.EnchantmentAuthoring.IsIdAvailable(
    Rollgeon.Editor.Tools.Enchantment.EnchantmentIdSlug.FromDisplayName("Piedra Sangrienta"),
    out owner);
return free ? "LIBRE" : "OCUPADO por " + owner.name;
```

Si esta ocupado, pedí otro nombre. No inventes sufijos vos.

## Paso 4 (escritura A) — Crear la identidad

`EnchantmentAuthoring.CreateEnchantment` hace **en un solo paso de undo**: crea el `.asset`, lo
registra en el `EnchantmentCatalog`, lo agrega al pool del altar (peso + piso), escribe
localizacion es+en y setea la categoria. Valida antes de escribir: si falla, no toca nada.

```csharp
var spec = new Rollgeon.Editor.Tools.Enchantment.EnchantmentCreationSpec
{
    DisplayName    = "Piedra Sangrienta",
    Description    = "Cuando este dado participa en una Generala, curás 4 PV.",
    DisplayNameEn  = "Bloodstone",
    DescriptionEn  = "When this die joins a Generala, heal 4 HP.",
    Icon           = null,
    Category       = Rollgeon.Upgrades.Dice.EnchantmentCategory.Recursos,
    AllowedDiceTypes = null,   // null o vacio = todos los tipos de dado
    PoolWeight     = null,     // null = 1
    MinFloorDepth  = null,     // null = 0
    TargetFolder   = null,     // null = Assets/Rollgeon/Upgrades/Dice/Enchantments

    // El CUANDO. Id de EnchantmentTriggerCatalog, no el enum crudo.
    TriggerId      = "combo.played.ids",
    TriggerComboIds = new System.Collections.Generic.List<string> { "combo.generala" },
    RequireCarrierParticipates = true,
};

var r = Rollgeon.Editor.Tools.Enchantment.EnchantmentAuthoring.CreateEnchantment(spec);
if (!r.Success) return "FAIL: " + string.Join(" | ", r.Errors);
return "OK id=" + r.UpgradeId + " path=" + r.AssetPath;
```

**Si `Success` es false, frená y reportá los `Errors` al usuario.** No sigas al Paso 5.

### Elegir el disparador

`EnchantmentHookEvent` tiene solo 9 miembros y todos funcionan — aca el enemigo no es un enum
gigante como en items, es la **semantica**: `ComboMatched` es preview y re-dispara por toggle
de hold (Paso 2.5). `EnchantmentTriggerCatalog` lleva esa trampa en el dato (`ScratchOnly`):

```csharp
var sb = new System.Text.StringBuilder();
foreach (var o in Rollgeon.Editor.Tools.Enchantment.EnchantmentTriggerCatalog.All)
    sb.Append(o.Id).Append(" | ").Append(o.DisplayName).Append(" | ").Append(o.Help).AppendLine();
return sb.ToString();
```

- `TriggerId` vacio = nace sin triggers (valido para solo-filtro o solo-capability).
- Un `TriggerId` fuera del catalogo **falla la creacion**, no crea un encantamiento mudo.
- `TriggerComboIds` solo aplica a las opciones `*.ids`.
- `RequireCarrierParticipates` con un disparador que no es de combo **falla la creacion**.

## Paso 5 (escritura B) — Autorar efectos, filtro y capabilities

Con `TriggerId`, `CreateEnchantment` deja el trigger armado pero **sin efectos**: ya sabe cuando
dispara y todavia no hace nada. Este segundo `execute_code` le pone el comportamiento:

```csharp
var path = "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_PiedraSangrienta.asset"; // AssetPath del Paso 4
var e = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Upgrades.Dice.EnchantmentSO>(path);
if (e == null) return "FAIL: no se encontro " + path;

// El trigger ya existe con su disparador puesto: solo se le agregan los efectos.
var bridge = (Rollgeon.Upgrades.Dice.Triggers.ExecuteEffectsOnDiceEvent)e.Triggers[0];
bridge.Effects.Add(new Rollgeon.Effects.EffectData
{
    Label = "Piedra Sangrienta +4",
    PreConditions = new System.Collections.Generic.List<Rollgeon.PreConditions.BasePreCondition>(),
    Effects = new System.Collections.Generic.List<Rollgeon.Effects.IEffect>
    {
        new Rollgeon.Effects.Concretes.EffHeal(),  // leé los campos reales del efecto antes
    },
    TargetSelector = null,
});

// Opcional: filtro de caras y capabilities.
// e.EditorSetFaceFilter(new Rollgeon.Upgrades.Dice.Filters.ParityFilter());

UnityEditor.EditorUtility.SetDirty(e);
UnityEditor.AssetDatabase.SaveAssets();
return "OK triggers=" + e.Triggers.Count + " efectos=" + bridge.Effects.Count;
```

Notas:
- **`EditorSetTriggers` pisa la lista entera; `EditorAddTrigger` agrega.** Para tocar un
  encantamiento existente, leé la lista actual antes de decidir cual usar.
- `SetDirty` + `SaveAssets` son obligatorios: sin eso Odin no re-serializa y el cambio se
  pierde al recargar el dominio.
- Los campos exactos de cada `IEffect` no estan hardcodeados aca. Antes de escribirlos, leé el
  archivo del efecto en `Assets/Scripts/Rollgeon/Effects/Concretes/`. Varios usan propiedades
  con backing field `[OdinSerialize, SerializeReference]` — si una propiedad no tiene setter
  publico, asignala por reflexion o elegí otro efecto.
- Si el efecto va en un hook `combo.matched.*`, **solo `IComboScratchWriter`** (Paso 2.5).

## Paso 6 — Verificar y reportar

1. `mcp__UnityMCP__read_console` para confirmar que no quedaron errores.
2. Releé el encantamiento y pasale la salud:

```csharp
var e = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Upgrades.Dice.EnchantmentSO>(path);
var sb = new System.Text.StringBuilder();
sb.AppendLine("efectos: " + string.Join(", ", System.Linq.Enumerable.Select(
    Rollgeon.Editor.Tools.Enchantment.EnchantmentQuery.GetEffectTypes(e), t => t.Name)));
foreach (var f in Rollgeon.Editor.Tools.Enchantment.EnchantmentQuery.CheckCatalogHealth(new[] { e }))
    sb.AppendLine(f.Severity + ": " + f.Message);
return sb.ToString();
```

3. **Sumá el id nuevo al diccionario de `EnchantmentCategoryAssigner`**
   (`Assets/Scripts/Editor/Tools/Enchantment/EnchantmentCategoryAssigner.cs`, con Edit — es un
   .cs, no un .asset): una linea `["ench.mi_id"] = EnchantmentCategory.MiCategoria,` en su
   seccion. El alta ya seteo la categoria en el asset; esto mantiene al assigner como fuente
   completa para reparaciones masivas.
4. Cerrá con estas advertencias, textuales:
   - **Ctrl+Z no borra el `.asset`.** Un undo despues de crear revierte catalogo, pool y
     localizacion, pero el archivo queda en disco (limitacion de Unity, spec §7.1). El camino
     correcto para deshacer es `EnchantmentAuthoring.DeleteEnchantment(e)`, no Ctrl+Z. Un
     huerfano aparece luego como hallazgo en `EnchantmentQuery.CheckCatalogHealth` y rompe
     `EnchantmentCoverageAuditTests`.
   - **El id esta congelado.** Es clave de save: cambiarlo despues (`RenameEnchantmentId`)
     rompe compatibilidad de partidas guardadas.
   - Si la localizacion en ingles quedo vacia o igual a la castellana, **la suite de tests
     queda roja** hasta que se complete.
   - El encantamiento no esta commiteado. **No commitees vos** salvo que el usuario lo pida.

## Cuando NO usar esta skill

- **Items pasivos o activos**: eso es `/passive-item`. Los "encantamientos de item activo"
  (`ActiveItemEnchantmentSO` en `Assets/Rollgeon/Items/ActiveEnchantments/`) son OTRO sistema —
  no pasan por aca.
- Editar un encantamiento existente: se puede reautorar con el Paso 5, pero confirmá primero
  con el usuario que quiere pisar lo que hay.
- Contenido sobre capabilities `[NotYetWired]`: avisá que no estan cableadas y frená.

Detalles de dominio (filtros, capabilities, efectos del canal scratch, categorias GDD, errores
conocidos): `references/domain-cheatsheet.md`.
