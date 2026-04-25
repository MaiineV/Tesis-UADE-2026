# Setup — System#0401 Exploration Behaviors

> **Audiencia:** el usuario del proyecto, tras mergear el branch a `develop`.
> **Tiempo estimado:** 15-20 min.
> **Prerrequisito merge:** ExplorationBehaviorService, ExplorationActionButtonsView,
> PhaseBehaviors refactor (ClassHeroSO, HeroActionBehavior, CombatHandoffService) ya
> en `develop`.

Este instructivo cubre el setup necesario para que los behaviors de exploracion
funcionen en runtime. El codigo C# ya esta mergeado — solo falta configurar assets,
escena e Inspector.

---

## 1. Renombrar campo YAML en CH_Warrior.asset

El asset `CH_Warrior` todavia tiene el campo viejo `ContextualBehaviors` en su
serializacion. Hay que renombrarlo a mano para que Unity lo deserialice con el
nuevo nombre.

1. Cerrar Unity (o al menos no tener `CH_Warrior` abierto en el Inspector).
2. Abrir `Assets/Rollgeon/Classes/CH_Warrior.asset` con un **editor de texto**.
3. Buscar las dos ocurrencias de `ContextualBehaviors`:
   - **Linea ~927**: `- Name: ContextualBehaviors` → cambiar a `- Name: PhaseBehaviors`
   - **Linea ~1003**: `ContextualBehaviors: []` → cambiar a `PhaseBehaviors: []`
4. Guardar. Volver a Unity y dejar que reimporte.

> **Verificacion:** abrir CH_Warrior en el Inspector — debe aparecer la seccion
> **"Phase Behaviors"** con una lista vacia (sin errores ni campos perdidos).

---

## 2. Agregar Exploration Movement en PhaseBehaviors

En el Inspector de `CH_Warrior` (ClassHeroSO), seccion **Phase Behaviors**:

1. Click **"+"** para agregar una entrada.
2. Configurar:

| Campo | Valor |
|-------|-------|
| `IsBaseBehavior` | **true** (tick) |
| `Slot` | **Movement** |
| `ActionName` | `"Movement"` |
| `AllowedPhases` | **Exploration** (solo Exploration) |
| `EnergyCost` | **1** |
| `NeedsDiceRoll` | **false** |
| `BlockOnRepeat` | **false** |

3. En la lista **Effects**, agregar un `EffectData` con un efecto de tipo
   **EffMove** (o el efecto de movimiento que corresponda).
4. En el `SelectionSettings` del efecto:

| Campo | Valor |
|-------|-------|
| `SlotState` | **Empty** |
| `IsGlobal` | **true** |
| `Timing` | **BeforeRoll** |
| `AutoAccept` | **true** |

> `IsGlobal = true` es lo que habilita movimiento libre por toda la sala, sin
> limite de rango.

### Opcional: Exploration Healing

Si queres curar durante exploracion, agregar otra entrada:

| Campo | Valor |
|-------|-------|
| `IsBaseBehavior` | **true** (tick) |
| `Slot` | **Healing** |
| `ActionName` | `"Healing"` |
| `AllowedPhases` | **Exploration** |
| `EnergyCost` | a definir |
| `NeedsDiceRoll` | **false** |

Configurar los effects de heal como en el behavior de combat.

---

## 3. Crear ExplorationActionButtonsView en la escena

En `02_Gameplay.unity`, dentro del hierarchy del **ExplorationHUD** (el GameObject
que tiene el componente `ExplorationHUDView`):

1. Crear un GameObject hijo — nombre sugerido: `ExplorationActions`.
2. Agregar el componente **ExplorationActionButtonsView**.
3. Crear botones hijos (UI > Button) — **uno por cada behavior** que hayas
   declarado en PhaseBehaviors para Exploration:
   - Minimo **1 boton** (Movement).
   - Si agregaste Healing, **2 botones**.
4. Arrastrar cada Button al campo `_buttons` (lista) del componente, **en el
   mismo orden** en que los behaviors se resuelven:
   - Indice 0 → Movement
   - Indice 1 → Healing (si existe)
   - Indice N → siguientes behaviors custom

> Los botones que no tengan behavior asignado se ocultan automaticamente en
> runtime.

---

## 4. Cablear referencia en ExplorationHUDView

En el Inspector del componente **ExplorationHUDView** (en el ExplorationHUD):

1. Arrastrar el GameObject `ExplorationActions` (el que tiene
   `ExplorationActionButtonsView`) al campo **`_explorationActions`**.

---

## 5. Lo que NO hace falta tocar

| Item | Razon |
|------|-------|
| **Asmdefs** | Todos los archivos nuevos caen bajo `Rollgeon.asmdef` existente |
| **RunController** | Ya registra `ExplorationBehaviorService.CreateAndRegister()` |
| **CombatHandoffService** | Ya usa `GetBehaviorsForPhase(Combat)` sin hardcoding |
| **Test asmdefs** | Los tests nuevos compilan contra los asmdefs existentes |

---

## 6. Verificacion

1. **Play** → entrar en una run → fase Exploration.
2. Los botones de exploracion deben aparecer visibles.
3. Click en el boton de Movement → deben iluminarse **todos los tiles** de la
   sala (IsGlobal=true, sin rango).
4. Click en un tile → el jugador se mueve → energia baja en 1.
5. Si la energia llega a 0 → el boton de Movement se grisa (interactable=false).
6. Entrar en combate → los botones de exploracion desaparecen.
7. Volver a exploracion → los botones reaparecen con energia refreshed.
