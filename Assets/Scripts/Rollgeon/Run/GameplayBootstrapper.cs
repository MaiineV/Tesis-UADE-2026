using Patterns;
using Rollgeon.Balance;
using Rollgeon.GameCamera;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// MonoBehaviour escena-scoped para <c>02_Gameplay</c>. Lee
    /// <see cref="PendingRunRequest"/>, arranca la run via
    /// <see cref="RunBootstrapper.StartRun"/> (que crea la scope Run + wirea
    /// servicios via <c>IRunController</c>), pushea <c>ExplorationHUD</c> y
    /// spawnea el GameObject del hero en la grilla de la primera sala.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject vive en 02_Gameplay.unity. Sin fields serializados.
    /// Execution order -500 para correr despues de ScreenHost (-1000) y antes
    /// de gameplay MonoBehaviours default (0).
    /// </remarks>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("Rollgeon/Run/Gameplay Bootstrapper")]
    public sealed class GameplayBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "[GameplayBootstrapper] ";

        private void Start()
        {
            if (!PendingRunRequest.HasRequest)
            {
                Debug.LogError(LogPrefix + "No pending run request. Cargaste 02_Gameplay sin pasar por BuildSelection?", this);
                return;
            }

            var hero = PendingRunRequest.SelectedHero;
            var runId = PendingRunRequest.RunId;

            ServiceLocator.TryGetService<RulesetSO>(out var ruleset);

            // 1. Push ExplorationHUD PRIMERO — queda en la base del stack.
            //    Cualquier overlay que pushee el chain de StartRun (CombatHUD,
            //    FloorTransition) aterriza correctamente encima.
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("ExplorationHUD");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — el ScreenHost de 02_Gameplay no corrio todavia?", this);
            }

            // 2. Bag construido en BuildSelectionScreen (Fase 2). Se pasa a StartRun para
            //    que lo aplique ANTES de disparar OnRunStart — los servicios que siembran
            //    estado desde IPlayerService.DiceBag en ese evento (DiceEnchantmentService)
            //    deben ver la build elegida, no el StartingDiceBagRef del hero (BUG-012).
            //    Si no vino, el flujo cae al fallback de Fase 1 (StartingDiceBagRef o
            //    Resources/AD_Warrior_StartingBag) en CombatHandoffService.
            var builtBag = PendingRunRequest.BuiltDiceBag;

            // 3. Arrancar la run. El chain
            //    (RunController.OnRunStart → ExplorationController.BeginExploration →
            //    ProcessRoom) puede pushear CombatHUD con seguridad.
            RunBootstrapper.StartRun(hero, ruleset, runId, builtBag);
            Debug.Log(LogPrefix + $"Run started. hero={hero.EntityId}, runId={runId}, " +
                      $"builtBag={(builtBag != null ? builtBag.Dice.Count + " dados" : "null (fallback)")}", this);

            var startingItems = PendingRunRequest.StartingItems;
            if (startingItems != null && startingItems.Count > 0)
            {
                if (ServiceLocator.TryGetService<IInventoryService>(out var inventory))
                {
                    int added = 0;
                    foreach (var item in startingItems)
                    {
                        if (item != null && inventory.AddItem(item)) added++;
                    }
                    Debug.Log(LogPrefix + $"Aplicados {added}/{startingItems.Count} starting items.", this);
                }
                else
                {
                    Debug.LogWarning(LogPrefix + "IInventoryService no registrado — starting items ignorados.", this);
                }
            }

            // 4. Spawn visual del hero en la grilla de la primera sala (§0203).
            //    RunController.OnRunStart ya corrió, así que grid + dungeon están cargados.
            SpawnHeroInFirstRoom(hero);

            PendingRunRequest.Clear();
        }

        private void SpawnHeroInFirstRoom(Rollgeon.Heroes.ClassHeroSO hero)
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)) return;
            if (playerService.PlayerGuid == System.Guid.Empty) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogWarning(LogPrefix + "IGridManager no registrado — hero no se posiciona en grilla.", this);
                return;
            }
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return;

            var instance = dungeon.CurrentRoomInstance;
            var spawnCoord = ResolveSpawnCoord(instance, grid);

            grid.Register(playerService.PlayerGuid, spawnCoord);

            EntityPawn heroPawn = null;
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals))
            {
                heroPawn = visuals.SpawnHero(playerService.PlayerGuid, hero, spawnCoord);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IEntityVisualService no registrado — hero queda sin pawn visible.", this);
            }

            if (heroPawn != null && ServiceLocator.TryGetService<ICameraService>(out var cam))
            {
                cam.SetFollowTarget(heroPawn.transform);
            }
        }

        /// <summary>
        /// Resuelve el tile de spawn del hero a partir del
        /// <see cref="RoomLayout.PlayerSpawnPoint"/> del prefab instanciado.
        /// Si no hay prefab o no tiene PlayerSpawnPoint, cae a
        /// <see cref="GridCoord.Zero"/>.
        /// </summary>
        private static GridCoord ResolveSpawnCoord(RoomInstance instance, IGridManager grid)
        {
            if (instance?.SpawnedPrefab == null) return GridCoord.Zero;

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null || layout.PlayerSpawnPoint == null) return GridCoord.Zero;

            return grid.WorldToGrid(layout.PlayerSpawnPoint.position);
        }
    }
}
