# Item Editor — definición de la pasada de mejoras

> Rama `Feature#0056_PassiveItemEditorTool`.
> Contrasta `Tools/Item Editor` (`Assets/Scripts/Editor/Tools/Item/ItemEditorWindow.cs`,
> hosteado sobre §26.12) contra el GDD de Ítems Pasivos en DocsApp.
> Estado del diagnóstico previo: `docs/tools/hero-class-editor-review.md` fijó el formato.

---

## 0. Punto de partida

| | |
|---|---|
| GDD | 37 ítems distintos → ~90 instancias con tiers y rarezas |
| Proyecto | **24 assets `ItemSO`** — 23 `Passive`, 1 `Active` (`potion.healing`) |
| Coinciden con el GDD | 7 |
| Legacy | 15 — versiones a mano de lo que el GDD ahora resuelve con plantillas `<combo>` |
| La tool | 90 líneas propias sobre ~1.700 de infra compartida |

El Item Editor es, en la práctica, la tool de pasivos.

**Los ítems no viven todos en la misma carpeta.** `DefaultFolder` es
`Assets/Rollgeon/Items`, pero dos assets están afuera:
`Assets/Rollgeon/Rooms/Shop/Items/Item_HealingPotion.asset` y
`Assets/Rollgeon/Tutorial/Item_Tutorial_Par50.asset`. La lista los encuentra igual
(`FindAssets("t:ItemSO")` es project-wide), pero **nada puede asumir que un ítem está bajo
`DefaultFolder`** — ni el renombrado, ni el agrupado por familia, ni las métricas.

`item.tutorial.par50` además usa un prefijo `item.` que ningún otro ítem tiene: un argumento
más para la política de ids de §3.

### Línea base verificada (MCP, Unity 6000.3.11f1)

Compila limpio — solo warnings preexistentes (CS0414 / CS0618 / CS0067), cero errores.
**82/82 tests EditMode en verde** sobre `Rollgeon.Editor.Tools.Polymorphic.Tests`,
`Rollgeon.Items.Tests` y `Rollgeon.Editor.Tools.Localization.Tests`.

`Rollgeon.Editor.Tools.Polymorphic.Tests` (AssetNaming, BlockGraph,
PolymorphicAuthoringContext, PolymorphicMemberScanner) es **la red de seguridad de la Fase 0**:
tiene que seguir verde después de partir el shell.

---

## 1. Decisiones tomadas

| # | Decisión | Resuelto |
|---|---|---|
| D1 | **Familia por variante**, no atada a rareza. 1 asset = 1 ítem = 1 id = 1 precio; la familia los agrupa | El motor no se toca. Cuando llegue "familia por combo" entra sin migración |
| D2 | **Precio en el ítem y ligado a rareza** (hueco H) — entra al flujo de creación | Desbloquea la tab de métricas |
| D3 | **Rareza Dios** (hueco G) + realineo de la paleta al GDD de pasivas | Ver §5 |
| D4 | **Id = slug del Display Name**, con puntos. Unicidad obligatoria. Sin hash | Ver §3 |
| D5 | **Sin cambios de motor** más allá de D2 y D3 | Ver §7 |
| D6 | El bug de copias duplicadas (hueco C) queda **documentado, no arreglado** | `TECHNICAL.md §18.6` |
| D7 | El conflicto de paletas **no se comunica todavía** al equipo | Ver §5.3 |

---

## 2. Modelo de datos

Tres campos nuevos en `ItemSO`. Nada más.

- **`FamilyId`** — string. Agrupa las variantes de un mismo ítem. Vacío = ítem suelto.
  Sirve además como el "tag" para filtrar la lista: *Botas*, *Coraza*, *Corona*.
- **`VariantIndex`** — int. Posición dentro de la familia. **Deliberadamente no es la rareza**:
  hoy las variantes son tiers, mañana pueden ser combos, y atarlo a `ItemRarity` obligaría a
  migrar cuando lleguen las plantillas `<combo>`.
- **Precio** — se resuelve desde la rareza por tabla del GDD, con override por ítem.
  Escribe el `BasePrice` del `WeightedShopItem` en `ShopPool.asset`; el ítem deja de depender
  de que alguien se acuerde de ir a otro asset.

