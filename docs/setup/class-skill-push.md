# Habilidad de Clase — Empuje del Guerrero (Feature#0055)

> Estado al 2026-08-26: implementado y cableado vía editor tool + Unity MCP en
> `Feature#0055_WarriorClassSkill`. Spec: GDD DocsApp → Combat System § "Habilidad de Clase,
> Empuje del Guerrero" + Turn System (tabla de acciones). TECHNICAL.md §12.8.
>
> **Qué es**: reemplaza al viejo *Ataque especial / a rango* del Guerrero. Botón propio (slot 2),
> cuesta 1 roll, exige adyacencia con un enemigo, se compromete **antes** de ver el combo y traduce el
> combo del Contrato de Generala a **casillas de empuje** (tabla propia, no escala con daño). Sin
> combo no hay efecto y el roll se pierde. Primera Habilidad de Clase: cada clase futura trae la suya.

## El modelo

- **Slot**: `HeroBehaviorSlot.ClassSkill = 2` (ex `SpecialAttack`, mismo valor — prefab y SOs
  serializan el int, no hubo migración). Hotkey `E` (`GameplayHotkey.ClassSkill`, acción
  `ClassSkill` en `InputSystem_Actions`). Localización: `action.class_skill`.
- **Kind**: `RollActionKind.ClassSkill` — **no** es `IsCombatPayable` (los encantamientos de oro y
  los hooks de ítem filtrados por Attack no se disparan con un empuje). Es un cambio de 1 línea en
  `RollActionKindExtensions.IsCombatPayable` si diseño quiere lo contrario.
- **Tabla**: `ClassSkillPushTableSO` (`Create → Rollgeon → Heroes → Class Skill Push Table`).
  Asset del Guerrero: `Assets/Rollgeon/Classes/ClassSkillPushTable_Warrior.asset`.

  | Combo | Id | Casillas | Estado |
  |---|---|---|---|
  | Par | `combo.pair` | 1 | Confirmado |
  | Doble Par | `combo.double_pair` | 1 | Provisional |
  | Suma 4 | `combo.higher_number` | 2 | Provisional |
  | Trío | `combo.trio` | 2 | Confirmado |
  | Full House | `combo.full_house` | 3 | Provisional |
  | Escalera | `combo.ladder` | 3 | Provisional |
  | Póker | `combo.poker` | 4 | Provisional |
  | Generala | `combo.generala` | 5 | Confirmado |

  `CollisionDamage = 10` (el GDD no lo especifica — decisión del 2026-08-26, data-only).
  `Fuerza Bruta` no tiene entrada ⇒ 0 casillas. Botón **Reset to Spec** en el inspector.
- **Efecto**: `EffClassSkillPush` (`Effects/Concretes`), hoja dentro de la fase `Push` del `EffChain`
  del behavior. Selección `Occupied + BeforeRoll + Enemies + Range 1 + Manhattan + Single`.
  `ApplyEffect` **siempre devuelve `true`**: sin combo loguea y no hace nada (el roll ya se cobró).
- **Resolver**: `IClassSkillPushResolver` (`Combat/Skills/Push`, `ClassSkillPushResolverBootstrap`,
  priority 82, en `ServiceBootstrap.ExtraServices`). `Resolve(pusher, target, distance,
  collisionDamage, stunTurns = 1)` → `PushOutcome` (lista de `PushHop`, logueada en consola).

## Flujo de combate

1. Click en el chip (o `E`) → el gate exige un enemigo a Manhattan 1 (`HasUsableEffectGroup`; sin
   rango el toast dice "Sin rango al objetivo").
2. Click en el enemigo → `SpendRollForThrow` cobra 1 roll → mesa de dados con skin Attack.
3. Rerolls como Ataque (1 roll cada uno). Confirmar → `ExecuteChainPhase` matchea el combo.
4. `EffClassSkillPush`: `tiles = tabla.GetTiles(combo)`; 0 ⇒ fin (roll perdido). Si no,
   `resolver.Resolve(player, enemigo, tiles, 10)`.
