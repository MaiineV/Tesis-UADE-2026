using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;
using Rollgeon.Combos;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Phase;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Intenta forzar la puerta adyacente al jugador. La dirección se auto-detecta
    /// igual que <see cref="EffPassDoor"/> — el caller debe garantizar adjacencia
    /// vía <c>PCAdjacentToDoor</c>.
    /// <para>
    /// - <b>Fuera de combate</b>: instantáneo. No tira dados ni gasta energía.
    /// </para>
    /// <para>
    /// - <b>En combate</b>: cuesta <see cref="EnergyCostInCombat"/> + 1 tirada.
    /// Si la suma alcanza <see cref="RequiredValue"/>, los enemigos de la sala se
    /// curan <see cref="EnemyHealPercentOnSuccess"/>% del max HP y el jugador cruza.
    /// Si no, la energía gastada se pierde y queda en la sala (con opción de reroll
    /// vía <see cref="IActionRollService"/>).
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffForceDoor : BaseEffect, IActionRollEffect, IHasTooltipInfo
    {
        [Title("Force Door")]
        [Tooltip("Threshold mínimo del 'effective total' de la tirada para forzar la puerta " +
                 "(solo aplica en combate). El effective total usa formula B: si la tirada " +
                 "matchea un combo, vale combo.BaseDamage; sino, vale la suma cruda de los pips.")]
        [Min(1)]
        public int RequiredValue = 25;

        [Tooltip("Energía que cuesta intentar forzar la puerta dentro de combate. " +
                 "Fuera de combate es 0.")]
        [Min(0)]
        public int EnergyCostInCombat = 2;

        [Tooltip("Porcentaje del max HP que se curan los enemigos de la sala cuando " +
                 "se fuerza la puerta exitosamente en combate.")]
        [Range(0, 100)]
        public int EnemyHealPercentOnSuccess = 25;

        public override string GetEffectName() => "Force Door";

        // EffForceDoor auto-detecta la puerta adyacente (no consume context.SelectionResult).
        // Override para que CombatHandoffService NO entre al path de player-state-selection
        // — eso defería el effect hasta el final del combate (visto in-game). Sin selection,
        // el behavior se ejecuta synchronously via TurnManager.TryExecuteEnergyPrepaid.
        protected override bool ShowSelection => false;
        public override bool HasSelectionRequirement() => false;
        public override bool RequiresSelectionAt(Selection.SelectionTiming timing) => false;

        public bool TryGetRollSpec(Guid playerGuid, out ActionRollSpec spec)
        {
            spec = default;
            if (!IsInCombat()) return false;
            if (IsBossRoom()) return false; // Sala de Boss = sin escape; el boss debe vencerse.

            spec = new ActionRollSpec
            {
                EnergyCost = EnergyCostInCombat,
                Threshold = RequiredValue,
                // RequireConfirm = false: ya no hay modal de confirm. El user ve threshold +
                // combo en el DamageFormulaView; al clickear el botón se cobra energía y va
                // directo a Rolling.
                RequireConfirm = false,
                ActionLabel = "Forzar Puerta",
                AllowReroll = true,
                RerollEnergyCost = 1,
            };
            return true;
        }

        // IHasTooltipInfo — el binder de la puerta consume esto. Fuera de combate
        // devuelve null porque la puerta se abre sin tirada y el tooltip no aporta.
        // En sala de Boss tampoco se puede escapar — el boss debe vencerse.
        public string BuildTooltip()
        {
            if (!IsInCombat()) return null;
            if (IsBossRoom()) return null;
            return $"<b>Forzar Puerta</b>\nPuntaje a superar: {RequiredValue}\n" +
                   $"Costo: {EnergyCostInCombat} de energía";
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
                return false;

            var instance = dungeon.CurrentRoomInstance;
            if (instance == null) return false;

            bool inCombat = IsInCombat();

            // In-combat: validar la tirada antes de tocar nada — la energía YA se cobró
            // por el IActionRollService, así que un fallo aca = energía perdida.
            if (inCombat)
            {
                if (context.DiceResult == null || context.DiceResult.Count == 0)
                {
                    Debug.LogWarning("[EffForceDoor] In-combat sin DiceResult — el behavior debió " +
                                     "pasar por IActionRollService antes de Execute.");
                    return false;
                }

                // Prioridad: el ActionRollService ya computó el effective sobre los held dice.
                // Si viene pre-computado, usarlo (sino el cálculo desde FinalRoll incluye los 5
                // dados aunque el user holdeó un subset → el threshold se evalúa mal).
                int effectiveTotal = context.ActionRollEffectiveTotal
                    ?? ActionRollTotals.ResolveEffectiveTotal(context.DiceResult, context.ComboResult);

                int rawSum = ActionRollTotals.SumOf(context.DiceResult);
                bool sheetCombo = context.ComboResult is { IsMatch: true };
                Debug.LogWarning($"[EffForceDoor] dice=[{string.Join(",", context.DiceResult)}] " +
                                 $"rawSum={rawSum} sheetCombo={(sheetCombo ? context.ComboResult.Value.BaseDamage.ToString() : "(none)")} " +
                                 $"override={(context.ActionRollEffectiveTotal?.ToString() ?? "(none)")} " +
                                 $"effective={effectiveTotal} threshold={RequiredValue} " +
                                 $"→ {(effectiveTotal >= RequiredValue ? "PASA" : "FALLA")}");

                if (effectiveTotal < RequiredValue) return false;
            }

            // Tanto in-combat (post-éxito) como out-of-combat: resolver puerta + cruzar.
            if (!TryResolveAdjacentDoorDirection(context, instance, out var direction))
            {
                Debug.LogWarning("[EffForceDoor] No hay puerta adyacente — no-op. " +
                                 "Verificar que el behavior tenga PCAdjacentToDoor en ShowConditions.");
                return false;
            }

            if (inCombat)
            {
                HealCurrentRoomEnemies(instance, EnemyHealPercentOnSuccess);
                // El DungeonManager limpia SpawnedEnemies en TransitionTo pero NO destruye
                // los GameObjects visuales — si no los despawneamos acá, los enemigos siguen
                // renderizados en pantalla en la sala nueva. Snapshot la lista antes de
                // EnterRoomByDoor (que la clea).
                DespawnRoomEnemyVisuals(instance);
            }

            MarkDoorForced(instance, direction);
            bool entered = dungeon.EnterRoomByDoor(direction);
            if (entered && inCombat)
            {
                // Force Door exitoso en combate = el jugador escapa. Notificamos al
                // CombatController para que el combat actual termine (Aborted). Sin
                // esto el HUD de combate y la FSM siguen activos en la nueva sala.
                if (ServiceLocator.TryGetService<ICombatSignaller>(out var signaller)
                    && signaller != null)
                {
                    signaller.NotifyCombatEnded(CombatOutcome.Aborted);
                }
                else
                {
                    Debug.LogWarning("[EffForceDoor] ICombatSignaller no registrado — " +
                                     "el combat no se va a cerrar despues de forzar la puerta.");
                }
            }
            return entered;
        }

        // Despawnea los pawns visuales + unregistra del grid para que los enemigos
        // que quedan vivos en la sala vieja no se rendericen al cambiar de sala.
        // El HP curado ya quedó guardado en EnemySpawnState.CurrentHP (ver
        // HealCurrentRoomEnemies); cuando el player vuelva, respawnean con ese HP.
        private static void DespawnRoomEnemyVisuals(RoomInstance instance)
        {
            if (instance?.SpawnedEnemies == null || instance.SpawnedEnemies.Count == 0) return;

            ServiceLocator.TryGetService<IEntityVisualService>(out var visuals);
            ServiceLocator.TryGetService<IGridManager>(out var grid);

            // Copia defensiva — TransitionTo va a clear-ar la lista original.
            var snapshot = new List<Guid>(instance.SpawnedEnemies);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var guid = snapshot[i];
                if (guid == Guid.Empty) continue;
                visuals?.Despawn(guid);
                grid?.Unregister(guid);
            }
        }

        private static void MarkDoorForced(RoomInstance instance, DoorDirection direction)
        {
            var doorKey = direction.DoorStateKey();
            if (instance.ObjectStates.TryGet<DoorState>(doorKey, out var doorState))
                doorState.Forced = true;
        }

        private static bool IsInCombat()
        {
            return ServiceLocator.TryGetService<IPhaseService>(out var phase)
                   && phase != null
                   && phase.CurrentBase == GamePhase.Combat;
        }

        private static bool IsBossRoom()
        {
            return ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                   && dungeon?.CurrentRoom != null
                   && dungeon.CurrentRoom.Type == RoomType.Boss;
        }

        // Auto-detect: busca DoorControllers en el SpawnedPrefab de la sala y devuelve
        // la dirección de la primera puerta no-tapiada cuya posición esté a Manhattan
        // ≤ 1 del player. Mismo algoritmo que EffPassDoor (TECHNICAL.md §13.6).
        private static bool TryResolveAdjacentDoorDirection(EffectContext context,
            RoomInstance instance, out DoorDirection direction)
        {
            direction = default;
            if (instance?.SpawnedPrefab == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;

            var sourceGuid = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            if (!grid.TryGetPosition(sourceGuid, out var playerCoord)) return false;

            foreach (var door in instance.SpawnedPrefab.GetComponentsInChildren<DoorController>())
            {
                if (door.CurrentState == DoorVisualState.Tapiada) continue;
                if (!instance.Connections.ContainsKey(door.Direction)) continue;
                var doorCoord = grid.WorldToGrid(door.transform.position);
                // Manhattan ≤ 1: solo casillas ortogonales (N/E/S/W). Las diagonales
                // no cuentan — el jugador tiene que estar pegado a la puerta en línea
                // recta para poder forzarla.
                if (playerCoord.Manhattan(doorCoord) <= 1)
                {
                    direction = door.Direction;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Test público: ¿el player está adyacente (Manhattan ≤ 1, ortogonal) a alguna
        /// puerta no-tapiada de la sala actual? Usado por la UI para gating del botón
        /// "Forzar Puerta" en combat — sin esto el botón quedaba habilitado aunque el
        /// player estuviera lejos de cualquier puerta.
        /// </summary>
        public static bool IsPlayerAdjacentToAnyDoor(Guid playerGuid)
        {
            if (playerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return false;
            var instance = dungeon?.CurrentRoomInstance;
            if (instance?.SpawnedPrefab == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;

            foreach (var door in instance.SpawnedPrefab.GetComponentsInChildren<DoorController>())
            {
                if (door.CurrentState == DoorVisualState.Tapiada) continue;
                if (!instance.Connections.ContainsKey(door.Direction)) continue;
                var doorCoord = grid.WorldToGrid(door.transform.position);
                if (playerCoord.Manhattan(doorCoord) <= 1) return true;
            }
            return false;
        }

        // Heal +X% del max HP a cada enemigo vivo de la sala. El max HP viene del
        // EnemyDataSO referenciado por el EnemySpawnState. Snapshotteamos el HP
        // resultante a state.CurrentHP para que la próxima entrada respawnee con
        // ese valor (el TransitionTo limpia SpawnedEnemies pero ObjectStates persiste).
        private static void HealCurrentRoomEnemies(RoomInstance instance, int percentOfMax)
        {
            if (percentOfMax <= 0) return;
            if (instance == null || instance.SpawnedEnemies == null) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes) || attributes == null)
                return;

            // Pareo de SpawnedEnemies (live, en orden de spawn) con EnemySpawnStates
            // alive ordenados por SpawnPointIndex — ambos arrays mantienen el mismo
            // orden si el OnEntityDestroyed de DungeonManager hizo bien su parte.
            var aliveStates = new List<EnemySpawnState>();
            foreach (var kv in instance.ObjectStates.Enumerate())
            {
                if (kv.Value is EnemySpawnState s && !s.IsDead) aliveStates.Add(s);
            }
            aliveStates.Sort((a, b) => a.SpawnPointIndex.CompareTo(b.SpawnPointIndex));

            int pairCount = Mathf.Min(instance.SpawnedEnemies.Count, aliveStates.Count);
            for (int i = 0; i < pairCount; i++)
            {
                var enemyGuid = instance.SpawnedEnemies[i];
                if (enemyGuid == Guid.Empty) continue;

                var state = aliveStates[i];
                int maxHp = LookupEnemyMaxHp(instance.Template, state.EnemyDataSOId);
                if (maxHp <= 0) continue;

                var health = attributes.GetAttribute<Health>(enemyGuid);
                int currentHp = health != null ? health.Value : state.CurrentHP;

                int healAmount = Mathf.CeilToInt(maxHp * (percentOfMax / 100f));
                int newHp = Mathf.Min(maxHp, currentHp + healAmount);

                if (health != null)
                    attributes.SetAttributeValue<Health, int>(enemyGuid, newHp);
                state.CurrentHP = newHp;
            }
        }

        // Replica privada de la lookup del DefaultEnemySpawnResolver — busca el
        // EnemyDataSO cuyo EntityId coincide con el grabado en el EnemySpawnState.
        // Necesario para resolver el max HP fuera del flow de spawn.
        private static int LookupEnemyMaxHp(RoomSO room, string entityId)
        {
            if (room == null || string.IsNullOrEmpty(entityId)) return 0;

            if (room.PossibleSetups != null)
            {
                foreach (var setup in room.PossibleSetups)
                {
                    if (setup?.Slots == null) continue;
                    foreach (var slot in setup.Slots)
                    {
                        if (slot.Enemy != null && slot.Enemy.EntityId == entityId)
                            return Mathf.Max(0, slot.Enemy.BaseHP);
                    }
                }
            }

            if (room.EnemyPool != null && room.EnemyPool.Entries != null)
            {
                foreach (var entry in room.EnemyPool.Entries)
                {
                    if (entry.Item != null && entry.Item.EntityId == entityId)
                        return Mathf.Max(0, entry.Item.BaseHP);
                }
            }

            return 0;
        }
    }
}