| Rareza | Precio base GDD |
|---|---|
| Normal | 15 (hasta 20 si el efecto es fuerte dentro del tier) |
| Raro | 35 |
| Épico | 60 |
| Legendario | 100 |
| Dios | 120 |

> **Seguro tomado:** las métricas y los conteos operan sobre **instancias del catálogo**, no
> sobre assets en disco, aunque hoy sea 1 a 1. El día que un asset de plantilla represente 25
> instancias, la capa de agregación ya está en el lugar correcto.

---

## 3. Identidad y unicidad

**Id = slug del Display Name**, minúsculas sin acentos, separadores en punto, siguiendo lo que
ya hay en disco: `"Banquete Real"` → `banquete.real`.

**Sin hash.** El id es clave de save (`InventorySlotSnapshot.ItemId`, `PassiveItemIds`,
`RoomObjectState.ReservedItemId`), de localización (`<id>.name` / `<id>.desc`) y de la consola
(`giveitem banquete.real`). Un sufijo aleatorio lo vuelve ilegible justo donde más se lee. Con
familias tampoco hace falta: los nombres de tiers del GDD ya son distintos entre sí
(Botas Ligeras / del Viento / del Rayo / Alas de Hermes → 4 slugs únicos naturalmente).

**Reglas:**

1. **Unicidad verificada globalmente**, no solo contra el ítem seleccionado. Dos assets con el
   mismo id salen marcados en la lista, ambos, con salto al otro.
2. **El id se congela al crear.** Cambiar el Display Name después no lo toca.
3. Cambiar el id es una acción explícita que **renombra también las dos claves de localización**
   y avisa que rompe saves en curso.
4. **`Duplicate` se parte en dos**, porque hoy hace dos trabajos disfrazados de uno y es la
   única vía real de generar ids repetidos (copia el id verbatim):
   - **Duplicar como ítem nuevo** → abre el asistente precargado, nombre vacío obligatorio.
   - **Agregar variante a la familia** → deriva nombre e id, copia la estructura, pide solo los
     números. Este es el uso del 90% de los casos.

---

## 4. Localización

**Hallazgo:** la tabla `Content` tiene 44 claves de ítems = 22 × `.name` + `.desc`. Esos 22
ítems ya están localizados en `es` y `en`, así que los campos `DisplayName` y `Description` del
asset **son texto muerto** — nunca se muestran, solo son respaldo si falta la clave.

Por eso la tool no lleva "un botón para ir a la tabla": **edita la tabla**.

> **Ya existe media pieza.** `Rollgeon.EditorTools.Localization.LocalizationSetupTools.UpsertEntry`
> hace el upsert idempotente a `Content` en ES y EN, con `SetDirty` sobre la shared data y sobre
> cada tabla. **No hace `Undo.RecordObject`** — viola §7, así que hay que envolverlo antes de
> usarlo desde la tool.
>
> **Convención del repo a respetar:** `LocalizationContentSeeder` es "la fuente de verdad
> revisable en git" y se re-corre desde el menú. Los textos de los ítems **no** están ahí (se
> autoraron a mano en las tablas), así que escribir desde la tool no los pisa — pero quedan
> invisibles en el diff. Decidir en A3 si la tool además los vuelca al seeder; no bloquea.

- Dropdown de idioma (`es` / `en`, leídos de los locales del proyecto).
- El nombre y la descripción que se ven y se editan son los de la tabla en ese idioma.
- El campo del asset queda debajo, marcado como respaldo, **con aviso cuando difiere** del texto
  real — que es exactamente lo que pasa hoy y nadie ve.
- Al crear un ítem, las dos claves se escriben solas en los dos idiomas.

---

## 5. Rareza y paleta

### 5.1 Enum

Se agrega `God` al final de `ItemRarity`. Es append: los assets guardan el int, nada se corre.

**No se renombran los valores existentes.** Sería lo coherente con el GDD
(`Common/Uncommon/Rare/Legendary` → `Normal/Rare/Epic/Legendary/God`) pero es una trampa:
`ItemRarity.Rare` está usado en ~25 lugares con números pegados al lado — precios de tienda
(10/20/35/55), HP de cofres (20/30/40/55), pesos de loot. Renombrar `Uncommon`→`Rare` y
`Rare`→`Epic` deja todos esos call sites **compilando y corridos un tier**, sin un solo error.

