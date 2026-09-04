namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Las keys de texto que publican los <see cref="IAIIntentNode"/>, enumeradas.
    /// </summary>
    /// <remarks>
    /// Enumeradas y no sueltas en cada nodo porque el guard de localización necesita una lista
    /// que recorrer: las cuatro vivieron una rama entera sin entry en las tablas y salían en
    /// español con el juego en inglés, y nada lo detectó.
    /// </remarks>
    public static class AIIntentTextKeys
    {
        public const string Ignite = "intent.ignite";

        /// <summary>
        /// El área que no es un cono sino el paño entero menos un hueco. Key propia porque el
        /// mismo nodo prende las dos: con una sola, el jugador lee "Bola de fuego" para algo de
        /// lo que no se escapa moviéndose al costado.
        /// </summary>
        public const string BurnRoom = "intent.burn_room";

        public const string RangedShot = "intent.ranged_shot";

        /// <summary>
        /// Texto de autor del título del disparo. Con nombre porque lo piden dos lugares: el nodo
        /// que dispara y el panel, que rotula así al bestiario ranged aunque su ataque venga del
        /// nodo genérico. Dos literales iguales ya se habían separado una vez.
        /// </summary>
        public const string RangedShotFallback = "Disparo";
        public const string BombField = "intent.bomb_field";
        public const string BombBlast = "intent.bomb_blast";

        /// <summary>Key propia: comparte el ciclo melee con el mandoble y con la del disparo las dos fichas se leían igual.</summary>
        public const string CashierShove = "intent.cashier_shove";

        /// <summary>Lo que le queda a una moneda antes de que la caja se la lleve.</summary>
        public const string CashierVault = "intent.cashier_vault";

        /// <summary>El reloj de las monedas resumido en el panel del jefe, no en el de cada moneda.</summary>
        public const string CashierCoins = "intent.cashier_coins";

        /// <summary>El turno en que avisa el 3×3. Key propia: el aviso y el cobro dicen cosas distintas.</summary>
        public const string CashierSlam = "intent.cashier_slam";

        /// <summary>El turno en que lo cobra, con el área ya congelada.</summary>
        public const string CashierSlamDue = "intent.cashier_slam_due";

        /// <summary>La marca telegrafiada ya congelada, leída por el nodo que la cobra.</summary>
        public const string Telegraph = "intent.telegraph";

        /// <summary>El golpe genérico del bestiario común (behavior componible, melee o ranged).</summary>
        public const string Attack = "intent.attack";

        /// <summary>Frase compartida de lo que una intención deja en el piso.</summary>
        public const string Leaves = "intent.leaves";

        public static readonly string[] All =
        {
            Ignite, BurnRoom, RangedShot, BombField, BombBlast, Telegraph, Attack, Leaves,
            CashierShove, CashierVault, CashierCoins, CashierSlam, CashierSlamDue,
        };

        /// <summary>
        /// Texto de autor de cada regla, con sus <c>{n}</c>. Vive acá y no en la vista: es la
        /// misma decisión que declarar la key, y un build con bundles viejos cae a esto.
        /// </summary>
        /// <remarks>
        /// Los argumentos son siempre los mismos tres —daño, cantidad, turnos— y cada frase usa
        /// los que le sirven. Una frase vacía es una tarjeta de solo título.
        /// </remarks>
        public static string RuleFallback(string key) => key switch
        {
            Ignite => "Prende un cono de fuego.",
            BurnRoom => "Prende la sala entera menos lo que rodea al jefe.",
            // Vacía a propósito: el título dice qué hace, el número de la tarjeta dice cuánto, y
            // "desde lejos" lo dice la familia del bicho arriba del panel. No quedaba nada.
            RangedShot => string.Empty,
            BombField => "Siembra <b>{1}</b> bombas al azar.",
            // Vacía a propósito: el título ya dice qué pasa y el badge cuánto falta. La cruz se
            // ve en el piso, y el fuego que queda lo cuenta la casilla al pasarle el mouse.
            BombBlast => string.Empty,
            // Vacías: el título dice qué pasa y el número de la tarjeta cuánto. Las casillas
            // marcadas ya se ven en el paño al hoverear.
            Telegraph => string.Empty,
            Attack => string.Empty,
            CashierShove => "Te empuja <b>{1}</b> casillas y te cobra parte del oro que lleves encima.",
            CashierVault => string.Empty,
            CashierCoins => "La caja se lleva una por turno; quedan <b>{1}</b> en el piso.",
            CashierSlam => "Marca un área de 3×3 donde estés parado y la cobra al turno siguiente.",
            CashierSlamDue => "Cae en el área marcada, no donde estés.",
            Leaves => "Deja fuego: <b>{0}</b> al entrar, <b>{1}</b> por turno, {2} rondas.",
            _ => string.Empty,
        };
    }
}
