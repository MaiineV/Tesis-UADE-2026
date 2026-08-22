using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Telegraph de <b>canal secundario</b>: misma semántica que
    /// <see cref="AINode_TelegraphMark"/> + <see cref="AINode_ExecuteTelegraph"/> (marco en el turno
    /// N, cobro en el N+1) pero bajo un id de fuente derivado de <see cref="ChannelId"/>. Existe
    /// porque <see cref="IThreatenedAreaService"/> guarda <i>un</i> área pendiente por fuente y
    /// <see cref="IThreatenedAreaService.Mark"/> sobrescribe la anterior, así que un boss que
    /// amenaza dos cosas el mismo turno perdería una de las dos marcas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se cablea como dos instancias con el mismo <see cref="ChannelId"/>: la de
    /// <see cref="TelegraphStep.Execute"/> va <b>fuera</b> de cualquier gate, porque el aviso hay que
    /// cobrarlo el turno siguiente aunque ese turno no se marque de nuevo.
    /// </para>
    /// <para>
    /// Shapes soportadas: sólo las centradas (SquareAroundSelf / SquareAroundPlayer / Row / Column /
    /// HalfRoom); DirectionalBand y ScatteredSquares no.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_AuxTelegraph : AIActionNode
    {
        public enum TelegraphStep
        {
            /// <summary>Marca el área para cobrarla el próximo turno del boss.</summary>
            Mark,

            /// <summary>Cobra el área marcada el turno anterior en este canal.</summary>
            Execute,
        }

        [Tooltip("Mark = marca el área de este canal. Execute = cobra la marca del turno anterior.")]
        public TelegraphStep Step = TelegraphStep.Mark;

        [Tooltip("Nombre del canal. Las dos instancias (Mark y Execute) del mismo aviso tienen que " +
                 "compartirlo; dos avisos distintos del mismo boss nunca.")]
        public string ChannelId = "aux";

        [Tooltip("Forma del área. SquareAroundSelf = anillo alrededor del propio boss.")]
        [ShowIf(nameof(Step), TelegraphStep.Mark)]
        public ThreatShape Shape = ThreatShape.SquareAroundSelf;

        [Tooltip("Radio del cuadrado (1 ⇒ 3×3) o ancho de la franja. Ignorado en HalfRoom.")]
        [MinValue(0)]
        [ShowIf(nameof(Step), TelegraphStep.Mark)]
        public int Size = 1;

        [Tooltip("Eje de corte para HalfRoom.")]
        [ShowIf(nameof(Shape), ThreatShape.HalfRoom)]
        public HalfRoomAxis HalfAxis = HalfRoomAxis.Vertical;

        [Tooltip("Daño que cobra el próximo turno si el jugador sigue dentro del área.")]
        [MinValue(0)]
        [ShowIf(nameof(Step), TelegraphStep.Mark)]
        public int Damage = 12;

        [Tooltip("Tipo de ataque del DamageContext al ejecutar.")]
        [ShowIf(nameof(Step), TelegraphStep.Mark)]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Color del overlay de este canal, para que no se lea igual que el telegraph principal.")]
        [ShowIf(nameof(Step), TelegraphStep.Mark)]
        public Color OverlayTint = new Color(0.55f, 0.35f, 0.95f, 0.55f);

        public override string NodeName => Step == TelegraphStep.Mark
            ? $"Aux Telegraph Mark ({ChannelId}: {Shape}, dmg {Damage})"
            : $"Aux Telegraph Execute ({ChannelId})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var channel = ChannelGuid(context.SelfGuid, ChannelId);
            return Step == TelegraphStep.Execute ? Execute(context, channel) : Mark(context, channel);
        }

        // ======================================================================
        // Mark
        // ======================================================================

        private AIResult Mark(AIContext context, Guid channel)
        {
            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;

            if (Shape == ThreatShape.DirectionalBand || Shape == ThreatShape.ScatteredSquares)
            {
                Debug.LogWarning($"[AINode_AuxTelegraph] Shape {Shape} no soportada en el canal " +
                                 "secundario — usá el TelegraphMark principal para esas formas.");
                return AIResult.Failed;
            }

            var anchor = Shape == ThreatShape.SquareAroundSelf ? context.SelfGuid : context.PlayerGuid;
            if (!grid.TryGetPosition(anchor, out var center)) return AIResult.Failed;

            var tiles = ThreatAreaShape.Compute(grid, center, Shape, Size, HalfAxis);
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"[AINode_AuxTelegraph] Área vacía (shape={Shape}) — no se marca nada.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_AuxTelegraph] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            threat.Mark(channel, tiles, Damage, Kind);
            ThreatTelegraphOverlay.ResolveOrCreate().Show(channel, tiles, OverlayTint);
            return AIResult.Succeeded;
        }

        // ======================================================================
        // Execute
        // ======================================================================

        /// <remarks>
        /// Delega en <see cref="AINode_ExecuteTelegraph"/> con un <see cref="AIContext"/> armado a
        /// mano cuyo <c>SelfGuid</c> es el canal.
        /// </remarks>
        private static AIResult Execute(AIContext context, Guid channel)
        {
            var channelContext = new AIContext
            {
                SelfGuid = channel,
                PlayerGuid = context.PlayerGuid,
                Grid = context.Grid,
                DamagePipeline = context.DamagePipeline,
                Attributes = context.Attributes,
                Rng = context.Rng,
            };

            new AINode_ExecuteTelegraph().Tick(channelContext);
            return AIResult.Succeeded;
        }

        // ======================================================================
        // Identidad del canal
        // ======================================================================

        /// <summary>
        /// Guid derivado: mismo boss + mismo <paramref name="channel"/> ⇒ mismo id en todos los
        /// turnos, sin un servicio que reparta ids. El hash se pliega sobre los últimos 4 bytes del
        /// guid del boss, así dos bosses nunca comparten canal ni chocan con su telegraph principal.
        /// </summary>
        public static Guid ChannelGuid(Guid self, string channel)
        {
            var bytes = self.ToByteArray();
            int hash = StableHash(channel);

            bytes[12] ^= (byte)(hash & 0xFF);
            bytes[13] ^= (byte)((hash >> 8) & 0xFF);
            bytes[14] ^= (byte)((hash >> 16) & 0xFF);
            bytes[15] ^= (byte)((hash >> 24) & 0xFF);
            return new Guid(bytes);
        }

        /// <remarks>
        /// Hash propio y no <see cref="string.GetHashCode()"/>: ese no es estable entre procesos, y
        /// un id de canal que cambia entre sesiones rompería un resume a mitad de aviso. El fallback
        /// distinto de cero evita que un canal sin nombre colisione con el guid del boss.
        /// </remarks>
        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(value))
                    foreach (char c in value) hash = hash * 31 + c;
                return hash == 0 ? 0x5C0FFEE : hash;
            }
        }
    }
}
