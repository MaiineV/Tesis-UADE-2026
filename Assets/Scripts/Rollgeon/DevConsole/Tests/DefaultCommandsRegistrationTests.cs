using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Commands;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Tests
{
    /// <summary>
    /// El registry de la consola es explícito: escribir un <see cref="DevCommandBase"/>
    /// no lo hace existir, hay que sumarlo a <see cref="DefaultCommands.CreateDefault"/>.
    /// Es un paso fácil de olvidar y el síntoma es "comando desconocido" en runtime, sin
    /// ningún error de compilación que avise.
    /// </summary>
    public class DefaultCommandsRegistrationTests
    {
        private static DevCommandRegistry BuildRealRegistry(FakeConsoleContext ctx)
            => DefaultCommands.CreateDefault(ctx,
                new GodModeController(ctx), new InfiniteRollsController(ctx), new FreeMoveController());

        [Test]
        public void test_registry_everyConcreteCommandInTheAssembly_isRegistered()
        {
            // Arrange
            var ctx = new FakeConsoleContext();
            var registered = new HashSet<string>(
                BuildRealRegistry(ctx).All.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            // Act — todo comando concreto que viva en el assembly de produccion.
            var missing = typeof(DefaultCommands).Assembly
                .GetTypes()
                .Where(t => typeof(DevCommandBase).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructors().Any(c => c.GetParameters().Length == 0))
                .Select(t => (DevCommandBase)Activator.CreateInstance(t))
                .Where(c => !registered.Contains(c.Name))
                .Select(c => $"{c.GetType().Name} ('{c.Name}')")
                .ToList();

            // Assert
            CollectionAssert.IsEmpty(missing,
                "Comandos escritos pero nunca registrados en DefaultCommands.CreateDefault:\n"
                + string.Join("\n", missing));
        }

        [Test]
        public void test_registry_activeItemCommands_areReachable()
        {
            // Arrange — el slot unico solo se puede equipar por consola: la tienda y los
            // cofres todavia entregan por IInventoryService.AddItem.
            var ctx = new FakeConsoleContext();
            var names = BuildRealRegistry(ctx).All.Select(c => c.Name).ToList();

            // Assert
            CollectionAssert.Contains(names, "equipactive");
            CollectionAssert.Contains(names, "unequipactive");
            CollectionAssert.Contains(names, "activeitem");
        }

        [Test]
        public void test_registry_hasNoDuplicateNames()
        {
            // Arrange — dos comandos con el mismo nombre: uno queda inalcanzable.
            var ctx = new FakeConsoleContext();
            var names = BuildRealRegistry(ctx).All.Select(c => c.Name).ToList();

            // Act
            var dupes = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                             .Where(g => g.Count() > 1)
                             .Select(g => g.Key)
                             .ToList();

            // Assert
            CollectionAssert.IsEmpty(dupes, "Nombres de comando repetidos: " + string.Join(", ", dupes));
        }
    }
}
