# Setup — System#0301 Playable combat checklist

> **Audiencia:** el usuario del proyecto, tras mergear §10 feedback manager
> (System#0300) y tener listo el resto del código de combate.
> **Tiempo estimado:** 1–2h si los docs previos ya están aplicados.

Este doc es un **índice de verificación** para tener combate jugable end-to-end.
No introduce setup nuevo — apunta a los docs ya escritos que, juntos, cubren
todo el stack. Per `CLAUDE.md`, lo que sigue lo hace el usuario a mano.

---

## 0. Orden de aplicación (esperado)

Si no está hecho todavía, seguir **en este orden** y no saltearse pasos —
cada doc asume los anteriores:

1. `docs/setup/Foundation#0001_ServiceLocatorEventManager.md`
2. `docs/setup/Foundation#0005_CatalogsAndBootstrap.md` → crear `ServiceBootstrap.asset`
3. `docs/setup/Foundation#0006_PlayerServiceReal.md`
4. `docs/setup/Foundation#0007_PhaseServiceReal.md`
5. `docs/setup/Foundation#0008_DamagePipeline.md`
6. `docs/setup/Foundation#0009_HealPipeline.md`
7. `docs/setup/System#0100a_EnergyAttributeAndRegen.md`
8. `docs/setup/System#0100b_ActionEconomyRepetition.md`
9. `docs/setup/System#0100c_TurnOrderHiddenSpeed.md`
10. `docs/setup/System#0100d_CombatTurnFSM.md`
11. `docs/setup/System#0012a_CombatScreenAndHandoff.md`
12. `docs/setup/System#0012b_EnemyAIReal.md`
13. `docs/setup/System#0012d_CombatEndToExploration.md`
14. `docs/setup/System#0201_GridFoundation.md`
15. `docs/setup/System#0203_EntityVisuals.md`
16. `docs/setup/UI#0095b_CombatHUD.md`
17. `docs/setup/UI#0013c_VictoryDefeatScreens.md`
18. `docs/setup/_PLAYABLE_LOOP_TWO_SCENE_SETUP.md` — arquitectura de 3 escenas
19. **`docs/setup/System#0300_FeedbackManagerWiring.md`** ← nuevo, este PR

---

## 1. Checklist final antes de dar Play

### 1.1 `ServiceBootstrap.asset`

- [ ] `Catalogs` tiene `ActionCatalog`, `ComboCatalog`, `EnemyCatalog`
- [ ] `SettingsAssets` tiene `RulesetSO`, `AudioSettings`, etc.
- [ ] `ExtraServices` tiene al menos:
  - `EnergyServiceBootstrap` (Priority 50)
  - `TurnManagerBootstrap` (Priority 60)
  - `PawnRegistryBootstrap` (Priority 20)
  - **`FeedbackManagerBootstrap` (Priority 55)** ← este PR
  - `PlayerServiceBootstrap`, `PhaseServiceBootstrap`, `DamagePipelineBootstrap`,
    `HealPipelineBootstrap`, etc. según docs previos
- [ ] `FeedbackServiceStubBootstrap` **removido** (el real lo reemplaza)
- [ ] `_nextSceneName = "01_MainMenu"`

### 1.2 `FeedbackDB.asset`

- [ ] Al menos las entries mínimas del doc #0300 §1 (`hit.basic`, `heal.basic`,
      `hit.impulse`, `sfx.hit`)
- [ ] `Assets/Resources/FloatingNumber.prefab` existe y tiene `FloatingNumberView`

### 1.3 Prefabs de pawn (héroe + cada enemigo)

- [ ] Root visual tiene `PawnRegistryBinding`
- [ ] Root visual tiene `HitImpulseConsumer` (opcional pero recomendado)
- [ ] El `Animator` (si hay) tiene los triggers que referencian las entries
      `FeedbackType.Animation`
- [ ] El spawner de combate llama `binding.SetGuid(entity.Guid)` justo después
      de `Instantiate`

### 1.4 Escenas

- [ ] `00_Bootstrap.unity`: `BootstrapRunner` apuntando al `ServiceBootstrap`
- [ ] `01_MainMenu.unity`: ScreenHost con `_initialScreenStringId = "MainMenu"`
- [ ] `02_Gameplay.unity`: `CombatController` + `GameplayBootstrapper` +
      `ScreenHost` + HUDs del doc `_PLAYABLE_LOOP_TWO_SCENE_SETUP.md`

### 1.5 `ActionDefinitionSO` de prueba

Crear al menos una action `BasicAttack` con este effect chain para ver feedback
en acción:

1. `EffDealDamage` (BaseAmount 10, AttackKind BasicAttack)
2. `EffApplyImpulse` (vector `(0, 0, 0.5)`)
3. `EffPlayFeedback` (FeedbackId `hit.basic`)
4. `EffPlayFeedback` (FeedbackId `hit.impulse`)

Asignar esta action al HeroActionBehavior del hero sheet.

---

## 2. Smoke test

1. Abrir `00_Bootstrap.unity` y Play.
2. MainMenu → ClassSelection → BuildSelection → Exploration → entrar a un combate.
3. Activar `BasicAttack`. Verificar:
   - Número de damage rojo flota sobre el enemigo.
   - Enemigo hace pequeño knockback visual (si tiene `HitImpulseConsumer`).
   - Se escucha el SFX (si la entry lo tiene).
   - Log: ningún warning de `IFeedbackService no registrado`,
     `FeedbackManager.timed out`, `id not found`.
4. Matar al enemigo → Victory screen → Return to Menu.
5. Otro intento con otro hero → otro combate → derrota → Defeat screen.

---

## 3. Si algo falla

- Consola llena de `NullReferenceException`: probablemente falta un servicio
  en `ExtraServices`. Revisar el orden de Priority.
- Feedback no aparece: ver troubleshooting en `System#0300` §7.
- Combate no arranca: revisar `System#0100d_CombatTurnFSM.md` + handoff docs.
- Enemigo no ataca: `System#0012b_EnemyAIReal.md`.

---

## 4. Qué queda fuera de scope

El código mergeado cubre el **runtime** del pipeline §10 + el combate básico,
pero no agrega:

- Una action `BasicAttack` de ejemplo (se crea a mano en el inspector).
- Prefabs de héroe / enemigo con arte real (Content#0099 entrega stubs, el
  arte final es otro ticket).
- Entries del `FeedbackDBSO` con VFX/SFX reales (lo llena el diseñador en
  iteraciones).
- Integración con `IAudioService` (no existe — hoy `SFX` rutea por
  `AudioSource.PlayClipAtPoint`). Ver `TECHNICAL.md §A` para cuando exista.
