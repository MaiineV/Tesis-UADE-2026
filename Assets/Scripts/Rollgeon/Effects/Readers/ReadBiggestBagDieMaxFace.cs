using System;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Player;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Cara máxima del dado MÁS GRANDE de la bolsa del jugador (Feature#0084, Blood
    /// Transfusion banda A: "cara máxima del dado más grande de la bolsa"). No es la cara
    /// que salió en la tirada — es el techo teórico del dado con más caras que el jugador
    /// tiene equipado, sin importar el resultado del roll.
    /// </summary>
    /// <remarks>
    /// Sin <see cref="IPlayerService"/> registrado, o bolsa vacía/null: fallback 6 (d6, el
    /// dado más chico de la build estándar) — nunca 0, que rompería una fórmula multiplicativa.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadBiggestBagDieMaxFace : EffectIntReader
    {
        private const int FallbackMaxFace = 6;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var players) || players == null)
                return FallbackMaxFace;

            var bag = players.DiceBag;
            if (bag?.Dice == null || bag.Dice.Count == 0) return FallbackMaxFace;

            int best = 0;
            for (int i = 0; i < bag.Dice.Count; i++)
            {
                int face = bag.Dice[i].MaxFace();
                if (face > best) best = face;
            }
            return best > 0 ? best : FallbackMaxFace;
        }
    }
}
