# Setup — UI#0098 Class Selection Screen

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 25-35 min (armar panel + prefab ComboRow + wirear Inspector).
> **Prerrequisito merge:** T102 (Main Menu + ScreenManager + BaseScreen), T97a (8 BaseComboSO
> concretos + `ComboCatalogSO`), T97b (`ClassHeroSO` + `ContractSheet` + `ContractWarriorFactory`),
> Foundation#0001–0005.

Este instructivo deja la pantalla de seleccion de clase **operativa dentro del editor**.
El worktree solo entrega **codigo C#** (`Rollgeon.UI.Screens.ClassSelectionScreen` +
`Rollgeon.UI.HUD.ContractDisplayView` + `Rollgeon.UI.HUD.ComboRowView` + payload + tests).
La escena / canvas / prefabs los arma el usuario manualmente por diseño.

Cualquier duda sobre el *por que* de un paso: ver `plan.md` (worktree raiz) y
`TECHNICAL.md` §17.D / §5.3 / §5.4.

---

## 8.0 Decisiones de infra confirmadas en este merge

- **No hay `Rollgeon.UI.asmdef` runtime.** Mantenemos la convencion de T102:
  `Rollgeon.UI.*` vive en `Assembly-CSharp` default. Si se modulariza en el futuro,
  es refactor dedicado.
- **`Rollgeon.UI.Tests.asmdef` (EditMode)** reutiliza la suite existente de T102.
  Este worktree le agrega la referencia `Rollgeon.Combos.Tests` para poder usar
  `ComboTestUtils.CreateCombo<T>` desde los tests nuevos.
- **Escena — Opcion A (recomendada).** Se reutiliza `01_MainMenu.unity`: la
  `ClassSelectionScreen` se agrega como screen hermana dentro del mismo Canvas.
  **No se crea** `02_ClassSelection.unity` (Opcion B del plan §8.3) — la escena
  dedicada queda para una tarea futura con `ISceneService` (§17.K). Esto
  **no toca Build Settings**.

---

## 8.1 Pre-requisitos

1. **T102 mergeada** a `develop`. Verificar que existen:
   - `Assets/Scripts/Rollgeon/UI/BaseScreen.cs`.
   - `Assets/Scripts/Rollgeon/UI/ScreenManager.cs` + `ScreenHost.cs`.
   - `Assets/Scripts/Rollgeon/UI/Screens/MainMenuScreen.cs` con la constante
     `ClassSelectionScreenId = "ClassSelectionScreen"` (tiene que matchear con
     `ClassSelectionScreen.ScreenStringId`).
