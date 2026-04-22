using System;
using Rollgeon.Dice;
using Rollgeon.Heroes;

namespace Rollgeon.Player
{
    /// <summary>Servicio global que expone la identidad del jugador activo (§17.G).</summary>
    public interface IPlayerService
    {
        /// <summary>GUID unico del jugador activo. <see cref="Guid.Empty"/> si no hay player.</summary>
        Guid PlayerGuid { get; }

        /// <summary>GUID de la run en curso.</summary>
        Guid RunId { get; }

        /// <summary>Clase heroe seleccionada para la run actual.</summary>
        ClassHeroSO CurrentHero { get; }

        /// <summary>
        /// Bolsa runtime del jugador (clon del bag de la clase, TECHNICAL.md §6.2).
        /// <c>null</c> hasta que <see cref="SetPlayer"/> resuelva una bag válida.
        /// </summary>
        DiceBagSO DiceBag { get; }

        /// <summary>Establece el jugador activo con la clase y run indicados.</summary>
        void SetPlayer(ClassHeroSO hero, Guid runId);

        /// <summary>
        /// Reemplaza la <see cref="DiceBag"/> activa con un bag construido externamente
        /// (Fase 2 — <c>BuildSelectionScreen</c>). El servicio asume ownership del clon.
        /// </summary>
        void SetDiceBag(DiceBagSO bag);

        /// <summary>Limpia el estado del jugador (post-run o reset).</summary>
        void ClearPlayer();

        /// <summary>Disparado tras <see cref="SetPlayer"/>.</summary>
        event Action<ClassHeroSO> OnPlayerSet;

        /// <summary>Disparado tras <see cref="ClearPlayer"/>.</summary>
        event Action OnPlayerCleared;
    }
}
