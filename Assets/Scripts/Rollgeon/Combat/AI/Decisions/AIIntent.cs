using System;
using System.Collections.Generic;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Tiles;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Lo que un enemigo va a hacer, en datos: el jugador lo lee en su turno antes de que pase.
    /// </summary>
    /// <remarks>
    /// Datos y nada de formato — la UI arma la frase. Una intención es siempre una afirmación,
    /// nunca una estimación: un nodo que no puede afirmar algo no lo pone acá (ver
    /// <see cref="IAIIntentNode"/>).
    /// </remarks>
    public readonly struct AIIntent
    {
        /// <summary>
        /// Fuente en <c>IThreatenedAreaService</c> y en el overlay. <see cref="Guid.Empty"/> =
        /// esta intención no vive ahí (el disparo no marca nada).
        /// </summary>
        public readonly Guid ChannelKey;

        /// <summary>
        /// De quién es esto. <see cref="Guid.Empty"/> = del enemigo mismo; si no, el objeto que
        /// lo genera — la bomba de la que sale esta cruz, para que el hover de la bomba muestre
        /// la suya y no las tres.
        /// </summary>
        public readonly Guid SubjectGuid;

        public readonly string LabelKey;
        public readonly string LabelFallback;

        /// <summary>Golpe directo. <c>0</c> = no pega por sí misma; lo que cobra es lo que deja.</summary>
        public readonly int Damage;

        public readonly AttackKind Kind;

        /// <summary>
        /// Celdas afectadas. <b>Vacío significa que no se sabe</b> — nunca una estimación. Las
        /// ranuras de una siembra se sortean al sembrar, así que ahí no hay nada que prometer.
        /// </summary>
        public readonly IReadOnlyCollection<GridCoord> Tiles;

        /// <summary>Lo que queda en el piso después. <c>null</c> = no deja nada.</summary>
        public readonly SpecialTileDefinitionSO Leaves;

        public readonly int LeavesRounds;

        /// <summary>El número del que habla el label — las bombas que siembra, por ejemplo.</summary>
        public readonly int Amount;

        /// <summary>Turnos hasta que pase. <c>0</c> = en su próximo turno.</summary>
        public readonly int TurnsAway;

        public AIIntent(string labelKey, string labelFallback, int damage, AttackKind kind,
                        IReadOnlyCollection<GridCoord> tiles = null,
                        SpecialTileDefinitionSO leaves = null, int leavesRounds = 0,
                        int amount = 0, int turnsAway = 0,
                        Guid channelKey = default, Guid subjectGuid = default)
        {
            LabelKey = labelKey;
            LabelFallback = labelFallback;
            Damage = damage;
            Kind = kind;
            Tiles = tiles ?? Array.Empty<GridCoord>();
            Leaves = leaves;
            LeavesRounds = leavesRounds;
            Amount = amount;
            TurnsAway = turnsAway;
            ChannelKey = channelKey;
            SubjectGuid = subjectGuid;
        }
    }
}