2. **T97a + T97b mergeadas.** Existen:
   - 8 `BaseComboSO` concretos (`Combo_Par`, `Combo_DoblePar`, `Combo_SumaX`, `Combo_Trio`,
     `Combo_Escalera`, `Combo_FullHouse`, `Combo_Poker`, `Combo_Generala`).
   - `ComboCatalogSO` registrado en el bootstrap (Foundation#0005).
   - `ClassHeroSO` + `ContractSheet` + `ContractWarriorFactory`.
3. **Odin, TMP instalados** (mismos requisitos de T102).
4. Abrir el proyecto en Unity, esperar recompile, confirmar **0 errores** en consola.

---

## 8.2 Crear `ClassHero_Warrior.asset`

Si aun no existe (de un worktree anterior), crearlo:

1. `Assets → Create → Rollgeon → Heroes → Class Hero` en
   `Assets/Rollgeon/Heroes/Instances/` → renombrar `ClassHero_Warrior`.
2. Rellenar:
   - `EntityId = "hero.warrior"`.
   - `DisplayName = "Guerrero"`.
   - `Description`: 2 lineas breves ("Clase body. Golpea duro con combos clasicos de
     dados. Su contrato prioriza Generala.").
3. `Sheet.Combos`: arrastrar los 8 `.asset` de T97a **en orden canonico** (Par,
   DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala) —
   `ContractWarriorFactory.CanonicalOrder` los lista.
4. `Sheet._displayLabel` (privado del sheet): setear a `"Contrato del Guerrero"` via
   `ContractWarriorFactory.Build(catalog)` en un editor menu one-shot. El
   `ContractDisplayView` tiene fallback `"Contrato"` si queda vacio.
5. `Portrait`: sprite placeholder o null (el screen no crashea si queda null — el
   `Image.sprite` del prefab se mantiene).
6. **No tocar** los campos marcados `[STUB] elevated by Hero Template task`
   (`BaseMaxHp`, `BaseSpeed`, `StartingDiceBagRef`, `PassiveRef`): son para Hero
   Template futura, no los consume este worktree.

---

## 8.3 Abrir `01_MainMenu.unity`

1. Abrir la escena `Assets/Rollgeon/Scenes/01_MainMenu.unity`.
2. Confirmar que existen:
   - `Canvas` (el mismo de T102).
   - `ScreenHost` (con `ScreenManager` + `_initialScreenStringId = "MainMenu"`).
   - `MainMenuScreen` como hijo del Canvas (disabled al bootstrap — lo activa
     el `ScreenHost`).
3. **No tocar** la screen del MainMenu ni el initial screen id. El push al
   `"ClassSelectionScreen"` ya esta cableado en `MainMenuScreen.OnPlayClicked`
   (T102).

---

## 8.4 Armado del panel `ClassSelectionScreen`

Agregar bajo el Canvas un nuevo GameObject hermano del `MainMenuScreen`:

```
Canvas
├── MainMenuScreen                        (existente — T102)
└── ClassSelectionScreen                  (NUEVO — GameObject empty + script)
    ├── [Component] ClassSelectionScreen.cs
    ├── Background                        (Image full-stretch, color consistente con MainMenu)
    ├── LeftPanel                         (VerticalLayoutGroup, anchor izquierdo)
    │   ├── WarriorButton                 (Button - TMP, label "Guerrero")
    │   │   ├── WarriorSelectionIndicator (Image o GameObject con outline, arranca disabled)
    │   │   └── LockIcon                  (Image disabled — Guerrero no esta lockeado)
    │   ├── MagoButton                    (Button - TMP, label "Mago")
    │   │   └── LockIcon                  (Image enabled con sprite de candado)
    │   └── PicaroButton                  (Button - TMP, label "Picaro")
    │       └── LockIcon                  (Image enabled con sprite de candado)
    └── RightPanel                        (VerticalLayoutGroup, anchor derecho)
        ├── PortraitImage                 (Image, aspect 1:1)
        ├── ContractDisplayView           (GameObject + ContractDisplayView.cs)
        │   ├── HeaderLabel               (TMP, "Contrato del Guerrero")
        │   ├── RowsContainer             (VerticalLayoutGroup, vacio en design time)
        │   └── FooterLabel               (TMP opcional, "Dano minimo = dado mas alto")
        ├── PassiveLabel                  (TMP, "Pasiva: TBD")
        └── ConfirmButton                 (Button - TMP, label "Confirmar")
```

**Notas importantes:**

- `ClassSelectionScreen` arranca **disabled** cuando pusheas Play — el `ScreenHost`
  la desactiva automaticamente con `includeInactive: true`. El push desde Jugar la
  activa.
- Los listeners de los botones se **cablean en `OnPushed()` via codigo** — NO agregar
  `OnClick()` en el Inspector. Si se cablea via Inspector y por codigo, los handlers
  corren 2 veces.

---

## 8.5 Armado del prefab `ComboRow`

1. Crear carpeta `Assets/Rollgeon/Prefabs/UI/` si no existe.
2. `GameObject → UI → Panel` → renombrar `ComboRow`, setear `HorizontalLayoutGroup`
   (+ padding/spacing a gusto).
3. Children:
   - `IconImage` (Image) — **opcional**, puede quedar disabled si no hay arte.
   - `NameLabel` (TMP, flex grow).
   - `DamageLabel` (TMP, right-aligned).
   - `DescriptionLabel` (TMP, opcional, text area pequeña).
4. Adjuntar `ComboRowView.cs` al root del `ComboRow`.
5. En el Inspector del `ComboRowView`: arrastrar cada TMP al field correspondiente.
   `_descriptionLabel` y `_iconImage` son opcionales — dejar null si no se usan.
6. Arrastrar el `ComboRow` a `Assets/Rollgeon/Prefabs/UI/ComboRow.prefab`, luego
   borrar el instance de la escena.

### Wiring del `ContractDisplayView`

En el Inspector del GameObject `ContractDisplayView` del RightPanel:

| Field | Arrastrar |
|---|---|
| `_headerLabel` | `HeaderLabel` (TMP hijo del `ContractDisplayView`). |
| `_rowsContainer` | `RowsContainer` (Transform hijo). |
| `_rowPrefab` | `Assets/Rollgeon/Prefabs/UI/ComboRow.prefab`. |
| `_footerLabel` | `FooterLabel` (opcional — null si no se usa). |

---

## 8.6 Wiring del `ClassSelectionScreen`

En el Inspector del GameObject raiz `ClassSelectionScreen`:

| Field | Arrastrar |
|---|---|
| `_warriorHero` | `Assets/Rollgeon/Heroes/Instances/ClassHero_Warrior.asset`. |
| `_nextScreenStringId` | dejar `"BuildSelectionScreen"` (stub hasta T-build). |
| `_rulesetId` | dejar `"default"`. |
| `_warriorButton` | `WarriorButton` del LeftPanel. |
| `_magoButton` | `MagoButton` del LeftPanel. |
| `_picaroButton` | `PicaroButton` del LeftPanel. |
| `_confirmButton` | `ConfirmButton` del RightPanel. |
| `_contractDisplay` | GameObject `ContractDisplayView` del RightPanel. |
| `_portraitDisplay` | `PortraitImage` del RightPanel. |
| `_passiveDisplay` | `PassiveLabel` (TMP) del RightPanel. |
| `_warriorSelectionIndicator` | `WarriorSelectionIndicator` (GameObject hijo del WarriorButton). |

Ningun field puede quedar `[Required]` vacio — Odin warnea en el Inspector.

---

## 8.7 Lock visuals Mago / Picaro

El codigo setea `Button.interactable = false` en `OnPushed()`. El lock visual lo
arma el usuario:

- Agregar un LockIcon (Image con sprite de candado) como hijo del Button, bien
  visible sobre el texto.
- Configurar el Button → `Transition → Color Tint` con `Disabled Color` gris para
  que el `interactable = false` aplique desaturacion automatica.
- Alternativamente, usar `Sprite Swap` con un sprite "bloqueado" custom.

---

## 8.8 Selection indicator del Guerrero

- `WarriorSelectionIndicator` es un GameObject hijo del `WarriorButton`. Puede ser:
  - Una Image con outline (fondo glow/check icon) — arranca con `SetActive(false)`.
  - Un `OutlineOverride` (Image border sobre el boton).
- El codigo lo prende al clickear el Guerrero y lo apaga si el Confirm falla
  (no deberia en MVP).

---

## 8.9 Build Settings

**Opcion A (recomendada):** `ClassSelectionScreen` vive en `01_MainMenu.unity`
como screen hermana del Canvas. **No hay cambios en Build Settings.**

**Opcion B (no aplica en este sprint):** si se decide crear `02_ClassSelection.unity`
como escena aparte, agregar en Build Settings en index 2 (despues de
`00_Bootstrap` en 0 y `01_MainMenu` en 1). Esta opcion requiere un
`ISceneService` async (fuera de scope — plan §8.3).

---

## 8.10 Verificacion funcional — smoke test

1. Abrir `Assets/Rollgeon/Scenes/00_Bootstrap.unity` → presionar **Play**.
2. Se carga automaticamente `01_MainMenu` (via `BootstrapRunner` + Foundation#0005).
3. Click **Jugar**:
   - La `MainMenuScreen` se desactiva.
   - La `ClassSelectionScreen` se activa.
   - Consola: `"[ClassSelectionScreen] "` — no logs de error.
4. Estado inicial esperado:
   - Boton Guerrero **habilitado** (no grayed-out).
   - Botones Mago + Picaro **grayed-out** (con LockIcon visible).
   - Panel derecho **vacio** (portrait placeholder, ContractDisplayView sin rows).
   - Boton Confirmar **deshabilitado**.
5. Click **Guerrero**:
   - Panel derecho se puebla:
     - `PortraitImage` cambia al sprite del warrior asset.
     - `HeaderLabel` muestra `"Contrato del Guerrero"`.
     - `RowsContainer` tiene **8 rows** — Par 10, Doble Par 18, Suma X 25, Trio 28,
       Escalera 35, Full House 40, Poker 60, Generala 100 (valores del Sprint03
       doc #97, asumiendo los `BaseComboSO._baseDamage` estan seteados en los
       assets de T97a).
     - `PassiveLabel` dice `"Pasiva: TBD"`.
   - `WarriorSelectionIndicator` se activa (outline/check visible).
   - Boton Confirmar **habilitado**.
6. Click **Confirmar**:
   - Consola: `"[ClassSelectionScreen] Run start. heroId=hero.warrior, runId=<guid>, next=BuildSelectionScreen"`.
   - Consola: warning `"[ScreenManager] 'BuildSelectionScreen' no esta registrada... Fallback graceful..."`
     — **esperado** (stub hasta que T-build mergee).
   - **No hay crash.** El usuario queda en `ClassSelectionScreen`.
7. Si tenes un listener de test suscripto a `EventName.OnRunStart`, deberia recibir
   `[Guid, "default"]`.

---

## 8.11 Troubleshooting

| Sintoma | Causa | Fix |
|---|---|---|
| Click Jugar → warning `"'ClassSelectionScreen' no esta registrada"` | El GameObject no es hijo del Canvas del `ScreenHost`, o el `ScreenHost` no tiene `includeInactive: true` al registrar screens. | Verificar jerarquia: `ClassSelectionScreen` debe ser hijo del mismo Canvas que `MainMenuScreen`. Verificar `ScreenHost.Awake()` usa `GetComponentsInChildren<BaseScreen>(includeInactive: true)`. |
| Panel derecho vacio tras clickear Guerrero | `_warriorHero` no cableado. | Inspector → arrastrar `ClassHero_Warrior.asset`. |
| Contract table vacia | `_warriorHero.Sheet.Combos` vacio o null. | Poblar los 8 refs en el asset (§8.2.3) o correr `ContractWarriorFactory.Build(catalog)`. |
| Contract con 8 rows pero nombres/daños vacios | Cada `BaseComboSO` concreto no tiene `_displayName` / `_baseDamage` seteado. | Editar cada `.asset` de combo (T97a) y setear esos campos. |
| Confirm no dispara OnRunStart | `_confirmButton` no cableado, o el click tiene listener extra via Inspector. | Verificar wiring §8.6. Borrar listeners en el Inspector del Button — los handlers van por codigo. |
| Confirm no navega y no aparece warning | `IScreenManager` no esta registrado. | Verificar que existe un `ScreenHost` en la escena (lo pone T102). |
| Warning `_warriorHero._baseMaxHp no inicializado` | Falso positivo — esos campos son `[STUB]` de Hero Template. | Ignorar. No se consume en este screen. |
| Double click enciende el Confirm 2 veces | Listener cableado en Inspector + en codigo. | Borrar el `OnClick()` del Inspector (el codigo lo suscribe en `OnPushed`). |

---

## 8.12 Definicion de Done

- [ ] `ClassHero_Warrior.asset` existe con los 8 combos poblados y `_displayLabel` seteado.
- [ ] `ClassSelectionScreen` es hija del Canvas de `01_MainMenu.unity` y el `ScreenHost`
      la detecta (no hay warning `"no esta registrada"`).
- [ ] `ComboRow.prefab` existe en `Assets/Rollgeon/Prefabs/UI/` y esta asignado al
      `ContractDisplayView._rowPrefab`.
- [ ] Play desde `00_Bootstrap` → Jugar → panel de seleccion con 3 botones + panel vacio.
- [ ] Click Guerrero → panel derecho poblado con 8 rows + Confirm habilitado.
- [ ] Botones Mago + Picaro grayed-out (no clickables) con LockIcon visible.
- [ ] Click Confirm → log `"Run start..."` + warning graceful de `BuildSelectionScreen`.
- [ ] `Rollgeon.UI.Tests` pasa 100% incluyendo `ClassSelectionScreenTests`.
- [ ] Cero `.unity`/`.prefab`/`.asset` commiteados desde el dev — los crea el usuario
      siguiendo este instructivo.
