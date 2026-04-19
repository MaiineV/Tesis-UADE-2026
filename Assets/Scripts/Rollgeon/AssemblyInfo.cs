using System.Runtime.CompilerServices;

// Tests que necesitan acceso a props `internal set` en tipos runtime
// (ej. RerollBudget en Dice). Mantener a mano — solo agregar assemblies
// que realmente requieran acceso a internals.
[assembly: InternalsVisibleTo("Rollgeon.Dice.Tests")]
