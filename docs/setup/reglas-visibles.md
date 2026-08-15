# Reglas invisibles · hacerlas visibles

> Estado al 2026-08-14. El código ya está; falta correr un MenuItem para que
> los prefabs de UI tengan las piezas nuevas, y autorar cuatro strings.
>
> Regla madre del documento de jefes: **todo lo que cambia tu contrato se
> muestra en la tabla de combos, sobre la fila afectada, mientras dure.** Nada
> vive sólo en un tooltip o en un panel cerrado.

## Qué cambió en código

| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/Rollgeon/UI/HUD/Contract/ContractRowState.cs` | La marca de una fila (`None` / `Blocked` / `Forbidden` / `Shifted` / `Buffed` / `Nerfed`) y su texto de badge. |
| `.../ContractRowStateResolver.cs` | Deduce la marca de cada fila leyendo `IContractModifierService` + `IComboBlockService`. `Resolve` es puro. |
| `.../ContractComboRowView.cs` | La fila ahora muestra el daño **efectivo** (no el de la hoja), se tacha y levanta un badge. |
| `.../ContractDrawerView.cs` | Se suscribe a `OnContractModifierChanged`, `OnComboBlocked`, `OnComboUnblocked` y `OnTurnFinished`. Antes no escuchaba nada. |
| `.../ContractRuleBoardView.cs` | **Nuevo.** La planilla: cartel persistente con SÓLO las filas alteradas. Se apaga entero cuando no hay ninguna. |
| `Assets/Scripts/Editor/Tools/HUD/ContractDrawerSetupTools.cs` | Arma la tachadura y el badge en el prefab de fila, y el paso 5 que instala la planilla. |

El drawer sólo repinta **si está abierto**: cerrado ya se repuebla al abrirse
(`SlidingDrawer.Opened`), y quien avisa que algo cambió mientras está cerrado es
la planilla.

## 1. Correr el installer

`Rollgeon → Contract Drawer → Setup All`

Es idempotente. Los pasos que importan acá:

- **3 - Create Row Prefab** — agrega a `Assets/Prefabs/UI/ContractComboRow.prefab`
  un hijo `Strike` (`Image`, apagado) que cruza nombre + daño, y un hijo
  `RuleBadge` (`Image` + label, apagado) encima de la mano de ejemplo.
- **5 - Setup Rule Board** — crea `ContractRuleBoard` dentro de
  `Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab`, con su `Panel` apagado.

Si sólo querés la planilla sin re-armar el drawer, corré **3** y después **5**
(el 5 necesita el row prefab actualizado).

> El badge se dibuja **encima** de la mano de ejemplo en vez de agregar una
> columna: una columna más ensancharía el drawer entero por algo que casi
> siempre está apagado, y con la fila marcada el ejemplo es lo que menos
> importa. Si preferís una columna aparte, hay que subir `NameWidth` /
> `RowSize` en el installer — `PanelSize` se recalcula solo.

## 2. Dónde queda la planilla

Sale colgada del borde izquierdo en `x = 24`, `y = -136` — el mismo lugar donde
abre el drawer, así que al abrir la tabla completa ésta la tapa y dice lo mismo
en grande. Es un cartel de lectura: `raycastTarget = false`, no come clicks del
tablero.

Para moverla, editar `BoardX` / `BoardY` en `ContractDrawerSetupTools` y
re-correr el paso 5 (o mover el `Panel` a mano en el prefab: el installer
respeta el objeto existente pero **le pisa la posición** en la próxima corrida).

El alto lo pone el contenido (`VerticalLayoutGroup` + `ContentSizeFitter`): con
una sola regla activa el cartel es una fila. El tope es `_maxRows` en el
componente (default 4) — pasado eso, el jugador abre el drawer.

## 3. Strings a autorar (tabla `UI`)

Sin estas keys se usan los fallbacks entre paréntesis, así que **no bloquean**;
autorarlas es lo que permite traducirlas y ajustar el largo.

| Key | Fallback | Se le concatena |
|---|---|---|
| `contract.rule.board_title` | `PLANILLA` | — |
| `contract.rule.forbidden` | `PROHIBIDO` | — |
| `contract.rule.blocked` | `BLOQUEADO` | ` ` + turnos restantes |
| `contract.rule.shifted` | `PAGA COMO` | ` ` + nombre del combo destino |

**Sin placeholders de formato.** Los textos se concatenan, no se pasan por
`string.Format`: un `{0}` mal autorado en la tabla tiraría en pantalla.

Los fallbacks son ASCII en mayúscula a propósito — `m6x11plus SDF` no tiene
glifos para `✕`, `→`, `▲`. Si se quiere el `✕` del documento, primero hay que
extender el font asset.

## 4. Cómo se ve cada regla

| Marca | Cuándo | Fila |
|---|---|---|
| **Bloqueado** | `IComboBlockService.GetRemainingTurns > 0` | Tachada + badge `BLOQUEADO N` |
| **Prohibido** | `IContractModifierService.IsForbidden` | Tachada + badge `PROHIBIDO`, daño `0` |
| **Corrido** | El daño efectivo cae exactamente sobre el base de otra fila | Tachada + badge `PAGA COMO <combo>` |
| **Sube** | Efectivo > base y no matchea ninguna fila | Daño en verde + badge `+N` |
| **Baja** | Efectivo < base y no matchea ninguna fila | Daño en rojo + badge `-N` |

Bloqueado le gana a prohibido cuando pasan los dos: se dibujan igual y sólo el
bloqueo sabe cuándo se va.

## 5. Deuda conocida — `IContractModifierService` no dice qué regla aplicó

El servicio expone el daño efectivo y `IsForbidden`, pero no **qué**
modificador produjo el valor. El corrimiento se reconoce por deducción:
`SetComboToNeighbor` copia el base del vecino tal cual, así que el efectivo cae
exactamente sobre otra fila de la tabla.

Consecuencia: un `MultiplyCombo` que caiga justo sobre el base de otra fila se
lee como corrimiento. El mensaje sigue siendo cierto ("ahora paga como
aquella"), pero pierde precisión.

Para eliminar la ambigüedad haría falta, en
`Assets/Scripts/Rollgeon/Combat/ContractMod/IContractModifierService.cs`:

```csharp
/// <summary>Vista read-only del modificador activo de un combo, para la UI.</summary>
bool TryGetModifier(string comboId, out float multiplier, out int? setValue, out string setFromComboId);
```

`ContractModifierService.SetComboToNeighbor` ya calcula el combo vecino —
alcanza con guardarlo en `ComboMod` junto al `SetValue`. **No se tocó desde la
tarea de UI**: el archivo es del área de combate y hay trabajo en paralelo ahí.

## 6. Lo que este cambio NO cubre

Del documento de jefes, siguen sin implementar:

- **Dado confiscado con tween a la mesa del jefe.** Hoy `DiceZoneView` ya
  grisea y pone candado en el slot (`IDiceBlockService`), pero no hay viaje del
  dado ni tween inverso al devolverlo.
- **Sacudida + sonido de rechazo** al intentar jugar un combo prohibido.
- **Animación de pluma del Anotador** al tachar la fila (hoy la tachadura
  aparece de golpe).
- **Debilidad del jefe** (icono del combo + `×1,5` junto a su barra de vida).
- **Contadores físicos en el mundo** (rueda, cuenta, pozo, escalón).