5. El resolver empuja con `IForcedMovementService.Push` (casillas especiales, hielo, portal siguen
   funcionando) y clasifica el choque con `ForcedMoveResult.BlockerGuid` (nuevo campo, capturado
   antes de la reubicación de portal):
   - **Pared / borde / prop sin vida / cofre** → `IStunService.ApplyStun(empujado, 1)`: el enemigo
     saltea su próximo turno (`StunTurnSkipper`).
   - **Objeto de sala rompible** (`RoomObjectDefinitionSO`, ej. dados de La Generala, bombas del
     Croupier) → el empujado recibe `CollisionDamage`, el objeto recibe daño letal por el
     `DamagePipeline` y se rompe (death watcher lo saca del grid; `OnDeathHazard` sigue saliendo).
     El empujado **no** avanza a la celda liberada.
   - **Otro enemigo** → **ambos** reciben `CollisionDamage`; si el segundo sigue vivo y queda
     distancia (`tiles − recorridas`), se lo empuja con las mismas reglas (cadena, misma dirección).
   - Muerte a mitad del recorrido (pinchos, fuego) → sin choque. Guardas: una entidad se empuja una
     sola vez por resolución, profundidad máxima 16.
6. La fase cierra (`FinishChain`), el HUD se desbloquea; el turno no termina (quedan rolls ⇒ más
   acciones).

Daño de choque: `AttackKind.Environmental`, `SourceId = player` (crédito de kill al jugador), sin
`ComboId` (sin weakness ni bonos planos de ataque). Los números flotantes salen solos
(`FloatingDamageSpawner` escucha `DamageResolvedPayload`).

## UI

- Chip `CombatHUDView/PlayerActionButtonsView/ClassSkillButton` (`_slot: 2`, `_buttons[2]`), mismo
  layout que antes. Icono: **placeholder** (el del viejo Special Attack) — follow-up de arte.
- `DamageFormulaView`: `"{combo}: empuja N"` o `"Empuje - sin combo: sin efecto"` (keys
  `formula.push.preview` / `formula.push.no_combo`). Sin N×M breakdown (la fase no tiene
  `EffDealDamage`/`EffAddShield`, el announcer no emite nada).
- Tooltip del chip: `HeroActionTooltip` → `action.class_skill` + tooltip del efecto (tabla completa +
  "Sin combo: la tirada se pierde sin efecto").
- Tutorial: el slot sigue **bloqueado** (`TutorialActionGateService`); no hay paso de enseñanza.

## Wiring (aplicado 2026-08-26)

1. **Editor tool** `Rollgeon → Heroes → Install Warrior Class Skill`
   (`Assets/Scripts/Editor/Tools/Heroes/WarriorClassSkillInstaller.cs`, idempotente):
   - crea `ClassSkillPushTable_Warrior.asset` con la spec (respeta una tabla ya autorada);
   - crea `Assets/Rollgeon/Combat/ClassSkillPushResolverBootstrap.asset` y lo agrega a
     `Assets/Rollgeon/ServiceBootstrap.asset → ExtraServices`;
   - en `CH_Warrior.asset` y `Assets/Rollgeon/Tutorial/CH_Warrior_Tutorial.asset`: behavior base
     del slot 2 → `ActionName = "Class Skill"`, `NeedsDiceRoll`, `AllowsReroll`, `BoardType = Attack`;
     reemplaza in-place cada `EffDealDamage` del árbol (fase del `EffChain` → step `InlineEffect` del
     `EffPlaySequence`) por `EffClassSkillPush`; renombra la fase a `Push`; y reconfigura a
     adyacencia (Range 1, Enemies) **todas** las selecciones pre-roll del árbol. **Gotcha**: la
     selección que gatea el botón y apunta el target es la primera `BeforeRoll` de la fase 0
     (`EffChain.FindPhaseSelectionAt`) — en el Warrior es la del `EffPlaySequence` que envuelve al
     efecto, no la del efecto; sin este paso el chip seguía apuntando a rango 4.
   - **Los `ClassHeroSO` son Odin**: editar el YAML a mano no round-tripea. Re-correr el tool si hay
     que reautorar. Verificar siempre `git diff Assets/Rollgeon/ServiceBootstrap.asset` (una sola
     entrada nueva en cada lista; un editor stale puede tirar refs).
