namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Keys de la String Table <c>UI</c> para las familias de enemigo.
    /// </summary>
    /// <remarks>
    /// Enumeradas en <see cref="All"/> por lo mismo que <c>TutorialTextKeys.All</c>: el guard de
    /// tablas necesita una lista que recorrer. Sin eso una key nueva sale con el texto de autor en
    /// español aunque el juego corra en inglés, y nada lo detecta.
    /// </remarks>
    public static class EnemyArchetypeKeys
    {
        public const string Melee = "enemy.archetype.melee";
        public const string Ranged = "enemy.archetype.ranged";
        public const string Support = "enemy.archetype.support";

        /// <summary>Un jefe sin familia autorada. "Jefe" solo es verdad y sirve.</summary>
        public const string Boss = "enemy.archetype.boss";

        /// <summary>
        /// Un jefe con familia. El prefijo va <b>adentro</b> del formato y no concatenado acá para
        /// que el separador sea autorable por idioma.
        /// </summary>
        public const string BossFormat = "enemy.archetype.boss_format";

        public static readonly string[] All =
        {
            Melee, Ranged, Support, Boss, BossFormat,
        };

        /// <summary>
        /// Key de una familia, o <c>null</c> para <see cref="EnemyArchetype.Unset"/> — que es lo
        /// que apaga la fila.
        /// </summary>
        public static string KeyFor(EnemyArchetype archetype) => archetype switch
        {
            EnemyArchetype.Melee => Melee,
            EnemyArchetype.Ranged => Ranged,
            EnemyArchetype.Support => Support,
            _ => null,
        };

        /// <summary>
        /// Texto de autor de cada key. Vive acá y no en la vista por lo mismo que
        /// <c>AIIntentTextKeys.RuleFallback</c>: un build con bundles viejos cae a esto.
        /// </summary>
        public static string Fallback(string key) => key switch
        {
            Melee => "Cuerpo a cuerpo",
            Ranged => "Rango",
            Support => "Soporte",
            Boss => "Jefe",
            BossFormat => "Jefe · {0}",
            _ => string.Empty,
        };
    }
}
