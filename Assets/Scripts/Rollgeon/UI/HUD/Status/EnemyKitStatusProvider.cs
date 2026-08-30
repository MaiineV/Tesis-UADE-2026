using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Weakness;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el kit del enemigo — el combo al que es débil y lo que sabe hacer que no es un
    /// ataque (hoy, teleportarse) — como rasgos (<see cref="StatusCardStyle.Trait"/>): slots de
    /// la fila de abajo del panel, nunca la fila que flota sobre la cabeza.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La debilidad sale del <see cref="IWeaknessRegistry"/> y no del <see cref="EnemyDataSO"/>:
    /// el registry es la fuente viva — lo puebla el spawn y lo puede reescribir la IA mid-combate —
    /// así que leer el SO daría el dato de autoría y no el vigente. Mismo contrato que
    /// <c>BossBarView.ApplyWeakness</c>.
    /// </para>
    /// <para>
    /// El teleport sale del árbol del propio bicho, no de una lista autorada aparte: si un rediseño
    /// le saca los nodos de salto, la tarjeta se va sola en vez de prometer una fuga que ya no
    /// existe.
    /// </para>
    /// </remarks>
    public sealed class EnemyKitStatusProvider : IStatusIconProvider
    {
        public const string TeleportId = "ability.teleport";

        /// <summary>
        /// Id del slot de la debilidad (la piedrita rota del mockup) y su key en el catálogo de
        /// íconos. Sin entry de catálogo el slot no sale: la fila de abajo filtra lo sin arte.
        /// </summary>
        public const string WeaknessId = "enemy.weakness";

        private readonly StatusIconCatalogSO _catalog;
        private readonly bool _teleports;

        public EnemyKitStatusProvider(StatusIconCatalogSO catalog, EnemyDataSO data)
        {
            _catalog = catalog;

            // Resuelto una vez al spawn y no por Collect: el árbol no cambia de forma en combate,
            // y recorrerlo entero en cada hover sería pagar en runtime un dato de autoría.
            _teleports = Teleports(data);
        }

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;

            if (_teleports)
                into.Add(new StatusIconState(
                    TeleportId,
                    LocalizedContent.Name(TeleportId, "Se teletransporta"),
                    LocalizedContent.Description(TeleportId,
                        "Salta a una casilla al lado tuyo, o al otro lado de la sala."),
                    // El sprite de tp_delay a propósito: es el único teleport dibujado. La key es
                    // otra porque tp_delay es el cooldown de HABER saltado, no saber saltar.
                    _catalog != null ? _catalog.Resolve("status.tp_delay") : null,
                    active: true,
                    style: StatusCardStyle.Trait));

            // La debilidad como slot — la piedrita rota del mockup. El nombre del combo viaja
            // como DisplayName por si algún día el slot dice algo; hoy el slot es sólo ícono.
            string combo = WeaknessComboName(ownerGuid);
            if (!string.IsNullOrEmpty(combo))
                into.Add(new StatusIconState(
                    WeaknessId,
                    combo,
                    null,
                    _catalog != null ? _catalog.Resolve(WeaknessId) : null,
                    active: true,
                    style: StatusCardStyle.Trait));
        }

        /// <summary>
        /// El nombre localizado del combo al que es débil, o <c>null</c> sin debilidad registrada.
        /// </summary>
        public string WeaknessComboName(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return null;
            if (!ServiceLocator.TryGetService<IWeaknessRegistry>(out var registry) || registry == null)
                return null;
            if (!registry.TryGet(ownerGuid, out var weakness)) return null;
            if (string.IsNullOrEmpty(weakness.comboId)) return null;

            var combo = ResolveCombo(weakness.comboId);
            return combo != null
                ? LocalizedContent.Name(combo.ComboId, combo.DisplayName)
                : weakness.comboId;
        }

        private static bool Teleports(EnemyDataSO data)
        {
            if (data == null || data.AIRoot == null) return false;

            var nodes = new List<AIActionNode>();
            AIIntentWalker.CollectNodes(data.AIRoot, nodes);

            foreach (var node in nodes)
                if (node is AINode_TeleportAwayToEdge
                    || node is AINode_TeleportNearTarget
                    || node is AINode_TeleportToRoomCenter) return true;

            return false;
        }

        private static BaseComboSO ResolveCombo(string comboId)
            => ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) && catalog != null
                ? catalog.GetById(comboId)
                : null;
    }
}
