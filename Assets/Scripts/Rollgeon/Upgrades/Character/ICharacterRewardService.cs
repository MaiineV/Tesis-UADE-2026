using System;
using System.Collections.Generic;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// API pública del Canal Personaje. La consume el
    /// <see cref="CharacterRewardPedestalInteractable"/> al claim y eventualmente
    /// la UI / HUD que muestra los rewards activos.
    /// </summary>
    public interface ICharacterRewardService
    {
        /// <summary>
        /// Aplica el reward al stat base del player vía <c>Modifier&lt;int&gt;</c>
        /// con <c>ModifierLifetime.Run</c>. Idempotente respecto al estado del
        /// pedestal — el service del room despawnea el resto al claim.
        /// </summary>
        bool Apply(CharacterRewardSO reward);

        /// <summary>Rewards ya aplicados en la run actual. Diagnostic / HUD.</summary>
        IReadOnlyList<CharacterRewardSO> ClaimedRewards { get; }

        /// <summary>
        /// Callback del <see cref="CharacterRewardPedestalInteractable"/> al
        /// interactuar. Aplica el reward del slot + marca todos los slots de la
        /// room como claimed (las opciones son mutuamente exclusivas — uno de 3) +
        /// despawnea los pedestales hermanos.
        /// </summary>
        void NotifyPedestalClaimed(Guid roomInstanceId, string spawnPointId);
    }
}