En su lugar: etiquetas de display con el vocabulario del GDD (*Normal / Raro / Épico /
Legendario / Dios*) en la tool, la tienda y los tooltips. Riesgo cero.

### 5.2 Colores

El GDD de pasivas define la escalera con emoji pero **no da hexes**. Se reciclan 3 de los 4 que
ya existen en `RarityPalette`:

| Tier | Emoji | Hex | Origen |
|---|---|---|---|
| Normal | ⚪ | `#D6D3CE` | nuevo — hueso, no blanco puro |
| Raro | 🔵 | `#3A6EA5` | nuevo |
| Épico | 🟣 | `#5C4A7A` | ya existía |
| Legendario | 🟠 | `#D9A44E` | ya existía |
| Dios | 🔴 | `#B33A1F` | ya existía (era Uncommon) |

El rojo pasa de segundo tier a quinto, que es donde el GDD lo quiere. Eso resuelve solo el
choque de que Raro y Dios querían el mismo color.

**Consecuencia:** `RarityPalette` alimenta el tinte del cofre en mundo y el flash del reveal
gacha. De los 4 cofres, **2 cambian de color** (Básico marrón → hueso; segundo tier rojo → azul)
y 2 quedan igual. Hay que re-correr el setup de cofres para re-bakear los prefabs.

### 5.3 ⚠️ Conflicto de docs — pendiente de comunicar

Los dos GDD se contradicen y **todavía no se avisó al equipo** (decisión D7):

| | GDD del Cofre | GDD de Pasivas |
|---|---|---|
| Tiers | 4 — Básico, Épico, Buenardo, Épicardo | 5 — Normal, Raro, Épico, Legendario, Dios |
| Colores | hexes confirmados | solo emoji |

**"Épico" es rojo `#B33A1F` en el doc del cofre y violeta 🟣 en el de pasivas.** Se tomó la
escalera de pasivas como fuente de verdad: tiene 5 tiers, precios y criterio por tier, mientras
que los nombres del cofre (*Buenardo*, *Épicardo*) parecen placeholders de una pasada vieja.

El doc del Cofre queda desactualizado a propósito hasta que se comunique.

---

## 6. Los seis bloques de la tool

### 6.1 Lista

- **Filtros**: por efecto implementado (*"todo lo que toca el oro"* — el más útil y sale gratis,
  se lee del árbol de efectos), por rareza, por tipo, por familia.
- **Slider de tamaño** estilo ventana de Project: de fila `[ Nombre 🖼 ]` a grilla de iconos con
  el nombre debajo.
- **Color de rareza** en la fila, leído de `RarityPalette` — no hexas nuevos sueltos.
- **Gradiente por familia** para leer el tier de un vistazo.
- ⚠️ Los iconos son sprites de atlas (`UI-sheet_3`): el preview de Unity devuelve la hoja entera.
  Hay que dibujarlos con el rect del sprite o la lista se llena de hojas idénticas.
- **Botones** Create / Duplicate / Delete / Ping: reubicados por estética; funcionalmente,
  Duplicate se parte según §3.4.

### 6.2 Creación

Asistente que pide Display Name, descripción, icono, rareza y tipo, y pregunta si es **ítem
suelto o familia de variantes**. Al confirmar, en **un solo paso de undo**:

1. Crea el/los asset(s) con id derivado y congelado.
2. Los registra en `ItemCatalog`.
3. Escribe `<id>.name` y `<id>.desc` en `Content`, en los dos idiomas.
4. Escribe el `BasePrice` en `ShopPool` según la rareza.

### 6.3 Vista de familia

Los tiers como **tabla editable lado a lado**: una columna por variante, una fila por valor.
La estructura se edita arriba y baja a todas; solo los números quedan sueltos. Es lo que evita
ir ítem por ítem.

### 6.4 Grafo

- **Nodos con información real**: el nodo raíz muestra nombre, descripción, icono y tipo;
  los demás dicen *"+30 daño al Full House"* en vez de `EffectData`. Desplegables.
