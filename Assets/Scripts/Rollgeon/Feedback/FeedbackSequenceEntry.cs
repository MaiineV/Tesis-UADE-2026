using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Una secuencia autorada con nombre, guardada en el <see cref="FeedbackDBSO"/>.
    /// Mismos <see cref="FeedbackSequenceStep"/> que autora un <c>EffPlaySequence</c>,
    /// pero direccionable por id. TECHNICAL.md §10.8.
    /// </summary>
    /// <remarks>
    /// Existe para las secuencias que dispara <b>código</b>, no un effect autorado: la
    /// muerte de un enemigo la lanza el <c>CombatDeathWatcher</c>, que no tiene dónde
    /// autorar steps inline. Sin esto, la composición (qué VFX, cuánto shake) quedaría
    /// hardcodeada en C# y tunearla exigiría recompilar.
    /// </remarks>
    [Serializable]
    public class FeedbackSequenceEntry
    {
        [Tooltip("Id único de la secuencia. Lo referencian los callers desde código.")]
        public string SequenceId;

        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true)]
        public List<FeedbackSequenceStep> Steps = new List<FeedbackSequenceStep>();
    }
}
