using System.Runtime.CompilerServices;

// Tests que necesitan acceso a props `internal set` en tipos runtime
// (ej. RerollBudget en Dice). Mantener a mano — solo agregar assemblies
// que realmente requieran acceso a internals.
[assembly: InternalsVisibleTo("Rollgeon.Dice.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Run.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Exploration.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Meta.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Feedback.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Combat.Handoff.Tests")]
// FloorTopologyPlanner.AssignBosses: el vínculo jefe→sala se testea sobre el paso solo,
// sin tener que armar un piso entero para cada caso.
[assembly: InternalsVisibleTo("Rollgeon.Dungeon.Tests")]
// AINode_SpawnRoomObjects.BuildDoorFrontSlots: el reparto de DoorFronts (puertas primero,
// remanente al anillo) se testea sin engine. Leer las puertas de un RoomLayout necesita un
// prefab de sala instanciado; el orden y el presupuesto, que son lo que puede romperse en
// silencio, no.
[assembly: InternalsVisibleTo("Rollgeon.Combat.Rooms.Tests")]
// HeroActionTooltip.ResolveActionNameKey: el mapeo slot→key de localización se testea
// como función pura, sin montar el runtime de Localization.
[assembly: InternalsVisibleTo("Rollgeon.UI.Tests")]
// AllEnemyRangesOverlay.Repaint(order): la selección de quién se pinta se testea contra
// una lista de guids, sin armar un TurnOrderService real con initiative para cada caso.
[assembly: InternalsVisibleTo("Rollgeon.Combat.AI.Tests")]
