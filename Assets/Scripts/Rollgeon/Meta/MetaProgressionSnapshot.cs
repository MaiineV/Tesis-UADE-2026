using System;
using System.Collections.Generic;

namespace Rollgeon.Meta
{
    /// <summary>
    /// DTO serializable del estado de meta-progresión (#164). Es lo que
    /// <see cref="MetaProgressionState.CaptureState"/> produce y lo que el
    /// <see cref="FileMetaSaveStore"/> persiste como JSON (formato compatible
    /// con <c>JsonUtility</c>: solo fields públicos, sin diccionarios).
    /// </summary>
    [Serializable]
    public class MetaProgressionSnapshot
    {
        /// <summary>Claves <c>categoría:targetId</c> de elementos desbloqueados.</summary>
        public List<string> UnlockedTargetKeys = new List<string>();

        /// <summary><c>UnlockId</c>s de definiciones ya cumplidas (para no re-notificar).</summary>
        public List<string> CompletedUnlockIds = new List<string>();

        /// <summary>Contador de consistencia: runs ganadas consecutivas. Se resetea al morir.</summary>
        public int ConsecutiveWins;

        /// <summary>Contador de acumulación: clases distintas jugadas. NO se resetea al morir.</summary>
        public List<string> ClassesPlayed = new List<string>();
    }
}
