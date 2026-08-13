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
    /// Telegraph de <b>canal secundario</b>: marca y ejecuta un área con la misma semántica de
    /// <see cref="AINode_TelegraphMark"/> + <see cref="AINode_ExecuteTelegraph"/> (marco en el turno N,
    /// cobro en el N+1), pero bajo un id de fuente propio derivado de <see cref="ChannelId"/>, así que
    /// <b>no se pisa con el telegraph principal del boss</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existe.</b> <see cref="IThreatenedAreaService"/> guarda <i>un</i> área pendiente por
    /// fuente y <see cref="IThreatenedAreaService.Mark"/> sobrescribe la anterior. Un boss que tiene
    /// que amenazar dos cosas el mismo turno (La Generala: la mano de dados <i>y</i> el cubilete a su
    /// alrededor) perdería una de las dos marcas. Este nodo resuelve el segundo aviso en su propio
    /// canal en vez de tocar el servicio compartido.
    /// </para>
    /// <para>
    /// <b>Cómo se cablea.</b> Dos instancias con el mismo <see cref="ChannelId"/>: una en
    /// <see cref="TelegraphStep.Execute"/> arriba del Sequence (al lado del ExecuteTelegraph
    /// principal, <b>fuera</b> de cualquier gate — el aviso hay que cobrarlo el turno siguiente aunque
    /// ese turno no se marque de nuevo) y una en <see cref="TelegraphStep.Mark"/> donde corresponda.
    /// </para>
    /// <para>
    /// <b>Shapes soportadas.</b> Las centradas (SquareAroundSelf / SquareAroundPlayer / Row / Column /
    /// HalfRoom). DirectionalBand y ScatteredSquares no: sus helpers son específicos del nodo
    /// principal y el canal secundario no las necesita.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_AuxTelegraph : AIActionNode
    {
        /// <summary>Mitad del ciclo que corre esta instancia.</summary>
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
        /// Delega en el <see cref="AINode_ExecuteTelegraph"/> de siempre con un
        /// <see cref="AIContext"/> armado a mano cuyo <c>SelfGuid</c> es el canal — el mismo truco que
        /// usa <c>HazardService</c> para correr telegraphs que no pertenecen a una entidad. Cero
        /// lógica de resolución duplicada.
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
        /// Guid estable y derivado: mismo boss + mismo <paramref name="channel"/> ⇒ mismo id en todos
        /// los turnos, sin necesidad de un servicio que reparta ids. Se pliega el hash del canal sobre
        /// los últimos 4 bytes del guid del boss, así dos bosses nunca comparten canal y el canal
        /// nunca coincide con el telegraph principal del propio boss.
        /// </summary>
        internal static Guid ChannelGuid(Guid self, string channel)
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
        /// Hash propio y no <see cref="string.GetHashCode()"/>: ese no está garantizado estable entre
        /// procesos, y un id de canal que cambia entre sesiones rompería un resume a mitad de aviso.
        /// El fallback distinto de cero evita que un canal sin nombre colisione con el guid del boss.
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
