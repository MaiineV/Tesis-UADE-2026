namespace Rollgeon.Combat.Actions
{
    /// <summary>
    /// Clasificacion de "cosas que un actor puede ejecutar en combate". Replica literal de
    /// TECHNICAL.md §12.6.0. Los valores NO deben reordenarse despues del merge — <see cref="ActionDefinitionSO.Type"/>
    /// serializa el enum como <c>int</c> en los assets del catalogo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada entry del <see cref="ActionCatalogSO"/> declara su <see cref="ActionType"/> para
    /// que el <c>TurnManager</c>, el HUD de acciones (T95a), y herramientas de balance puedan
    /// filtrar sin reflejar sobre <c>BackingAsset</c>.
    /// </para>
    /// <para>
    /// <b>No presente por diseño</b>: <c>EndTurn</c> no es un <see cref="ActionType"/>. El
    /// "fin de turno" del sprint #100 se mapea a <see cref="Defend"/> con <c>EnergyCost=0</c>
    /// y <c>Effect</c> vacio (plan §10 R6).
    /// </para>
    /// </remarks>
    public enum ActionType
    {
        /// <summary>Combo del Contrato de Generala (§5) — wrapped por <see cref="ActionDefinitionSO"/>.</summary>
        Combo = 0,

        /// <summary>Ataque directo sin combo (dado mas alto del GD, AI nodes).</summary>
        Attack = 1,

        /// <summary>Habilidad unica de clase/enemigo con efecto autoreo (§8).</summary>
        Ability = 2,

        /// <summary>Defensa / pasar turno (§12.4).</summary>
        Defend = 3,

        /// <summary>Movimiento en grid (§B). Suele declarar <c>BlockOnRepeat = false</c>.</summary>
        Move = 4,

        /// <summary>Interaccion con objeto del mundo (§7.7).</summary>
        Interact = 5,

        /// <summary>Uso de item activo (§18). <c>BackingAsset</c> apunta al <c>ItemSO</c>.</summary>
        UseItem = 6,

        /// <summary>Skill check de una sola tirada vs umbral (§12.5) — heal, forzar puerta, etc.</summary>
        SkillCheck = 7,
    }
}
