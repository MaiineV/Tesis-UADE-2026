using System;
using System.Collections.Generic;
using System.Text;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Localization;
using Rollgeon.Tiles.Visuals;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Visuals
{
    /// <summary>
    /// Contenido del tooltip de un objeto que un jefe pone en la sala — la bomba del Croupier y su
    /// mecha. Mismo reparto que casillas y enemigos: <see cref="BuildContent"/> es el header y
    /// <see cref="CollectCards"/> los dos bloques, el de la mecha y el de lo que deja al estallar.
    /// </summary>
    /// <remarks>
    /// Se rearma en cada hover en vez de guardar el string: el idioma puede cambiar en pleno
    /// combate, misma razón que da <c>EnemyTooltipInfo</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Rooms/Room Object Tooltip Info")]
    public sealed class RoomObjectTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();
        private readonly List<StatusIconState> _cards = new();

        private RoomObjectDefinitionSO _definition;
        private Guid _ownerGuid;
        private Guid _selfGuid;

        /// <summary>
        /// <paramref name="ownerGuid"/> es el jefe que lo puso: es a su árbol al que hay que
        /// preguntarle cuánto le queda a la mecha.
        /// </summary>
        public void Bind(RoomObjectDefinitionSO definition, Guid ownerGuid, Guid selfGuid)
        {
            _definition = definition;
            _ownerGuid = ownerGuid;
            _selfGuid = selfGuid;
        }

        /// <summary>Etiquetas y títulos de los dos bloques del panel.</summary>
        public const string FuseTickKey = "prop.panel.fuse_tick";
        public const string FuseBlowsKey = "prop.panel.fuse_blows";
        public const string OnBlastKey = "prop.panel.on_blast";
        public const string BlastHitKey = "prop.panel.blast_hit";

        /// <summary>
        /// El header del panel: identidad y una frase, sin pie. La vida NO se muestra — ninguna
        /// otra cosa del juego la pone en su panel y la barra sobre la cabeza ya la dice —, y la
        /// mecha se mudó al bloque de próximo turno, que es donde el jugador ya la busca.
        /// </summary>
        public TooltipContent BuildContent()
        {
            var def = _definition;
            if (def == null) return default;

            return new TooltipContent(
                text: LocalizedContent.Description(def.Id, string.Empty),
                name: LocalizedContent.Name(def.Id, def.EffectiveDisplayName),
                type: LocalizedContent.Ui("prop.panel.type", "Objeto"));
        }

        /// <summary>
        /// Los dos bloques, en el orden en que se leen. Arriba el próximo turno, igual que
        /// cualquier enemigo: mientras haya plazo dice que la mecha se acorta, y el turno
        /// anterior al estallido dice que explota. Abajo, siempre, lo que el estallido hace: es
        /// lo que decide si vale la pena romperla, y eso se pregunta desde el primer turno.
        /// </summary>
        public IReadOnlyList<StatusIconState> CollectCards()
        {
            _cards.Clear();
            if (!TryFindBlast(out var intent)) return _cards;

            AppendFuseCard(intent);
            AppendBlastCards(intent);
            return _cards;
        }

        // El badge cuenta como el de cualquier intención: TurnsAway 0 es "en su próximo turno",
        // así que ahí la mecha ya no se acorta — estalla, y el badge no tiene nada que contar.
        private void AppendFuseCard(in AIIntent intent)
        {
            bool blowsNext = intent.TurnsAway <= 0;

            _cards.Add(new StatusIconState(
                blowsNext ? FuseBlowsKey : FuseTickKey,
                blowsNext
                    ? LocalizedContent.Ui(FuseBlowsKey, "Explota")
                    : LocalizedContent.Ui(FuseTickKey, "Se acorta la mecha"),
                description: null,
                icon: null,
                active: true,
                // Sin badge cuando ya no queda nada que contar: un "0" al lado de "Explota" se
                // lee como que faltan cero turnos, que es justo lo que la palabra ya dice.
                remainingTurns: blowsNext ? (int?)null : intent.TurnsAway,
                style: StatusCardStyle.Terrain,
                eyebrow: EnemyStatusIconsView.NextTurnEyebrow()));
        }

        // Un bloque solo: el golpe y el fuego que queda son la MISMA consecuencia. Por eso el
        // fuego entra sin abrir bloque propio — dos etiquetas partirían en dos lo que pasa de una.
        private void AppendBlastCards(in AIIntent intent)
        {
            string eyebrow = LocalizedContent.Ui(OnBlastKey, "Al explotar");

            if (intent.Damage > 0)
            {
                _cards.Add(new StatusIconState(
                    BlastHitKey,
                    LocalizedContent.Ui(BlastHitKey, "Golpe del estallido"),
                    description: null,
                    icon: null,
                    active: true,
                    style: StatusCardStyle.Terrain,
                    damage: intent.Damage,
                    eyebrow: eyebrow));
                eyebrow = null;
            }

            if (intent.Leaves == null) return;
            // El precio por empezar el turno en el fuego es del panel del fuego, no de la bomba.
            SpecialTileCards.Append(intent.Leaves, _cards, eyebrow, opensBlock: eyebrow != null,
                                    includeTurnStart: false);
        }

        private bool TryFindBlast(out AIIntent blast)
        {
            blast = default;
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return false;
            if (!intents.TryRead(_ownerGuid, _standing, _next)) return false;

            foreach (var intent in _standing)
            {
                if (intent.SubjectGuid != _selfGuid) continue;
                blast = intent;
                return true;
            }
            return false;
        }

        public string BuildTooltip()
        {
            var def = _definition;
            if (def == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<b>").Append(LocalizedContent.Name(def.Id, def.EffectiveDisplayName)).Append("</b>");

            string description = LocalizedContent.Description(def.Id, string.Empty);
            if (!string.IsNullOrEmpty(description)) sb.AppendLine().Append(description);

            AppendBlast(sb);
            return sb.ToString();
        }

        private void AppendBlast(StringBuilder sb)
        {
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;
            if (!intents.TryRead(_ownerGuid, _standing, _next)) return;

            foreach (var intent in _standing)
            {
                if (intent.SubjectGuid != _selfGuid) continue;

                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("prop.tooltip.fuse", "Estalla en {0} turnos"),
                    Mathf.Max(0, intent.TurnsAway)));

                if (intent.Damage > 0)
                    sb.AppendLine()
                      .Append(LocalizedContent.Ui(BlastHitKey, "Golpe del estallido"))
                      .Append(": ").Append(intent.Damage);

                var fire = intent.Leaves;
                if (fire == null) return;

                if (fire.EnterDamage > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.enterdamage", "Daño al entrar: {0}"),
                        fire.EnterDamage));
                if (fire.TurnStartDamage > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.turndamage", "Daño por turno encima: {0}"),
                        fire.TurnStartDamage));
                if (intent.LeavesRounds > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.duration", "Dura {0} rondas"),
                        intent.LeavesRounds));
                return;
            }
        }
    }
}