- ⚠️ El grafo se reconstruye entero en cada edición: el estado colapsado hay que guardarlo por
  path o se borra solo y va a parecer un bug.
- **Layout determinístico** para que agregar un nodo no reordene el resto. Compatible con la
  regla vigente de no persistir posiciones (determinístico ≠ guardado).
- **Arrastrar del conector → menú de add.** Con una condición: **nunca conectar nodo con nodo**.
  Las flechas no son cableado, son contención. Soltar en vacío abre el menú; soltar sobre un
  nodo no hace nada.

### 6.5 Raw Data

- Aliviar el peso visual.
- ⚠️ Bug real detectado: `ItemSO.PassiveHooks` es público **y** tiene `[OdinSerialize]`, así que
  se serializa dos veces. Es lo que dispara la advertencia amarilla de Odin en el inspector. Una
  línea.
- El resto del peso son los `[InfoBox]` de los campos de runtime, siempre expandidos. Pasarlos a
  tooltip aliviana mucho pero afecta también al inspector normal — decisión aparte.

### 6.6 Métricas

Para el game designer, no para el que autora.

- Daño / oro / curación por rareza; outliers (*"este Normal pega más que aquel Épico"*).
- Distribución por combo y por evento.
- Valor esperado cruzando con la probabilidad de cada combo.
- Contraste contra la tabla de precios del GDD (§2).
- **Salud del catálogo**: ids repetidos, ítems fuera de la tienda, sin icono, hooks vacíos.

> Dato verificado contra `ShopPool.asset`: **18 de los 24 ítems están en el pool** (1 garantizado
> + 17 roleables). Los 6 de afuera — Amuleto de Reflejo, Coraza Reforzada, Egoísta, Instinto de
> Supervivencia, Rodilleras de Acero y el del tutorial — no cuestan nada y no aparecen en tienda,
> y nada lo avisa hoy.

Reemplaza a `docs/balance/item-inventory.html`, generado a mano en julio, que dice "5 ítems"
cuando hay 22. Matar ese mantenimiento manual es parte del valor.

### 6.7 Skill + MCP

La skill **no escribe el asset**. `ItemSO` es un asset de Odin: el archivo renumera índices de
tipo por orden de aparición y editarlo a mano lo desincroniza en silencio — el proyecto ya se
comió esto (`EgoistaComboBonusReauthorTool`, `AfiladoFaceFilterFixTool` existen por eso).

Forma correcta: **un único punto de entrada en C# que recibe la especificación y construye el
ítem con la API real.** La skill hace preguntas, arma la especificación y llama a ese comando.
El asistente de §6.2 usa el mismo punto de entrada, así que no hay dos caminos que puedan
divergir.

---

## 7. Regla transversal: Dirty y Undo

Hoy en toda la infra de la tool hay **un solo** `Undo.RecordObject` y **un solo** `SetDirty`,
ambos dentro de `PolymorphicAuthoringContext.Mutate()`. Cubren la edición de campos y del grafo.
Todo lo demás está afuera.

**Estado actual:**

| Operación | Undo | Dirty |
|---|---|---|
| Editar campo / agregar / quitar bloque | ✅ | ✅ |
| Alta en el catálogo | ✅ | ✅ |
| Create / Duplicate / Delete / Rename | ❌ | n/a |

**Reglas que se fijan:**

1. **Toda mutación pasa por `Mutate()`** — record → mutar → dirty → notificar, en ese orden. Es
   el único orden que funciona con el blob de Odin (`Undo.RecordObject` fuerza el
   `OnBeforeSerialize` que regenera el blob; un `SerializedProperty` nunca llega ahí).
2. **Toda operación que toca más de un asset se agrupa en un solo paso de undo**
   (`SetCurrentGroupName` + `CollapseUndoOperations`). Crear un ítem toca 4 assets: un Ctrl+Z
   tiene que deshacer los 4, no el último.
3. **Cada asset tocado recibe `SetDirty`.** Los que más se olvidan y más duelen: la tabla de
   localización y el `ShopPool`, que son ScriptableObjects como cualquier otro y se pierden al
   cerrar Unity si nadie los marca.
