using System.Text;
using Patterns;
using Rollgeon.Combat.Cashier;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Línea bajo la barra del Cajero: en qué escalón está, cuánto pega, y de dónde sale ese número.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es lo que hace visible la trampa de las fichas.</b> El daño del Cajero lo decide el oro que
    /// llevás encima, así que levantar monedas le sube el golpe — y hasta ahora eso no se decía en
    /// ningún lado. El jugador veía un jefe que a veces pegaba 14 y a veces 35 sin saber por qué, y
    /// lo único que soltaba el jefe era oro.
    /// </para>
    /// <para>
    /// <b>El número no se recalcula acá.</b> Sale de <see cref="ICashierLedgerService.LastTier"/>,
    /// que publica el propio nodo que marca la columna. La tabla vive en el asset del jefe y el
    /// escalón efectivo depende además del rastrillo y del soborno: recalcularlo en la vista es
    /// exactamente donde la UI y el golpe se separan.
    /// </para>
    /// <para>
    /// <b>Se apaga sola cuando no hay Cajero.</b> El ledger es lazy y sólo existe mientras él está en
    /// la sala, así que la ausencia del servicio es la señal de "esta pelea no es suya".
    /// </para>
    /// <para>
    /// [SETUP] <see cref="_label"/> tiene que ser un HIJO y no este mismo GameObject: se apaga y se
    /// prende, y apagarse a sí mismo dejaría la vista sin poder volver.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Cashier Tier Readout View")]
    public sealed class CashierTierReadoutView : MonoBehaviour
    {
        /// <summary>Nombre del hijo del label — lo busca el instalador del HUD.</summary>
        public const string LabelChildName = "TierText";

        [Title("Refs")]
        [SerializeField, Required]
        [Tooltip("Label de la línea. Tiene que ser un HIJO: se prende y se apaga.")]
        private TextMeshProUGUI _label;

        private void Awake()
        {
            if (_label == null)
            {
                var child = transform.Find(LabelChildName);
                if (child != null) _label = child.GetComponent<TextMeshProUGUI>();
            }

            Refresh();
        }

        private void OnEnable()
        {
            EventManager.Subscribe(EventName.OnCashierTierChanged, HandleChanged);
            EventManager.Subscribe(EventName.OnGoldChanged, HandleChanged);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleChanged);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleEnded);
            EventManager.Subscribe(EventName.OnRunEnd, HandleEnded);
            Refresh();
        }

        private void OnDisable()
        {
            EventManager.UnSubscribe(EventName.OnCashierTierChanged, HandleChanged);
            EventManager.UnSubscribe(EventName.OnGoldChanged, HandleChanged);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleChanged);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleEnded);
            EventManager.UnSubscribe(EventName.OnRunEnd, HandleEnded);
        }

        private void HandleChanged(params object[] args) => Refresh();

        private void HandleEnded(params object[] args) => Hide();

        /// <summary>Repinta la línea. Pública para tooling y tests.</summary>
        public void Refresh()
        {
            if (_label == null) return;

            if (!ServiceLocator.TryGetService<ICashierLedgerService>(out var ledger)
                || ledger?.LastTier == null)
            {
                Hide();
                return;
            }

            _label.text = Format(ledger.LastTier.Value, ledger.BribeRoundsLeft);
            _label.gameObject.SetActive(true);
        }

        /// <summary>Baja la línea sin consultar servicios.</summary>
        public void Hide()
        {
            if (_label != null) _label.gameObject.SetActive(false);
        }

        /// <summary>
        /// La línea, ya escrita. Pura y estática para poder testear el texto sin canvas ni TMP —
        /// es lo único que esta vista decide.
        /// </summary>
        /// <remarks>
        /// El escalón se muestra 1-based porque <c>Rank</c> es un índice y "escalón 0" no se lee como
        /// el más barato, se lee como "ninguno". Las tres causas van entre paréntesis y sólo aparecen
        /// las que están activas: con rastrillo 0 y sin soborno, la línea es sólo el oro — que es el
        /// 90% de la pelea y no necesita tres cifras para decir una cosa.
        /// <para>
        /// <b>Sólo caracteres del atlas de <c>m6x11plus</c>.</b> La pixel font del HUD no tiene
        /// <c>·</c> (U+00B7) ni <c>⟳</c> (U+27F3), y un glifo que falta sale como cuadradito: los
        /// separadores son dos puntos y comas, y las rondas se escriben con la palabra.
        /// </para>
        /// </remarks>
        public static string Format(CashierTierSnapshot tier, int bribeRoundsLeft)
        {
            var sb = new StringBuilder();
            sb.Append("Escalón ").Append(tier.Rank + 1).Append(": pega ").Append(tier.Damage);
            sb.Append("   (oro ").Append(tier.Gold);

            if (tier.StepUp > 0) sb.Append(", rastrillo +").Append(tier.StepUp);
            if (tier.StepDown > 0)
            {
                sb.Append(", soborno -").Append(tier.StepDown);
                if (bribeRoundsLeft > 0)
                    sb.Append(" por ").Append(bribeRoundsLeft)
                      .Append(bribeRoundsLeft == 1 ? " ronda" : " rondas");
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}
