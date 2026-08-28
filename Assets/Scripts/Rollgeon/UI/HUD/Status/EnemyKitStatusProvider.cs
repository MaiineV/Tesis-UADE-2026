using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Weakness;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el kit del enemigo: la debilidad (combo y multiplicador) para la columna principal
    /// del panel, y lo que sabe hacer que no es un ataque (hoy, teleportarse) para la del costado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La debilidad sale del <see cref="IWeaknessRegistry"/> y no del <see cref="EnemyDataSO"/>:
    /// el registry es la fuente viva — lo puebla el spawn y lo puede reescribir la IA mid-combate —
    /// así que leer el SO daría el dato de autoría y no el vigente. Mismo contrato que
    /// <c>BossBarView.ApplyWeakness</c>, hasta en el default del multiplicador.
    /// </para>
    /// <para>
    /// El teleport sale del árbol del propio bicho, no de una lista autorada aparte: si un rediseño
    /// le saca los nodos de salto, la tarjeta se va sola en vez de prometer una fuga que ya no
    /// existe.
    /// </para>
    /// </remarks>
    public sealed class EnemyKitStatusProvider : IStatusIconProvider
    {
        public const string WeaknessId = "enemy.weakness";
        public const string TeleportId = "ability.teleport";

        private const float FallbackWeaknessMultiplier = 1.5f;

        private readonly StatusIconCatalogSO _catalog;
        private readonly bool _teleports;

        public EnemyKitStatusProvider(StatusIconCatalogSO catalog, EnemyDataSO data)
        {
            _catalog = catalog;

            // Resuelto una vez al spawn y no por Collect: el árbol no cambia de forma en combate,
            // y recorrerlo entero en cada hover sería pagar en runtime un dato de autoría.
            _teleports = Teleports(data);
        }

        /// <summary>Sólo el teleport: la debilidad va por <see cref="CollectWeakness"/>.</summary>
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
                    active: true));
        }

        /// <summary>
        /// La debilidad sola, para la columna PRINCIPAL del panel. Separada de
        /// <see cref="Collect"/> porque no comparte columna con el resto del kit: es lo único que
        /// cambia qué tirás, y va en el panel, no en la tirita del costado.
        /// </summary>
        public void CollectWeakness(Guid ownerGuid, List<StatusIconState> into)
        {
            if (ownerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IWeaknessRegistry>(out var registry) || registry == null)
                return;
            if (!registry.TryGet(ownerGuid, out var weakness)) return;
            if (string.IsNullOrEmpty(weakness.comboId)) return;

            float multiplier = weakness.mult > 0f ? weakness.mult : DefaultWeaknessMultiplier();
            var combo = ResolveCombo(weakness.comboId);
            string comboName = combo != null
                ? LocalizedContent.Name(combo.ComboId, combo.DisplayName)
                : weakness.comboId;

            into.Add(new StatusIconState(
                WeaknessId,
                LocalizedContent.Name(WeaknessId, "Débil"),
                LocalizedContent.DescriptionFormat(WeaknessId, "{0} le pega ×{1}.",
                    comboName, multiplier.ToString("0.##")),
                // El arte del combo, no un ícono de "debilidad": lo que el jugador tiene que
                // reconocer es la mano que le conviene tirar.
                combo != null ? combo.Icon : null,
                active: true));
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

        private static float DefaultWeaknessMultiplier()
        {
            ServiceLocator.TryGetService<RulesetSO>(out var ruleset);
            return ruleset != null && ruleset.Weakness != null
                ? ruleset.Weakness.DefaultMultiplier
                : FallbackWeaknessMultiplier;
        }

        private static BaseComboSO ResolveCombo(string comboId)
            => ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) && catalog != null
                ? catalog.GetById(comboId)
                : null;
    }
}
