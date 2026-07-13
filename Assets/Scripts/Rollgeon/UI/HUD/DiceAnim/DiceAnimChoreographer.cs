using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Snapshot plano del tuning de animación. Separado del SO para que el
    /// choreographer y sus tests no dependan de assets ni de PrimeTween.
    /// </summary>
    public struct DiceAnimTimings
    {
        public float SpinSeconds;
        public float SpinTickSeconds;
        public float SpinDecelerationPower;
        public float SpinStaggerSeconds;
        public float SpinTurns;
        public int PreviewFaceMax;

        public float RaiseOffsetY;
        public float RaiseSeconds;
        public float LowerSeconds;

        public float ThrowSeconds;
        public float ThrowStaggerSeconds;
        public float ThrowEndScale;
        public float ThrowFadePortion;

        public float DiscardSeconds;
        public float DiscardEndScale;

        public float OutroHoldSeconds;

        /// <summary>Mismos valores default que <see cref="DiceUiAnimationSettingsSO"/>.</summary>
        public static DiceAnimTimings Defaults => new DiceAnimTimings
        {
            SpinSeconds = 0.5f,
            SpinTickSeconds = 0.06f,
            SpinDecelerationPower = 2f,
            SpinStaggerSeconds = 0.05f,
            SpinTurns = 2f,
            PreviewFaceMax = 6,
            RaiseOffsetY = 18f,
            RaiseSeconds = 0.12f,
            LowerSeconds = 0.10f,
            ThrowSeconds = 0.35f,
            ThrowStaggerSeconds = 0.05f,
            ThrowEndScale = 0.6f,
            ThrowFadePortion = 0.5f,
            DiscardSeconds = 0.25f,
            DiscardEndScale = 0.7f,
            OutroHoldSeconds = 0.05f,
        };
    }

    /// <summary>Plan de spin para un slot: cuándo arranca, cuánto dura, cuántas caras preview.</summary>
    public readonly struct DiceSpinPlan
    {
        public readonly bool Spins;
        public readonly float Delay;
        public readonly float Duration;
        public readonly int TickCount;

        public DiceSpinPlan(bool spins, float delay, float duration, int tickCount)
        {
            Spins = spins;
            Delay = delay;
            Duration = duration;
            TickCount = tickCount;
        }
    }

    public enum DiceOutroKind
    {
        Skip,
        Throw,
        Discard,
    }

    /// <summary>Plan de outro para un slot: throw al centro o descarte en el lugar.</summary>
    public readonly struct DiceOutroPlan
    {
        public readonly DiceOutroKind Kind;
        public readonly float Delay;
        public readonly float Duration;
        public readonly Vector2 TargetPosition;
        public readonly float EndScale;
        public readonly float FadeDelay;
        public readonly float FadeDuration;

        public DiceOutroPlan(DiceOutroKind kind, float delay, float duration,
            Vector2 targetPosition, float endScale, float fadeDelay, float fadeDuration)
        {
            Kind = kind;
            Delay = delay;
            Duration = duration;
            TargetPosition = targetPosition;
            EndScale = endScale;
            FadeDelay = fadeDelay;
            FadeDuration = fadeDuration;
        }

        public static DiceOutroPlan Skipped => new DiceOutroPlan(
            DiceOutroKind.Skip, 0f, 0f, Vector2.zero, 1f, 0f, 0f);
    }

    /// <summary>
    /// Decisiones de coreografía como data pura (sin GameObjects, sin tweens): qué
    /// slot anima, cuándo, hacia dónde y por cuánto tiempo. Los animators solo
    /// ejecutan estos planes — así los tests EditMode validan la coreografía completa
    /// sin levantar escena. Patrón <see cref="FloatingNumberFormat"/>.
    /// </summary>
    public static class DiceAnimChoreographer
    {
        /// <summary>
        /// Un plan por slot. Los holdeados no spinean (el reroll los saltea) y el
        /// stagger acumula solo sobre los que sí, para que el primer dado visible
        /// siempre arranque en t=0. SpinSeconds &lt;= 0 ⇒ nadie spinea (path instantáneo).
        /// </summary>
        public static DiceSpinPlan[] BuildSpinPlans(IReadOnlyList<bool> willReveal, in DiceAnimTimings t)
        {
            var plans = new DiceSpinPlan[willReveal?.Count ?? 0];
            if (willReveal == null || t.SpinSeconds <= 0f) return plans;

            int spinIndex = 0;
            int ticks = TickCount(t.SpinSeconds, t.SpinTickSeconds);
            for (int i = 0; i < willReveal.Count; i++)
            {
                if (!willReveal[i]) continue;
                float delay = spinIndex * Mathf.Max(0f, t.SpinStaggerSeconds);
                plans[i] = new DiceSpinPlan(true, delay, t.SpinSeconds, ticks);
                spinIndex++;
            }
            return plans;
        }

        /// <summary>
        /// Un plan por slot para el outro del confirm: holdeado activo → Throw al
        /// centro (stagger acumulado sobre los throws), no holdeado activo → Discard
        /// inmediato en el lugar, inactivo → Skip.
        /// </summary>
        public static DiceOutroPlan[] BuildOutroPlans(
            IReadOnlyList<bool> held,
            IReadOnlyList<bool> active,
            IReadOnlyList<Vector2> slotPositions,
            Vector2 centerLocal,
            in DiceAnimTimings t)
        {
            int count = active?.Count ?? 0;
            var plans = new DiceOutroPlan[count];
            if (active == null) return plans;

            int throwIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if (!active[i])
                {
                    plans[i] = DiceOutroPlan.Skipped;
                    continue;
                }

                bool isHeld = held != null && i < held.Count && held[i];
                if (isHeld && t.ThrowSeconds > 0f)
                {
                    float fadeDuration = t.ThrowSeconds * Mathf.Clamp01(t.ThrowFadePortion);
                    plans[i] = new DiceOutroPlan(
                        DiceOutroKind.Throw,
                        delay: throwIndex * Mathf.Max(0f, t.ThrowStaggerSeconds),
                        duration: t.ThrowSeconds,
                        targetPosition: centerLocal,
                        endScale: t.ThrowEndScale,
                        fadeDelay: t.ThrowSeconds - fadeDuration,
                        fadeDuration: fadeDuration);
                    throwIndex++;
                }
                else if (!isHeld && t.DiscardSeconds > 0f)
                {
                    Vector2 ownPos = slotPositions != null && i < slotPositions.Count
                        ? slotPositions[i]
                        : Vector2.zero;
                    plans[i] = new DiceOutroPlan(
                        DiceOutroKind.Discard,
                        delay: 0f,
                        duration: t.DiscardSeconds,
                        targetPosition: ownPos,
                        endScale: t.DiscardEndScale,
                        fadeDelay: 0f,
                        fadeDuration: t.DiscardSeconds);
                }
                else
                {
                    // Duración <= 0 para su tipo ⇒ ese slot se limpia instantáneo.
                    plans[i] = DiceOutroPlan.Skipped;
                }
            }
            return plans;
        }

        /// <summary>Duración total del outro (máximo fin de tween + hold). 0 si nadie anima.</summary>
        public static float OutroTotalSeconds(IReadOnlyList<DiceOutroPlan> plans, float holdSeconds)
        {
            if (plans == null) return 0f;
            float latest = 0f;
            for (int i = 0; i < plans.Count; i++)
            {
                var p = plans[i];
                if (p.Kind == DiceOutroKind.Skip) continue;
                float end = p.Delay + Mathf.Max(p.Duration, p.FadeDelay + p.FadeDuration);
                if (end > latest) latest = end;
            }
            return latest <= 0f ? 0f : latest + Mathf.Max(0f, holdSeconds);
        }

        /// <summary>Cantidad de cambios de cara durante el spin. Intervalo &lt;= 0 ⇒ 0 ticks.</summary>
        public static int TickCount(float spinSeconds, float tickSeconds)
        {
            if (spinSeconds <= 0f || tickSeconds <= 0f) return 0;
            return Mathf.FloorToInt(spinSeconds / tickSeconds);
        }

        /// <summary>
        /// Momento (segundos desde el inicio del spin) del tick <paramref name="tickIndex"/>
        /// (1-based). Mapeo ease-in (n^p) con exponente &gt; 1 ⇒ los primeros ticks caen
        /// pegados y los intervalos crecen hacia el reveal — el dado "frena" como uno
        /// físico. Con p=1 el ritmo es constante.
        /// </summary>
        public static float TickTime(int tickIndex, int tickCount, float spinSeconds, float decelerationPower)
        {
            if (tickCount <= 0 || spinSeconds <= 0f) return 0f;
            float n = Mathf.Clamp01((float)tickIndex / tickCount);
            float p = Mathf.Max(1f, decelerationPower);
            return spinSeconds * Mathf.Pow(n, p);
        }

        /// <summary>
        /// Próxima cara preview en [1, faceMax], nunca repite la anterior (con faceMax
        /// &gt; 1) para que el ciclado siempre se lea como movimiento.
        /// </summary>
        public static int NextPreviewFace(System.Random rng, int faceMax, int previousFace)
        {
            if (rng == null || faceMax <= 1) return 1;
            int face = 1 + rng.Next(faceMax);
            if (face != previousFace) return face;
            // Corrimiento circular: garantiza distinto sin re-tirar (determinista con seed).
            return 1 + face % faceMax;
        }

        /// <summary>Rango de preview: cubre la cara real aunque supere el máximo configurado.</summary>
        public static int PreviewFaceRange(int configuredMax, int finalFace)
            => Mathf.Max(2, Mathf.Max(configuredMax, finalFace));
    }
}
