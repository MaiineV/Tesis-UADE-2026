namespace Patterns
{
    /// <summary>
    /// Familia mínima de eventos del bus legacy <see cref="EventManager"/>.
    /// Schema definido en TECHNICAL.md §1.2 (líneas 500–621).
    /// <para>
    /// <b>Regla transversal de <c>args[0]</c>.</b> Para todo evento cuyo payload referencie a una
    /// entidad concreta, <c>args[0]</c> es <b>siempre</b> <see cref="System.Guid"/> — el
    /// <c>InstanceId</c> de la entidad primaria. Nunca se pasa <c>Entity</c> ni
    /// <c>MonoBehaviour</c>. Ver TECHNICAL.md §1.2 línea 525.
    /// </para>
    /// <para>
    /// <b>Regla de canal único.</b> <c>OnDamageResolved</c>, <c>OnHealthChanged</c> y
    /// <c>OnComboMatched</c> viven únicamente como <see cref="TypedEvent{T}"/>
    /// (<c>DamageResolvedPayload</c>, <c>HealthChangedPayload</c>, <c>ComboMatchedPayload</c>).
    /// No tienen entry en este enum por diseño. Ver TECHNICAL.md §1.2.1.
    /// </para>
    /// <para>
    /// <b>No presente por diseño:</b> <c>OnScreenPushed</c>, <c>OnScreenPopped</c>,
    /// <c>OnPauseChanged</c> viven en <c>IScreenManager</c> (§17.D), no en el bus legacy.
    /// </para>
    /// </summary>
    public enum EventName
    {
        // --- Run lifecycle ------------------------------------------------------
        /// <summary>args: [Guid runId, string rulesetId]</summary>
        OnRunStart,
        /// <summary>args: [Guid runId, RunOutcome outcome]</summary>
        OnRunEnd,
        /// <summary>args: [Guid runId]. La run se ganó: el player tomó la salida del piso
        /// terminal (FloorLayoutSO.NextFloor == null). Lo consume VictoryScreen (#158).</summary>
        OnRunVictory,

        // --- Combat lifecycle ---------------------------------------------------
        /// <summary>args: [Guid roomInstanceId]</summary>
        OnCombatStart,
        /// <summary>args: [Guid roomInstanceId, CombatOutcome outcome]</summary>
        OnCombatEnd,
        /// <summary>args: [Guid runId]</summary>
        OnPlayerDefeated,
        /// <summary>args: [Guid playerGuid, Guid targetGuid]. El jugador eligió (o limpió) el
        /// enemigo objetivo del ataque antes de tirar. targetGuid == Guid.Empty significa
        /// "sin target". Lo consume el HUD para previsualizar la mitigación real (weakness +
        /// escudo del enemigo apuntado) en la fórmula de daño.</summary>
        OnCombatTargetChanged,

        // --- Damage pipeline ----------------------------------------------------
        /// <summary>args: [Guid sourceGuid, Guid targetGuid, int baseDamage]</summary>
        OnDamageOutgoing,
        /// <summary>args: [Guid sourceGuid, Guid targetGuid, int incomingDamage]</summary>
        OnDamageIncoming,
        // OnDamageResolved NO existe acá — vive como TypedEvent<DamageResolvedPayload>.

        // --- Turn / initiative --------------------------------------------------
        /// <summary>args: [Guid entityGuid]. Quien arranca su turno.</summary>
        OnTurnStarted,
        /// <summary>args: [Guid entityGuid]. Quien cierra su turno — lo consume Modifier&lt;T&gt; para decrementar Duration.</summary>
        OnTurnFinished,
        /// <summary>args: [IReadOnlyList&lt;Guid&gt; orderForRound, int roundIndex]</summary>
        OnTurnQueueBuilt,

        // --- Phase lifecycle (§12.0) --------------------------------------------
        /// <summary>args: [GamePhase exiting]</summary>
        OnPhaseExit,
        /// <summary>args: [GamePhase entering]</summary>
        OnPhaseEnter,
        /// <summary>args: [PhaseOverlay overlay]</summary>
        OnOverlayPushed,
        /// <summary>args: [PhaseOverlay overlay]</summary>
        OnOverlayPopped,

        // --- Roll ---------------------------------------------------------------
        /// <summary>args: [Guid sourceGuid]. Se dispara al iniciar una tirada.</summary>
        OnRollStarted,
        /// <summary>args: [Guid sourceGuid, IReadOnlyList&lt;int&gt; faces]. Resultado crudo de los dados tirados.</summary>
        OnDiceRolled,
        /// <summary>args: [Guid sourceGuid, int rerollIndex]. Se dispara al iniciar un reroll.</summary>
        OnRerollStarted,
        /// <summary>args: [Guid sourceGuid, IReadOnlyList&lt;int&gt; finalFaces]. Roll finalizado y lockeado, tras rerolls.</summary>
        OnRollResolved,

        // --- Chain -----------------------------------------------------------------
        /// <summary>args: [Guid sourceGuid]. Una accion con EffChain fue seleccionada
        /// y el chain quedo activo (antes del primer roll). La UI lo usa para mantener
        /// los botones de behavior lockeados entre fases del chain.</summary>
        OnChainStarted,
        /// <summary>args: [Guid sourceGuid, int phaseIndex, int totalPhases]</summary>
        OnChainPhaseStarted,
        /// <summary>args: [Guid sourceGuid, int phasesCompleted, int totalPhases, bool wasPass]</summary>
        OnChainCompleted,
        /// <summary>args: [Guid sourceGuid]. Una fase del chain abrió una selección
        /// INTERACTIVA de target (post-confirm) — el jugador tiene que clickear al
        /// enemigo. No dispara en fases Self/AutoResolve/sin targets.</summary>
        OnChainTargetSelectionStarted,
        /// <summary>args: [Guid sourceGuid]. Una accion sin tirada (ej. Movement) quedo
        /// comprometida y esta esperando que el jugador elija el tile target. La accion ya
        /// se cobro y se ejecuta de forma asincrona al clickear el destino; mientras tanto
        /// la UI debe lockear los demas slots para impedir iniciar otra accion en paralelo
        /// (BUG-013). El lock se libera con el <see cref="OnBehaviorExecuted"/> que dispara
        /// la accion al completarse.</summary>
        OnActionSelectionStarted,
        /// <summary>args: [Guid sourceGuid, string actionName].
        /// El behavior termino de ejecutarse en el turno (sea simple o chain). La UI
        /// lo usa para liberar el slot seleccionado y recomputar los botones.</summary>
        OnBehaviorExecuted,

        // --- Combat resolve -----------------------------------------------------
        // OnHealthChanged NO existe acá — vive como TypedEvent<HealthChangedPayload>.
        /// <summary>args: [Guid entityGuid, int currentShield]</summary>
        OnShieldChanged,
        /// <summary>args: [Guid entityGuid, Guid sourceGuid]. Entidad destruida por source.</summary>
        OnEntityDestroyed,

        // --- Contract -----------------------------------------------------------
        // OnComboMatched NO existe acá — vive como TypedEvent<ComboMatchedPayload>.
        /// <summary>args: [Guid sourceGuid, string comboId]. Combo strikable crossed.</summary>
        OnComboCrossed,
        /// <summary>args: [Guid sourceGuid, Guid targetGuid]. Se pegó contra una debilidad.</summary>
        OnWeaknessHit,
        /// <summary>args: [string comboId, int durationTurns]. Boss FloorManager bloquea un combo del ContractSheet. [T103]</summary>
        OnComboBlocked,
        /// <summary>args: [string comboId]. Bloqueo expirado (duration llego a 0). [T103]</summary>
        OnComboUnblocked,
        /// <summary>args: [string comboId, int newCount]. Contador run-scoped de un combo incrementado (§5.5 — T97c).</summary>
        OnComboCounterIncremented,
        /// <summary>args: [Guid playerGuid, int used, int cap]. Estado del reroll budget cambió (T104 extensión — opcional, dispara si el servicio lo emite).</summary>
        OnRerollBudgetChanged,

        // --- Bosses: sistemas prerequisito --------------------------------------
        /// <summary>args: [Guid sourceGuid]. El Boss marcó un área telegráfica (turno N). Hook para VFX/SFX de advertencia.</summary>
        OnThreatenedAreaMarked,
        /// <summary>args: [Guid sourceGuid, bool hit]. Se ejecutó el área telegráfica (turno N+1); hit=true si el jugador estaba dentro.</summary>
        OnThreatenedAreaResolved,
        /// <summary>args: [Guid playerGuid]. Cambió el conjunto de dados bloqueados del jugador — la UI re-lee IDiceBlockService.</summary>
        OnDiceBlockChanged,
        /// <summary>args: []. Cambió la capa de modificadores del Contrato (Boss 3) — la UI del Contrato re-lee los valores efectivos.</summary>
        OnContractModifierChanged,
        /// <summary>args: [Guid bossGuid, int phaseIndex]. El Boss cruzó un umbral de fase (1-based). Hook para feedback visual + diálogo.</summary>
        OnBossPhaseChanged,
        /// <summary>OBSOLETO — nadie lo dispara ni lo escucha. Era del pasivo global
        /// anti-repetición (A/B Combo/Dice), eliminado. <b>NO borrar el miembro:</b> Odin
        /// serializa los enums por su int y sacarlo del medio shiftearía los valores de todos
        /// los de abajo, rompiendo en silencio los assets que los tengan guardados (ya pasó con
        /// la pasiva del guerrero). Reutilizable si vuelve una mecánica parecida.</summary>
        OnAntiRepeatModeChanged,

        // --- Modifier / attributes ---------------------------------------------
        /// <summary>args: [Guid entityId, Type attributeType]. Notifica que un atributo
        /// de la entidad cambió su valor calculado (consumido por Foundation#0003 Attributes + Modifiers).</summary>
        OnAttributeChanged,
        /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
        OnModifierAdded,
        /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
        OnModifierRemoved,

        // --- Dungeon ------------------------------------------------------------
        /// <summary>args: [Guid roomInstanceId, string roomId]</summary>
        OnRoomEntered,
        /// <summary>args: [Guid roomInstanceId]</summary>
        OnRoomCleared,
        /// <summary>args: [Guid runId, int floorIndex]</summary>
        OnFloorCleared,
        /// <summary>args: [Guid runId, int newFloorIndex]. Fired by RunContext.AdvanceFloor().</summary>
        OnFloorChanged,
        /// <summary>args: [Guid roomInstanceId]. El player activó una puerta de salida física
        /// (caminó al tile de salida). Lo consume FloorProgressionService para transicionar
        /// al siguiente piso (#158).</summary>
        OnFloorExitRequested,

        // --- HUD bindings (le hablan al §D ScreenManager) ----------------------
        /// <summary>args: [Guid entityGuid, int current, int max]</summary>
        OnPlayerHealthChanged,
        /// <summary>args: [Guid entityGuid, int current, int max]. Pool de Rolls del
        /// jugador (Feature#0050) — reemplazó a OnPlayerEnergyChanged.</summary>
        OnPlayerRollsChanged,
        /// <summary>args: [int current, int delta]</summary>
        OnGoldChanged,
        /// <summary>args: [Guid targetGuid, FloatingNumberType type, float value, Vector3 offset]</summary>
        OnFloatingNumberRequested,

        // --- Craps --------------------------------------------------------------
        /// <summary>args: [Guid sessionId, Guid playerGuid]</summary>
        OnCrapsSessionStarted,
        /// <summary>args: [Guid sessionId, string comboId, int stake]</summary>
        OnCrapsBetPlaced,
        /// <summary>args: [Guid sessionId, CrapsOutcome outcome, int payout]</summary>
        OnCrapsResolved,

        // --- Save (§15) ---------------------------------------------------------
        /// <summary>args: []. Request global para que cada servicio que implementa captura
        /// serialice su estado al contenedor de save activo.</summary>
        OnCaptureRequested,
        /// <summary>args: []. Se dispara cuando la restauración de save terminó de hidratar a todos los servicios.</summary>
        OnRestoreCompleted,

        // --- Feedback -----------------------------------------------------------
        /// <summary>args: [Guid instanceId, string feedbackId]. Un feedback comenzó a ejecutarse.</summary>
        OnFeedbackStarted,
        /// <summary>args: [Guid instanceId, string feedbackId]. Un feedback terminó su ciclo de vida.</summary>
        OnFeedbackCompleted,

        // --- Interaction (§7.7) ------------------------------------------------
        /// <summary>args: [Guid targetGuid, string resolvedLabel, bool isAvailable].
        /// targetGuid == Guid.Empty significa "no hay target, esconder el prompt".
        /// resolvedLabel es el LocalizedString del label ya resuelto por el
        /// LocalizationManager. isAvailable == false =&gt; prompt grayed out.</summary>
        OnInteractionTargetChanged,
        /// <summary>args: [Guid targetGuid]</summary>
        OnInteractionExecuted,

        // --- Shop (§17.F) ------------------------------------------------------
        /// <summary>args: [bool hasTarget, string itemName, string description, int price, Sprite icon].
        /// hasTarget == false → esconder el ItemInspectView.</summary>
        OnShopItemTargetChanged,
        /// <summary>args: [string spawnPointId, string rewardId, int pricePaid]</summary>
        OnShopItemPurchased,
        /// <summary>args: [Guid roomInstanceId, int slotsRestocked]</summary>
        OnShopRestocked,

        // --- Status (§20) ------------------------------------------------------
        /// <summary>args: [Guid targetGuid, StatusEffectSO status, int stacks]</summary>
        OnStatusApplied,
        /// <summary>args: [Guid targetGuid, StatusEffectSO status]</summary>
        OnStatusRemoved,
        /// <summary>args: [Guid targetGuid, StatusEffectSO status, int deltaAmount]. deltaAmount = damage/heal aplicado en el tick.</summary>
        OnStatusTicked,

        // --- Items (§18) -------------------------------------------------------
        /// <summary>args: [Guid ownerGuid, string itemId]. Cubre pickups y upgrades (payload uniforme).</summary>
        OnItemObtained,
        /// <summary>args: [Guid ownerGuid, string itemId]</summary>
        OnItemRemoved,
        /// <summary>args: [Guid sourceGuid, string itemId]. Se usó un item activo.</summary>
        OnActiveItemUsed,

        // --- Quest (§21) -------------------------------------------------------
        /// <summary>args: [string questId, QuestState state]</summary>
        OnQuestStateChanged,

        // --- Exploration -------------------------------------------------------
        /// <summary>args: [Guid runId]</summary>
        OnExplorationStarted,
        /// <summary>args: [Guid roomInstanceId, string roomId, RoomType roomType]</summary>
        OnCombatTriggered,

        // --- Scene (§K) --------------------------------------------------------
        /// <summary>args: [string sceneName]. Scene aditiva terminó de cargar.</summary>
        OnSceneLoaded,
        /// <summary>args: [string sceneName]. Scene aditiva terminó de descargarse.</summary>
        OnSceneUnloaded,

        // --- Upgrades / Enchantments -------------------------------------------
        /// <summary>args: [Guid playerGuid, string enchantmentId, int bagIndex, int enchSlotIndex]. Encantamiento aplicado a un cupo del dado.</summary>
        OnEnchantmentApplied,
        /// <summary>args: [Guid playerGuid, string enchantmentId, int bagIndex, int enchSlotIndex]. Encantamiento removido (manual o por trigger self-destruct).</summary>
        OnEnchantmentRemoved,
        /// <summary>args: [Guid playerGuid, Guid roomInstanceId, int baseCost]. El player presionó interact sobre el altar — la UI debe abrir la pantalla de selección de dado/slot.</summary>
        OnEnchantmentAltarActivated,
        /// <summary>args: []. La pantalla del altar se cerró — el altar puede volver a mostrar su prompt de interacción.</summary>
        OnEnchantmentAltarClosed,

        // --- Camera (§17.E) ----------------------------------------------------
        /// <summary>args: [Rollgeon.Camera.CameraFacing newFacing]. Yaw discreto cambió tras un RotateBy45 (§17.E.5).</summary>
        OnCameraFacingChanged,
        /// <summary>args: [bool enabled]. Cruce del umbral de floor view (§17.E.9). true = shells visibles, sala actual hidden.</summary>
        OnCameraFloorViewToggled,
        /// <summary>args: [bool instant]. Cámara hizo recenter — instant omite el tween (§17.E.6.4).</summary>
        OnCameraRecentered,
        /// <summary>args: [float amplitude, float durationSeconds]. Feedback pide un camera shake; el CameraService lo consume (§17.E.10, TODO v8).</summary>
        OnCameraShakeRequested,

        // --- Tutorial ------------------------------------------------------------
        /// <summary>args: [Rollgeon.Heroes.HeroBehaviorSlot slot]. El tutorial desbloqueó una acción — los HUDs recomputan estados de botones.</summary>
        OnTutorialActionUnlocked,
        /// <summary>args: [Rollgeon.Heroes.HeroBehaviorSlot slot]. El jugador clickeó (y seleccionó efectivamente) un botón de acción del HUD de combate — el tutorial encadena el paso siguiente (p.e. señalar los dados).</summary>
        OnHeroBehaviorClicked,

        // --- Combat: refuerzos --------------------------------------------------
        // NOTA: agregar SIEMPRE al final del enum. EventName se serializa por VALOR en assets
        // Odin (ej. PassiveHook.TriggerEvent), así que insertar en el medio correría los ints
        // de los miembros siguientes y corrompería data ya guardada.
        /// <summary>args: [Guid reinforcementGuid]. Un refuerzo fue spawneado mid-combate
        /// (<c>AINode_SpawnReinforcements</c>) y se sumó al turn order en curso. Lo consume
        /// <c>TreeDrivenEnemyAI</c> para diferir la PRIMERA activación del refuerzo: en la ronda
        /// en que aparece no actúa (no hace daño gratis), así el jugador tiene un turno para
        /// reaccionar antes de que el golpe caiga.</summary>
        OnReinforcementSpawned,

        // --- Combat: hazards (fire / ice) ---------------------------------------
        /// <summary>args: [Guid instanceId]. Se activó una instancia de hazard de área dinámica
        /// (<c>IHazardService.Activate</c> con tiles). El id es de la <b>instancia</b>, no de la
        /// definición: varias llamas del mismo SO conviven, cada una con su propio id. Hook para
        /// VFX/SFX de aparición.</summary>
        OnHazardActivated,
        /// <summary>args: [Guid instanceId, Guid entityGuid]. Una entidad activó el hazard — pisó
        /// una tile (OnEnter) o terminó su turno parada en una (OnTurnEndInTile). El daño, si hay,
        /// ya pasó por el pipeline cuando esto se dispara. Es el hook con el que otros sistemas
        /// montan su efecto encima sin que el hazard los conozca: el stun del hielo lo aplica
        /// <c>StunService</c> escuchando acá.</summary>
        OnHazardTriggered,
        /// <summary>args: [Guid instanceId]. La instancia se terminó: se le acabó DurationRounds, se
        /// consumió su última tile (ConsumeOnTrigger) o alguien llamó <c>Deactivate</c>. NO se
        /// dispara en el cleanup de OnCombatEnd/OnRunEnd (mismo criterio que OnComboUnblocked).</summary>
        OnHazardExpired,

        // --- Combat: stun -------------------------------------------------------
        /// <summary>args: [Guid entityGuid, int turns]. Se aplicó stun a la entidad. <c>turns</c> es
        /// el total RESTANTE tras el <c>max(actual, nuevo)</c> — <c>IStunService.ApplyStun</c> no
        /// acumula. Sale en cada llamada a ApplyStun, incluso si el max() no movió el contador
        /// (el feedback es del disparo, no del delta).</summary>
        OnStunApplied,
        /// <summary>args: [Guid entityGuid]. El stun de la entidad llegó a 0: se consumió el último
        /// turno o se curó con <c>IStunService.Clear(entity)</c>. El teardown
        /// (<c>ClearAll</c> en OnCombatEnd/OnRunEnd) NO lo dispara.</summary>
        OnStunExpired,

        // --- Chests (Feature#0046) ----------------------------------------------
        /// <summary>args: [Guid chestGuid, int tier (ItemRarity), bool isMimic]. Un cofre
        /// spawneó en la sala de combate al iniciar el combate.</summary>
        OnChestSpawned,
        /// <summary>args: [Guid chestGuid, int tier (ItemRarity)]. El jugador dio el golpe
        /// final: el cofre se abrió y la recompensa ya fue otorgada. El payload completo
        /// para UI viaja por <c>TypedEvent&lt;ChestOpenedPayload&gt;</c>.</summary>
        OnChestOpened,
        /// <summary>args: [Guid chestGuid, Guid sourceGuid]. Un enemigo o evento dio el golpe
        /// final: el cofre se rompió sin recompensa.</summary>
        OnChestBroken,
        /// <summary>args: [Guid chestGuid, Guid mimicEnemyGuid]. Un golpe del jugador activó
        /// al Mimic: el cofre fue reemplazado por un enemigo activo.</summary>
        OnChestMimicActivated,
        /// <summary>args: [Guid chestGuid]. El combate terminó con el cofre sin resolver —
        /// desapareció sin recompensa.</summary>
        OnChestExpired,

        /// <summary>args: [int rank, int damage]. El Cajero resolvió el escalón con el que va a
        /// pegar la columna de este turno. Lo consume la lectura del HUD, que muestra el daño real
        /// en vez de recalcularlo con su propia copia de la tabla.</summary>
        OnCashierTierChanged,

        // --- Casillas Especiales (Rollgeon.Tiles) --------------------------------
        /// <summary>args: [Guid instanceId]. Una instancia de casilla especial quedó activa en la
        /// sala (autoría de sala o creación runtime). El id es de la instancia, no de la
        /// definición — mismo criterio que <see cref="OnHazardActivated"/>.</summary>
        OnSpecialTilePlaced,
        /// <summary>args: [Guid instanceId, Guid entityGuid, int trigger (TileTrigger)]. La casilla
        /// resolvió su efecto sobre la entidad. El efecto (daño/heal/estado) ya pasó por su
        /// pipeline cuando esto sale — es el hook de feedback/VFX y de sistemas que montan
        /// encima sin que la casilla los conozca.</summary>
        OnSpecialTileTriggered,
        /// <summary>args: [Guid instanceId]. La instancia expiró (DurationRounds) o fue removida
        /// explícitamente. NO se dispara en el teardown de OnCombatEnd/OnRunEnd/OnRoomEntered
        /// (mismo criterio que <see cref="OnHazardExpired"/>).</summary>
        OnSpecialTileExpired,
        /// <summary>args: [Guid instanceId, GridCoord coord, bool armed]. Una celda de la instancia
        /// cambió de estado armado/desarmado (Pinchos). Hook para el visual del tile.</summary>
        OnSpecialTileStateChanged,

        // --- Combat: poison -------------------------------------------------------
        /// <summary>args: [Guid entityGuid, int turns]. Se aplicó (o refrescó) Envenenado. <c>turns</c>
        /// es el total restante tras el refresh — <c>IPoisonService.ApplyPoison</c> no acumula:
        /// re-pisar Veneno resetea la duración, nunca la suma.</summary>
        OnPoisonApplied,
        /// <summary>args: [Guid entityGuid, int damage, int remainingTurns]. Tick de veneno al
        /// inicio del turno del envenenado. El daño ya pasó por el DamagePipeline.</summary>
        OnPoisonTicked,
        /// <summary>args: [Guid entityGuid]. El veneno de la entidad llegó a 0 turnos o se curó.
        /// El teardown (ClearAll en OnCombatEnd/OnRunEnd) NO lo dispara.</summary>
        OnPoisonExpired,

        // --- Combat: teleport cooldown (portales) ---------------------------------
        /// <summary>args: [Guid entityGuid, int turns]. La entidad quedó "recién teletransportada"
        /// tras usar un portal. <c>turns</c> es el total restante tras el refresh —
        /// <c>ITeleportCooldownService.Apply</c> toma max(), nunca suma (criterio Veneno/Stun).</summary>
        OnTeleportCooldownApplied,
        /// <summary>args: [Guid entityGuid, int remainingTurns]. Decrementó al inicio del turno de
        /// la entidad.</summary>
        OnTeleportCooldownTicked,
        /// <summary>args: [Guid entityGuid]. El cooldown llegó a 0 o se limpió con Clear. El
        /// teardown (ClearAll en OnCombatEnd/OnRunEnd/OnRoomEntered) NO lo dispara.</summary>
        OnTeleportCooldownExpired,

        // --- Camera: zoom feedback (BUG-068) -------------------------------------
        // NOTA: agregado al final absoluto del enum a propósito — ver el comentario
        // de OnReinforcementSpawned más arriba. Insertarlo junto a OnCameraFacingChanged
        // (grupo lógico "Camera") shiftearía los ints de todo lo que va después y
        // corrompería los valores ya guardados en assets Odin.
        /// <summary>args: [float newZoom, float previousZoom]. <see cref="ICameraService.ZoomBy"/>
        /// movió <c>_targetZoom</c> (no dispara si el clamp lo dejó igual, ni con zoom
        /// deshabilitado). El tutorial lo consume para gatear el paso de cámara — practicar
        /// el zoom real, no solo leer el texto.</summary>
        OnCameraZoomChanged,

        // --- Movement die (§6.6) --------------------------------------------------
        // NOTA: al final absoluto del enum a propósito (ver OnReinforcementSpawned).
        /// <summary>args: [Guid playerGuid, int face, DiceType type]. El dado de Movimiento
        /// (entidad separada de la build de 5) reveló su cara; la cara es el rango de esa
        /// acción de Movimiento. NO dispara <c>OnDiceRolled</c> — el DiceZoneView no lo ve.
        /// Cierra la mesa de dados (ActionRollExplorationVisibility / CombatHudZoneFlow).</summary>
        OnMovementDieRolled,
        /// <summary>args: [Guid playerGuid, DiceType type]. Arrancó la tirada del dado de
        /// Movimiento con presenter (spin en la mesa). Abre la mesa de dados igual que
        /// <c>OnDiceRolled</c>/<c>OnChainStarted</c>. No se emite en reveals sincrónicos.</summary>
        OnMovementDieRollStarted,

        // --- Roll pool: leftover al fin de turno -----------------------------------
        // NOTA: al final absoluto del enum a propósito (ver OnReinforcementSpawned).
        /// <summary>args: [Guid playerGuid, int leftover]. El turno del jugador terminó con
        /// <c>leftover</c> rolls sin consumir en el pool. Lo emite <c>RollPoolService</c>
        /// ANTES del grant por turno y del clamp: dentro de este dispatch,
        /// <c>IRollPoolService.GetCurrent</c> == leftover (lo que <c>ReadCurrentRolls</c>
        /// aprovecha). Items "Corazón/Tesoro de la fortuna" cuelgan de acá.</summary>
        OnPlayerTurnRollsLeftover,

        // --- Turn state: racha de rondas limpias -----------------------------------
        // NOTA: al final absoluto del enum a propósito (ver OnReinforcementSpawned).
        /// <summary>args: [Guid playerGuid, int streak]. La racha de rondas limpias
        /// (<c>IPlayerTurnStateService.CleanTurnStreak</c>) cambió de valor — incremento
        /// al abrir ronda sin daño, o reset al perder vida. Solo se emite en cambios
        /// REALES (no en cada turno). La UI de daño base (Furia Contenida) re-lee el
        /// override al escucharlo; también sirve como TriggerEvent de PassiveItemHook.</summary>
        OnCleanTurnStreakChanged,
    }
}
