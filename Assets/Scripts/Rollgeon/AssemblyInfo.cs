using System.Runtime.CompilerServices;

// Tests que necesitan acceso a props `internal set` en tipos runtime
// (ej. RerollBudget en Dice). Mantener a mano — solo agregar assemblies
// que realmente requieran acceso a internals.
[assembly: InternalsVisibleTo("Rollgeon.Dice.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Run.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Exploration.Tests")]
[assembly: InternalsVisibleTo("Rollgeon.Meta.Tests")]
