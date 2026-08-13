using System;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Player
{
    /// <summary>Runtime implementation of <see cref="IPlayerService"/> (§17.G).</summary>
    public sealed class PlayerService : IPlayerService, IDisposable
    {
        public Guid PlayerGuid { get; private set; }
        public Guid RunId { get; private set; }
        public ClassHeroSO CurrentHero { get; private set; }
        public DiceBagSO DiceBag { get; private set; }

        public event Action<ClassHeroSO> OnPlayerSet;
        public event Action OnPlayerCleared;

        public void SetPlayer(ClassHeroSO hero, Guid runId)
            => SetPlayerInternal(hero, runId, Guid.NewGuid());

        /// <summary>
        /// Overload de resume (#0028): preserva el <see cref="PlayerGuid"/> guardado en vez de
        /// generar uno nuevo, para que la cola de turnos / modifiers restaurados (que referencian
        /// ese GUID) matcheen. No está en <see cref="IPlayerService"/> a propósito — solo el
        /// resume production path lo usa. <c>Guid.Empty</c> ⇒ GUID nuevo.
        /// </summary>
        public void SetPlayer(ClassHeroSO hero, Guid runId, Guid restoredGuid)
            => SetPlayerInternal(hero, runId, restoredGuid != Guid.Empty ? restoredGuid : Guid.NewGuid());

        private void SetPlayerInternal(ClassHeroSO hero, Guid runId, Guid playerGuid)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            CurrentHero = hero;
            RunId = runId;
            PlayerGuid = playerGuid;

            // Si el hero ya trae un DiceBagSO concreto en su slot opaco, lo clonamos.
            // Si no, DiceBag queda null y el handoff aplica un fallback (Fase 1).
            DiceBag = null;
            if (hero.StartingDiceBagRef is DiceBagSO heroBag)
            {
                DiceBag = heroBag.Clone();
            }

            OnPlayerSet?.Invoke(hero);
        }

        public void SetDiceBag(DiceBagSO bag)
        {
            if (bag == null)
            {
                Debug.LogWarning("[PlayerService] SetDiceBag(null) — limpiando bag activa.");
                DiceBag = null;
                return;
            }
            DiceBag = bag;
        }

        public void ClearPlayer()
        {
            CurrentHero = null;
            RunId = Guid.Empty;
            PlayerGuid = Guid.Empty;
            DiceBag = null;

            OnPlayerCleared?.Invoke();
        }

        public void Dispose()
        {
            OnPlayerSet = null;
            OnPlayerCleared = null;
        }
    }
}
