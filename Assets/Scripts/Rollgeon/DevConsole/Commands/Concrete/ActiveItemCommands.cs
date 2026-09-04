using System.Collections.Generic;
using Patterns;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Equipa un item en el slot unico de item activo. Es la unica via de adquisicion
    /// mientras la tienda y los cofres sigan dando items por el camino viejo
    /// (<c>IInventoryService.AddItem</c>) — el catalogo todavia no esta migrado.
    /// </summary>
    public sealed class EquipActiveItemCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("itemId", ArgKind.String, options: ArgProviders.Items),
        };

        public override string Name => "equipactive";
        public override string Description => "Equipa un item activo en el slot unico (descarta el que haya).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e1)) return e1;
            if (!RequireService<ItemCatalogSO>(ctx, out var cat, out var e2)) return e2;
            if (args.Count == 0) return CommandResult.Fail("Usá 'equipactive <itemId>'.");

            var item = cat.GetById(args[0]);
            if (item == null) return CommandResult.Fail($"Item desconocido: '{args[0]}'.");
            if (item.Type != ItemType.Active)
                return CommandResult.Fail($"'{item.ItemId}' no es un item activo.");

            var discarded = equipped.Equip(item);

            string tail = discarded != null ? $" (descartado: {discarded.ItemId})" : string.Empty;
            return CommandResult.Ok(
                $"Equipado {item.DisplayName} — d{item.ActiveDie.MaxFace()}, familia {item.ActiveFamily}{tail}.");
        }
    }

    /// <summary>Vacia el slot de item activo.</summary>
    public sealed class UnequipActiveItemCommand : DevCommandBase
    {
        public override string Name => "unequipactive";
        public override string Description => "Vacia el slot de item activo.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e)) return e;

            var discarded = equipped.Clear();
            return discarded != null
                ? CommandResult.Ok($"Slot vaciado (descartado: {discarded.ItemId}).")
                : CommandResult.Ok("El slot ya estaba vacio.");
        }
    }

    /// <summary>
    /// Aplica un encantamiento al item equipado, pisando el que hubiera. Sin argumentos
    /// lista el pool disponible.
    /// </summary>
    public sealed class EnchantActiveItemCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("enchantmentId", ArgKind.String, optional: true),
        };

        public override string Name => "enchantactive";
        public override string Description => "Encanta el item activo equipado. Sin args lista el pool.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e1)) return e1;

            var pool = ResolvePool();
            if (pool == null || pool.Count == 0)
                return CommandResult.Fail("No hay pool de encantamientos cableado en el bootstrap.");

            if (args.Count == 0)
            {
                var lines = new List<string>();
                foreach (var e in pool)
                    if (e != null) lines.Add($"  {e.EnchantmentId} — {e.DisplayName}: {e.DescribeEffect()}");
                return CommandResult.Ok("Pool de encantamientos:\n" + string.Join("\n", lines));
            }

            ActiveItemEnchantmentSO picked = null;
            foreach (var e in pool)
                if (e != null && string.Equals(e.EnchantmentId, args[0], System.StringComparison.OrdinalIgnoreCase))
                { picked = e; break; }

            if (picked == null) return CommandResult.Fail($"Encantamiento desconocido: '{args[0]}'.");
            if (!equipped.HasItem) return CommandResult.Fail("No hay item activo equipado.");

            var previous = equipped.Enchantment;
            equipped.ApplyEnchantment(picked);

            string tail = previous != null ? $" (pisó a {previous.EnchantmentId})" : string.Empty;
            return CommandResult.Ok($"{equipped.Current.DisplayName} ← {picked.DisplayName}: {picked.DescribeEffect()}{tail}.");
        }

        /// <summary>
        /// El pool vive en el bootstrap, que no se registra en el ServiceLocator. Se lo
        /// busca por AssetDatabase en editor; en build el comando no existe.
        /// </summary>
        private static IReadOnlyList<ActiveItemEnchantmentSO> ResolvePool()
        {
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ActiveItemServiceBootstrap)))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var boot = UnityEditor.AssetDatabase.LoadAssetAtPath<ActiveItemServiceBootstrap>(path);
                if (boot != null) return boot.EnchantmentPool;
            }
#endif
            return null;
        }
    }

    /// <summary>
    /// Estado del slot: que hay equipado, su dado y familia, el reparto de bandas y por
    /// que esta bloqueado ahora mismo.
    /// </summary>
    public sealed class ActiveItemStatusCommand : DevCommandBase
    {
        public override string Name => "activeitem";
        public override string Description => "Muestra el slot de item activo y su gate actual.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e)) return e;

            if (!equipped.HasItem) return CommandResult.Ok("Slot vacio.");

            var item = equipped.Current;
            int faces = item.ActiveDie.MaxFace();

            string gate = "sin servicio de activacion";
            if (ServiceLocator.TryGetService<IActiveItemActivationService>(out var act) && act != null)
                gate = act.CanActivate().ToString();

            string ench = equipped.Enchantment == null
                ? "sin encantamiento"
                : $"{equipped.Enchantment.DisplayName} — {equipped.Enchantment.DescribeEffect()}"
                  + (equipped.Enchantment.IsLimited ? $" [{equipped.EnchantmentUsesLeft} usos]" : string.Empty);

            // Caras y no rangos: en Precision/Control las bandas no son contiguas, y en
            // Binary/Gradient/Hierarchy directamente no hay 3 bandas fijas.
            var text = new System.Text.StringBuilder();
            text.AppendLine($"{item.DisplayName} ({item.ItemId}) — d{faces}, "
                          + $"resolucion {item.ActiveResolution}, familia {item.ActiveFamily}");

            var rows = ActiveItemBands.DescribeStructure(item);
            var rowParts = new List<string>();
            foreach (var row in rows) rowParts.Add($"{row.Label} {row.Faces}");
            text.AppendLine("  " + string.Join(" | ", rowParts));
            text.AppendLine($"  {ench}");
            text.Append($"  gate: {gate}");

            return CommandResult.Ok(text.ToString());
        }
    }
}
