using System.Collections.Generic;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Buffer mutable que los triggers escriben durante el dispatch de un evento.
    /// El <c>DiceEnchantmentService</c> (Phase 4) crea uno fresh por evento, llama
    /// a los hooks, y luego aplica los acumulados (combat, economía, escudo) sobre
    /// los sistemas reales.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué scratch y no modificar <c>EffectContext</c>.</b> EffectContext es
    /// compartido con el pipeline de combate; agregarle fields enchantment-específicos
    /// acoplaría combat a enchantments. El scratch vive en
    /// <c>EnchantmentTriggerContext</c> y existe solo durante el evento.
    /// </para>
    /// <para>
    /// <b>Composición.</b> Múltiples triggers (incluso de encantamientos distintos
    /// en distintos dados) escriben al mismo scratch. El orden de dispatch lo
    /// determina el service; los efectos se suman (BonusGold) o multiplican
    /// (ComboDamageMultiplier) según corresponda.
    /// </para>
    /// </remarks>
    public sealed class EnchantmentScratch
    {
        /// <summary>Bonus plano que se suma al daño del combo resuelto. Suma entre triggers.</summary>
        public int BonusComboDamage;

        /// <summary>Multiplicador aplicado al daño final del combo. Se compone multiplicativamente entre triggers.</summary>
        public float ComboDamageMultiplier = 1f;

        /// <summary>
        /// Si algún trigger setea este flag a <c>true</c>, el daño del combo se anula
        /// a 0 después de aplicar multipliers/bonuses. Used by "no gold = no damage".
        /// </summary>
        public bool BlockComboDamage;

        /// <summary>Oro neto que el service le suma al jugador tras el evento. Puede ser negativo (costos).</summary>
        public int BonusGold;

        /// <summary>Shield extra que el service le aplica al jugador tras el evento.</summary>
        public int BonusShield;

        /// <summary>
        /// Acumuladores genéricos por recurso (oro / stats) que escriben los triggers
        /// parametrizables vía <see cref="Modify"/>. El <c>EnchantmentScratchApplier</c>
        /// los resuelve sobre los sistemas reales tras el evento. Los campos legacy
        /// <see cref="BonusGold"/> / <see cref="BonusShield"/> se fusionan acá al aplicar.
        /// </summary>
        private readonly Dictionary<ResourceTarget, ResourceAccumulator> _resources =
            new Dictionary<ResourceTarget, ResourceAccumulator>();

        public IReadOnlyDictionary<ResourceTarget, ResourceAccumulator> Resources => _resources;

        /// <summary>Aplica una operación sobre un recurso, acumulándola para el evento.</summary>
        public void Modify(ResourceTarget target, ResourceOperation op, int amount)
        {
            if (!_resources.TryGetValue(target, out var acc)) acc = ResourceAccumulator.Identity;
            _resources[target] = acc.Apply(op, amount);
        }

        /// <summary>Resetea el scratch para reusar la instancia. Llamado por el service entre eventos.</summary>
        public void Reset()
        {
            BonusComboDamage = 0;
            ComboDamageMultiplier = 1f;
            BlockComboDamage = false;
            BonusGold = 0;
            BonusShield = 0;
            _resources.Clear();
        }
    }
}