2. **Prefab** `Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab` (vía MCP `manage_prefabs`):
   `SpecialAttackButton` → `ClassSkillButton`; `UITooltipTrigger._previewText` actualizado.
3. **Localización**: `Rollgeon → Localization → Seed Content + UI` re-corrido (`action.class_skill`,
   `tooltip.effect.push.*`, `formula.push.*`). La key vieja `action.special_attack` se eliminó de la
   colección `UI` (API de Localization desde el editor).
4. **Restos del viejo ataque especial / a rango eliminados**: `AD_AttackSpecial.asset` borrado y
   quitado de `ActionCatalog.asset` (catálogo `ActionCatalogSO`, dormido — sin consumidores runtime);
   handlers `OnHotkeySpecial` → `OnHotkeyClassSkill`; fixtures y comentarios renombrados. Lo único que
   conserva el nombre es la nota histórica en `HeroBehaviorSlot.cs` ("ex SpecialAttack").

Para otra clase: crear su `ClassSkillPushTableSO` (o un efecto distinto), autorar el behavior base del
slot 2 con `IsBaseBehavior = true` y `Slot = ClassSkill`. **Gotcha**: `GetBehaviorsForPhase` compacta
índices — el HUD asume que los slots 0 y 1 existen en combate para que el chip 2 caiga en el índice 2.

## Semántica de cancel / sin combo

- Antes de elegir objetivo: cancelar es gratis (click derecho / otro chip), como Ataque.
- Con objetivo elegido el roll ya está cobrado. Sin combo ⇒ ningún efecto, sin reembolso, sin piso
  de dado más alto. La fórmula lo avisa antes de confirmar.
- Un combo prohibido por contrato de jefe (`DetectWithContractMods` → `NoMatch`) ⇒ sin empuje.

## Tests

- `Tiles/Tests/SpecialTileChainTests.cs` — `BlockerGuid`/`BlockedAt` en `Push_StopsBeforeObstacle`,
  `Push_IntoEdge_BlockerGuidIsEmpty`.
- `Combat/Skills/Tests/ClassSkillPushResolverTests.cs` — pared/prop/cofre ⇒ stun; objeto de sala ⇒
  daño + rompe; enemigo ⇒ ambos dañados + remanente encadenado; cadena contra pared; bloqueador muere
  ⇒ sin 2º eslabón; pinchos matan a mitad ⇒ sin choque; empujado muere pero el bloqueador igual se
  empuja; guarda anti-loop. (Sin caso de hielo/portal — follow-up.)
- Smoke en Play Mode (MCP, 2026-08-26): servicios registrados, selección efectiva `Range 1 / Enemies`,
  empuje libre (2,0)→(4,0), pared ⇒ stun 1, cadena ⇒ ambos −10 y el 2º recorre el remanente. El flujo
  de UI (click chip → target → mesa → confirmar) comparte el path del Base Attack; queda para QA manual.
- `Heroes/Tests/ClassSkillPushTableSOTests.cs` — 8 valores de la spec, ids desconocidos ⇒ 0,
  `CollisionDamage == 10`. `HeroBehaviorSlotTests` (valor 2),
  `HeroActionBehaviorRollActionKindTests` (`ClassSkill` no pagable).
- `Effects/Tests/EffClassSkillPushTests.cs` — sin combo / sin tabla / sin resolver ⇒ `true` sin
  empuje; Par ⇒ 1, Generala ⇒ 5; fallback a `TargetGuid`. Gating de adyacencia y regresión "sin
  breakdown" en `Effects/Tests`.

## Follow-ups (fuera de alcance)

- Timing visual: el tween del pawn empujado no está trackeado por el `TurnManager` (igual que el
  empujón del Cajero) — el HUD se desbloquea mientras el pawn se desliza y los números de choque
  salen en la posición actual del pawn. Candidato: `FeedbackRequest` de impacto.
- Sin visual de stun para enemigos (`OnStunApplied` solo lo consume `PlayerStatusIconsView`).
- Skin `DiceBoardType.ClassSkill`, icono definitivo del chip, paso de tutorial.
- ¿Debe una tirada de empuje pagar encantamientos de oro? (`IsCombatPayable`).
