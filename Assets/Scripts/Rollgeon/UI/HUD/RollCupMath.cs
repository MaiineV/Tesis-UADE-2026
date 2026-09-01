using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Transición visual del vaso de generala ante un cambio del pool de rolls.
    /// Clasificada por delta de <c>current</c>: el pool no emite eventos
    /// dedicados de gasto/recupero — solo <c>OnPlayerRollsChanged</c>.
    /// </summary>
    public enum RollCupTransition
    {
        None,
        Spend,
        SpendToEmpty,
        Recover,
        RecoverFromEmpty
    }

    /// <summary>
    /// Decisiones puras del vaso de generala (clasificador de transiciones y
    /// winding del flip), separadas del MonoBehaviour para testearlas EditMode
    /// (precedente <see cref="Rollgeon.UI.Menu.MenuJuiceMath"/>).
    /// </summary>
    public static class RollCupMath
    {
        // Winding del flip en grados Z. El flip-up continúa hacia 360 (misma
        // dirección que el flip-down) para que recuperar "complete la vuelta"
        // en vez de rebobinarla — con quaternions el shortest-path de un arco
        // de 180° es ambiguo, por eso los tweens usan estos floats.
        public const float UprightZ = 0f;
        public const float FaceDownZ = 180f;
        public const float FlipUpToZ = 360f;

        /// <summary>Con 0 rolls el vaso descansa boca abajo sobre la mesa.</summary>
        public static bool IsFaceDown(int current) => current == 0;

        // Swap de sprite a mitad del giro. El vaso tiene dos dibujos: parado
        // (VasoGenerala) y boca abajo (VasoGeneralaFlip, ya dibujado invertido).
        // En la mitad de cada media vuelta (90° y 270°) el vaso está horizontal
        // y ambos sprites apuntan la boca al mismo lado: ahí se cambia el
        // dibujo sin salto visual. Como el Flip ya viene invertido, mientras se
        // muestra se compensa la rotación en -180 — así a 180° lógico el Flip
        // queda a 0° físico (boca abajo real) en vez de girado (= parado otra vez).
        private const float FlipSpriteEnterZ = 90f;
        private const float FlipSpriteExitZ = 270f;
        private const float FlipSpriteRotationOffset = -180f;

        /// <summary>
        /// True cuando el ángulo lógico (Z, en grados, cualquier winding) cae en
        /// la mitad "boca abajo" de la vuelta: [90, 270) módulo 360.
        /// </summary>
        public static bool ShowsFlipSprite(float logicalZ)
        {
            float a = Mathf.Repeat(logicalZ, 360f);
            return a >= FlipSpriteEnterZ && a < FlipSpriteExitZ;
        }

        /// <summary>
        /// Rotación Z a aplicar al RectTransform para un ángulo lógico dado.
        /// Con el sprite Flip visible se compensa el pre-invertido del dibujo;
        /// sin sprite Flip cableado el ángulo físico es el lógico (comportamiento
        /// previo: mismo dibujo girado 180°).
        /// </summary>
        public static float VisualZ(float logicalZ, bool flipSpriteShown)
            => flipSpriteShown ? logicalZ + FlipSpriteRotationOffset : logicalZ;

        /// <summary>
        /// Clasifica el cambio de pool. <paramref name="previous"/> negativo
        /// significa "sin dato previo" (primer fetch o reentrada a combate):
        /// ahí no hay transición que animar, solo pose.
        /// </summary>
        public static RollCupTransition Classify(int previous, int current)
        {
            if (previous < 0 || previous == current) return RollCupTransition.None;

            if (current < previous)
            {
                return current == 0 ? RollCupTransition.SpendToEmpty : RollCupTransition.Spend;
            }

            return previous == 0 ? RollCupTransition.RecoverFromEmpty : RollCupTransition.Recover;
        }

        /// <summary>
        /// Re-chequeo en la frontera shake→flip: el flip-down solo se encadena
        /// si al terminar el shake la pose objetivo sigue siendo boca abajo
        /// (un recupero puede colarse durante los ~0.3s del shake).
        /// </summary>
        public static bool ShouldChainFlipDown(RollCupTransition transition, bool targetFaceDownNow)
            => transition == RollCupTransition.SpendToEmpty && targetFaceDownNow;
    }
}
