using System;
using UnityEngine;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — TECHNICAL.md §9.2. Base polimórfica del bag runtime de valores
    /// almacenados en un <see cref="BaseBehavior"/>. La foundation real de Behaviors
    /// completa lifecycle, snapshot para FeedbackRequest y consumidores. Acá sólo se
    /// declara la base + 2 subtipos mínimos para que los effects ejemplares escriban.
    /// </summary>
    [Serializable]
    public abstract class BaseBehaviorStoredValue
    {
    }

    /// <summary>
    /// [STUB] — TECHNICAL.md §9.2. Valor flotante single-key. Los feedbacks animator
    /// parameter lo consumen downstream.
    /// </summary>
    [Serializable]
    public class FloatBehaviorValue : BaseBehaviorStoredValue
    {
        public float Value;
    }

    /// <summary>
    /// [STUB] — TECHNICAL.md §9.2. Payload para los floating damage / heal numbers
    /// que <see cref="Rollgeon.Effects.Concretes.EffDamage"/> escribe durante apply.
    /// </summary>
    [Serializable]
    public class FloatingNumberBehaviorValue : BaseBehaviorStoredValue
    {
        public float Value;
        public Vector3 Offset;
        public Guid TargetEntityGuid;
        public float Delay;
    }
}
