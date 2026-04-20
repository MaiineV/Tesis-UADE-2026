using System;
using Rollgeon.Heroes;

namespace Rollgeon.Player
{
    /// <summary>Runtime implementation of <see cref="IPlayerService"/> (§17.G).</summary>
    public sealed class PlayerService : IPlayerService, IDisposable
    {
        public Guid PlayerGuid { get; private set; }
        public Guid RunId { get; private set; }
        public ClassHeroSO CurrentHero { get; private set; }

        public event Action<ClassHeroSO> OnPlayerSet;
        public event Action OnPlayerCleared;

        public void SetPlayer(ClassHeroSO hero, Guid runId)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            CurrentHero = hero;
            RunId = runId;
            PlayerGuid = Guid.NewGuid();

            OnPlayerSet?.Invoke(hero);
        }

        public void ClearPlayer()
        {
            CurrentHero = null;
            RunId = Guid.Empty;
            PlayerGuid = Guid.Empty;

            OnPlayerCleared?.Invoke();
        }

        public void Dispose()
        {
            OnPlayerSet = null;
            OnPlayerCleared = null;
        }
    }
}
