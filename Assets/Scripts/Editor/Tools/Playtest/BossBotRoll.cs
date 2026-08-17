namespace Rollgeon.EditorTools.Playtest
{
    /// <summary>
    /// Las caras que el bot le encola a <c>RiggedRollState</c> antes de cada ataque.
    /// </summary>
    /// <remarks>
    /// **Todos los dados a la misma cara, a propósito.** Una tirada aleatoria suele no formar
    /// ningún combo, y un turno sin combo no hace daño: la captura de ese turno no valida nada
    /// (ni el "-70%" de la mesa, ni el número del jefe bajando). Repetir cara garantiza que el
    /// mejor combo disponible salga siempre, así que cada turno de ataque produce una imagen
    /// con números que mirar.
    ///
    /// La seed rota qué cara se repite, y el turno la corre: así dos corridas de la misma seed
    /// dan exactamente las mismas tiradas —que es el punto de fijarlas— pero cambiar la seed
    /// mueve la pelea de verdad en vez de dar siempre lo mismo.
    ///
    /// <c>RiggedRollState</c> clampea a <c>MaxFace</c> de cada dado, así que pedir 6 sobre un
    /// d4 sale 4 y no hay que conocer el bag acá.
    /// </remarks>
    public static class BossBotRoll
    {
        /// <summary>Caras de un dado estándar; el clamp del roller ajusta los dados chicos.</summary>
        private const int FaceCount = 6;

        public static int FaceFor(int seed, int turn)
        {
            // Módulo positivo: una seed negativa daría un índice negativo y una cara inválida.
            int raw = seed + turn;
            int index = ((raw % FaceCount) + FaceCount) % FaceCount;
            return index + 1;
        }

        public static int[] FacesFor(int seed, int turn, int diceCount)
        {
            if (diceCount <= 0) return new int[0];

            int face = FaceFor(seed, turn);
            var faces = new int[diceCount];
            for (int i = 0; i < diceCount; i++) faces[i] = face;
            return faces;
        }
    }
}
