using System;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Pinta en la grilla lo que un enemigo tiene en curso y lo que va a hacer, mientras el mouse
    /// está encima de él.
    /// </summary>
    /// <remarks>
    /// El dibujo sale del hover y no del turno del enemigo: su turno dura segundos y ahí nadie
    /// lee. Lo que pinta no se re-simula nunca — son las áreas que el enemigo ya dejó
    /// comprometidas más la casilla del jugador para un ataque a distancia.
    /// </remarks>
    public sealed class EnemyIntentPreviewOverlay
    {
        private static EnemyIntentPreviewOverlay s_instance;

        public static EnemyIntentPreviewOverlay ResolveOrCreate()
            => s_instance ??= new EnemyIntentPreviewOverlay();

        /// <summary>Pinta todo lo del enemigo: lo que tiene puesto y su próximo ataque.</summary>
        public void Show(Guid enemyId)
        {
        }

        /// <summary>
        /// Pinta sólo lo que sale de <paramref name="subjectGuid"/> — la cruz de una bomba y no
        /// las de sus tres hermanas.
        /// </summary>
        public void ShowForSubject(Guid enemyId, Guid subjectGuid)
        {
        }

        public void Clear()
        {
        }
    }
}