4. **Las operaciones de archivo no entran al sistema de Undo de Unity.** Create y Duplicate se
   registran con `RegisterCreatedObjectUndo`. Delete y Rename **no se hacen undoables** — se
   protegen con confirmación y con "qué referencia a esto", que es lo que realmente falta hoy:
   borrar deja una entry null en el catálogo y un `WeightedShopItem` sin ítem en el pool, que el
   rolling saltea **en silencio**.

### 7.1 Límite medido de la atomicidad del alta

Probado end-to-end contra Unity con `ItemAuthoring.CreateItem` + un solo `PerformUndo`:

| | Tras crear | Tras un Ctrl+Z |
|---|---|---|
| Archivo `.asset` | ✅ creado | ⚠️ **sigue existiendo** |
| `ItemCatalog` | ✅ registrado | ✅ revertido |
| `ShopPool` | ✅ con precio | ✅ revertido |
| Tabla `Content` (es + en) | ✅ con las 2 claves | ✅ revertido |

Los tres assets revierten a contenido **byte-idéntico** (verificado con `git diff`). El que no
vuelve es el archivo: `Undo.RegisterCreatedObjectUndo` desregistra el objeto pero **Unity no
borra el `.asset` del disco**.

**Consecuencia:** un Ctrl+Z después de crear deja exactamente el estado que el `UndoGroup`
buscaba evitar — un ítem huérfano, sin catálogo, sin precio y sin localización.

**Cómo se cubre:** no se intenta forzar el borrado en el undo (engancharse a
`undoRedoPerformed` para borrar archivos es frágil y puede comerse trabajo real). En su lugar,
`ItemQuery.CheckCatalogHealth` ya reporta los ítems fuera del catálogo y fuera del pool, así que
el huérfano **aparece como hallazgo** en la tab de métricas. La UI de la Fase 3 debería ofrecer
borrarlo desde ahí.

`Item_New.asset` e `Item_New 1.asset`, que están sin trackear en el repo, son exactamente este
caso y sirven de caso de prueba.
5. **Un test EditMode por superficie** que verifique que después de cada mutación el asset quedó
   dirty. Es la única forma de que la regla 3 no se degrade con el tiempo.

---

## 8. Fuera de alcance

Ninguno bloquea esta rama. Siete de los diez huecos son aditivos: la tool los absorbe sola
porque hooks y efectos se arman por reflexión.

| | Hueco | Desbloquea | ¿Le pega a la tool? |
|---|---|---|---|
| A | Los 4 eventos faltantes (movimiento, puerta, defensa, umbral) | ~13 instancias | No |
| B | Multiplicador de daño persistente | 7 ítems | No |
| C | Estado por copia — **C1 bookkeeping (roto hoy)**, C2 memoria | 4 ítems | Poco |
| D | Disparador compuesto | — | Ya es posible |
| E | Ítems que cambian una regla — 6 sistemas distintos | 6 ítems | No |
| F | Plantillas `<combo>` | **25 instancias** | Sí |
| I | Convivencia: contrato, incompatibles, duplicados | — | Un campo |
| J | Feedback al dispararse + inventario agrupado | — | No |

**F es el que más rinde** después de esta pasada: 25 instancias de un saque y jubila los 15
ítems legacy. **E es el peor negocio**: 6 ítems a cambio de tocar economía, contrato, curación,
inventario, presupuesto de rolls y la fórmula de daño.

**C1 está roto hoy** — ver `TECHNICAL.md §18.6`. Contenido en `InventoryService`, sin migración
de save. Se dejó documentado a pedido.

---

## 9. Orden propuesto

Por dependencias, no por importancia:

1. Modelo de datos — `FamilyId`, `VariantIndex`, precio, rareza Dios y paleta
2. Asistente de creación — ids, unicidad, localización, precio
3. Vista de familia
4. Lista — filtros, slider, iconos, color
5. Grafo — nodos con info, layout, menú desde el conector
6. Métricas
7. Raw Data (barato, entra en cualquier momento)
8. Skill + MCP sobre el punto de entrada de 2

La regla de Dirty/Undo (§7) no es un paso: aplica a todos.
