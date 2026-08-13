using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.BossHand
{
    /// <summary>
    /// Estado de la mano de dados que un boss tiene sobre la mesa (piso 3 — La Generala).
    /// Guarda, por boss, los valores de la última tirada, el combo que salió de correrla por el
    /// mismo <c>ComboResolver</c> que la mano del jugador, y si esa mano ya está <b>armada</b>
    /// (lista para marcar su área) o solo <b>cantada</b> (la ronda extra de aviso).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué un servicio y no estado del nodo.</b> Quien tira (<c>AINode_RollHand</c>) y
    /// quien ramifica según el combo (<c>PcBossHandCombo</c>, dentro de un <c>AINode_If</c>) son
    /// piezas distintas del árbol y no se ven entre sí. La mano es el único dato compartido, y
    /// además la tiene que poder leer la UI ("los cinco números y el combo cantado son públicos").
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Global, state run-scoped: se limpia en <c>OnCombatEnd</c> /
    /// <c>OnRunEnd</c> igual que <c>ThreatenedAreaService</c> / <c>ComboLogService</c>.
    /// </para>
    /// </remarks>
    public interface IBossDiceHandService
    {
        /// <summary>
        /// Publica la mano de <paramref name="ownerGuid"/>, sobrescribiendo la anterior.
        /// <paramref name="armed"/> = false ⇒ la mano queda cantada pero no dispara todavía
        /// (ronda extra de aviso de la Generala grande).
        /// </summary>
        void SetHand(Guid ownerGuid, IReadOnlyList<int> values, string comboId, bool armed);

        /// <summary>
        /// Arma la mano ya cantada de <paramref name="ownerGuid"/> sin re-tirar los dados.
        /// No-op si no hay mano publicada. Devuelve <c>true</c> si pasó de cantada a armada.
        /// </summary>
        bool ArmHand(Guid ownerGuid);

        /// <summary>Mano vigente de <paramref name="ownerGuid"/>; <c>false</c> si no tiró todavía.</summary>
        bool TryGetHand(Guid ownerGuid, out BossDiceHand hand);

        /// <summary>
        /// Cuántas veces puede re-tirar los dados que no le sirven, por tirada. 0 = sin reroll
        /// (Fase 1). Lo sube el setup de Fase 2 vía <c>AINode_SetHandReroll</c>.
        /// </summary>
        void SetRerollsPerRound(Guid ownerGuid, int rerolls);

        /// <summary>Rerolls disponibles por tirada para <paramref name="ownerGuid"/> (0 por default).</summary>
        int GetRerollsPerRound(Guid ownerGuid);

        /// <summary>Olvida la mano y los rerolls de <paramref name="ownerGuid"/>.</summary>
        void Clear(Guid ownerGuid);

        /// <summary>Olvida todo. Usado en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.</summary>
        void ClearAll();
    }

    /// <summary>Snapshot inmutable de la mano de un boss.</summary>
    public readonly struct BossDiceHand
    {
        /// <summary>Valor de <see cref="ComboId"/> cuando la tirada no formó ningún combo (bust).</summary>
        public const string NoCombo = "";

        private static readonly int[] EmptyValues = Array.Empty<int>();

        /// <summary>Caras que salieron, en orden de tirada. Públicas: la UI las muestra.</summary>
        public readonly IReadOnlyList<int> Values;

        /// <summary><c>BaseComboSO.ComboId</c> del combo detectado, o <see cref="NoCombo"/>.</summary>
        public readonly string ComboId;

        /// <summary>
        /// <c>true</c> si la mano ya puede marcar su área. <c>false</c> = cantada este turno,
        /// arma el que viene (la ronda extra de aviso).
        /// </summary>
        public readonly bool Armed;

        public BossDiceHand(IReadOnlyList<int> values, string comboId, bool armed)
        {
            Values = values ?? EmptyValues;
            ComboId = comboId ?? NoCombo;
            Armed = armed;
        }

        /// <summary><c>true</c> si la tirada formó algún combo.</summary>
        public bool HasCombo => !string.IsNullOrEmpty(ComboId);

        /// <summary>Dados que se tiraron — cuántas categorías le quedan disponibles depende de esto.</summary>
        public int DiceCount => Values?.Count ?? 0;
    }
}
