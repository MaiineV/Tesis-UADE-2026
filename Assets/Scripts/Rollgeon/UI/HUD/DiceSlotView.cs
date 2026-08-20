using TMPro;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view for a single die slot. Used in build selection (Bind string) and
    /// in combat (ShowFace / SetHeld / OnToggled). UI#0013a / T97c.
    /// </summary>
    public class DiceSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _diceLabel;

        [Title("Combat — Hold toggle")]
        [SerializeField, Optional] private Button _button;

        [Tooltip("Cuerpo del dado: lleva la silueta del tipo y recibe los tints de hold/blocked.")]
        [SerializeField, Optional] private Image _background;

        [Title("Forma por tipo de dado")]
        [SerializeField, Optional]
        [Tooltip("Catálogo de siluetas. Si queda vacío se resuelve desde " +
                 "Resources/Dice/DiceShapeCatalog.")]
        private DiceShapeCatalogSO _shapeCatalog;

        [SerializeField, Optional]
        [Tooltip("Overlays que se recortan con la silueta del dado (glow, flash). Sin el sprite " +
                 "quedan como cuadrados llenos sobre una forma que no lo es.")]
        private Image[] _shapedOverlays;

        [Title("Encantamiento")]
        [SerializeField, Optional]
        [Tooltip("Material del dado encantado (EnchantHoloUI). Compartido: la variación por " +
                 "dado sale de la posición, no de instanciarlo.")]
        private Material _enchantMaterial;

        [SerializeField, Optional]
        [Tooltip("Material del dado maldito (EnchantCurseUI). Compartido, igual que el holo; " +
                 "si falta, cae al material holo para que el encantado nunca se pierda.")]
        private Material _cursedMaterial;

        [Title("Dice block")]
        [SerializeField, Optional]
        [Tooltip("Ícono de candado que se muestra cuando el dado está bloqueado. Opcional.")]
        private GameObject _lockIcon;

        [SerializeField, Optional]
        [Tooltip("Qué se llevó el dado, escrito sobre el candado (el Croupier pone el número que " +
                 "cantó). Vacío ⇒ candado pelado, que es lo correcto para los jefes que sortean al " +
                 "azar: ahí no hay nada que explicar. Opcional.")]
        private TextMeshProUGUI _lockLabel;

        /// <summary>Nombre del hijo del label del candado — lo busca el instalador del HUD.</summary>
        public const string LockLabelChildName = "LockLabel";

        [Title("Breakdown")]
        [SerializeField, Optional]
        [Tooltip("Label '+N' bajo el dado: lo que este dado suma al combo (cara + bonos de " +
                 "sus encantamientos). Solo se muestra en dados contribuyentes. Opcional.")]
        private Rollgeon.UI.HUD.Breakdown.DiceContributionLabel _contribution;

        [HideInInspector] public UnityEvent OnToggled = new UnityEvent();

        private static readonly Color BlockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private Color _defaultColor;
        private bool _blocked;
        private bool _held;
        private bool _hovered;
        private DiceType _type;
        private DiceShapeRole? _spinRole;

        private DiceShapeCatalogSO _resolvedCatalog;
        private bool _catalogResolved;
        private EnchantmentSO _enchantment;

        private void Awake()
        {
            if (_background != null) _defaultColor = _background.color;
            if (_button != null) _button.onClick.AddListener(() => OnToggled.Invoke());
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }

        /// <summary>Build selection — set die type label.</summary>
        public void Bind(string diceTypeName)
        {
            if (_diceLabel != null)
                _diceLabel.text = diceTypeName;
        }

        /// <summary>Build selection — etiqueta y silueta del tipo de dado.</summary>
        public void Bind(DiceType type)
        {
            Bind(type.ToString());
            SetDiceType(type);
        }

        /// <summary>
        /// Fija el tipo de dado del slot y repinta. Sin catálogo o sin entrada, el sprite queda
        /// en null y el <see cref="Image"/> dibuja el cuadrado de color plano de siempre — por
        /// eso esto es seguro antes de que exista el arte.
        /// </summary>
        /// <remarks>
        /// NO fuerza el frontal: <c>DiceZoneView.RefreshDiceShapes</c> repasa los CINCO slots en
        /// cada roll, incluidos los holdeados, así que forzarlo acá le borraría el visual de
        /// hold al dado apartado en cada reroll.
        /// </remarks>
        public void SetDiceType(DiceType type)
        {
            _type = type;
            RefreshShape();
        }

        /// <summary>Puntero encima del dado. Lo llama <see cref="DiceSlotHoverJuice"/>.</summary>
        /// <remarks>
        /// El estado de hover no se detecta acá aunque el sprite sea de este componente: el
        /// gate correcto (dado interactable ⇒ ni girando ni bloqueado) y el reset al
        /// desactivarse ya viven en <see cref="DiceSlotHoverJuice"/>. Dos máquinas de hover
        /// sobre el mismo GameObject divergirían.
        /// </remarks>
        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            RefreshShape();
        }

        /// <summary>
        /// Rol impuesto por el ciclado del giro, o <c>null</c> para volver al rol de reposo.
        /// Lo llama <c>DiceSlotAnimator</c>.
        /// </summary>
        public void SetSpinRole(DiceShapeRole? role)
        {
            if (_spinRole == role) return;
            _spinRole = role;
            RefreshShape();
        }

        /// <summary>
        /// El ÚNICO escritor de <c>sprite</c> del slot: resuelve el rol contra el estado
        /// completo y lo pinta. Todos los mutadores pasan por acá.
        /// </summary>
        /// <remarks>
        /// Los overlays llevan la misma silueta: el juice solo les anima el alfa, así que sin
        /// sprite el glow y el flash se dibujarían como cuadrados sobre un dado que no lo es.
        /// </remarks>
        private void RefreshShape()
        {
            var catalog = ResolveCatalog();
            var role = DiceShapeRoleResolver.Resolve(_blocked, _held, _hovered, _spinRole);
            // Sin catálogo el sprite queda en null a propósito: Image degrada al cuadrado de
            // color plano de siempre, que es lo que hace esto seguro antes de que exista el arte.
            var shape = catalog != null ? catalog.GetShape(_type, role) : null;

            // El setter de Image.sprite early-outea si no cambió: repintar de más sale gratis.
            if (_background != null) _background.sprite = shape;

            if (_shapedOverlays == null) return;
            for (int i = 0; i < _shapedOverlays.Length; i++)
                if (_shapedOverlays[i] != null) _shapedOverlays[i].sprite = shape;
        }

        private DiceShapeCatalogSO ResolveCatalog()
        {
            if (_catalogResolved) return _resolvedCatalog;
            _resolvedCatalog = DiceShapeCatalogSO.Resolve(_shapeCatalog);
            _catalogResolved = true;
            return _resolvedCatalog;
        }

        /// <summary>
        /// Muestra el visual del dado encantado: holo para bendiciones, maldito
        /// (<see cref="CapCursed"/>) para curses. <c>null</c> = sin encantamiento (vuelve al
        /// material default de uGUI).
        /// </summary>
        /// <remarks>
        /// Toma el <see cref="EnchantmentSO"/> y no un bool: la elección holo/maldito sale
        /// del SO, sin tocar call-sites. El material es compartido — la variación por dado
        /// sale de la posición canvas-space dentro del shader, así que no hay que instanciar
        /// nada (uGUI ni siquiera soporta MaterialPropertyBlock).
        /// </remarks>
        public void SetEnchantVisual(EnchantmentSO enchantment)
        {
            // Idempotente: sin esto, escribir el material cada frame dispara SetMaterialDirty.
            // Válido con la elección holo/maldito: es función pura de la identidad del SO.
            if (_enchantment == enchantment) return;
            _enchantment = enchantment;

            if (_background == null) return;
            _background.material = ResolveMaterial(enchantment);
        }

        private Material ResolveMaterial(EnchantmentSO enchantment)
        {
            if (enchantment == null) return null;
            if (enchantment.IsCursed() && _cursedMaterial != null) return _cursedMaterial;
            return _enchantMaterial;
        }

        /// <summary>Última cara mostrada (0 = sin tirada). Lo lee el presenter de
        /// throw para etiquetar el ghost al agarrar este dado en un grab-reroll.</summary>
        public int CurrentFace { get; private set; }

        /// <summary>Combat — show rolled face value.</summary>
        public void ShowFace(int face)
        {
            CurrentFace = face;
            _diceLabel?.SetText(face.ToString());
        }

        /// <summary>
        /// Cara transitoria durante el spin del roll. Solo pinta el label — no toca
        /// <see cref="CurrentFace"/>, que debe seguir reflejando la última cara real
        /// (la lee el presenter de throw para el ghost del grab-reroll).
        /// </summary>
        public void SetSpinPreviewFace(int face)
        {
            _diceLabel?.SetText(face.ToString());
        }

        /// <summary>
        /// Vacía el label mientras el dado gira. Mismo contrato que
        /// <see cref="SetSpinPreviewFace"/>: no toca <see cref="CurrentFace"/>, y el reveal
        /// repinta el número vía <see cref="ShowFace"/>.
        /// </summary>
        public void ClearSpinPreview()
        {
            _diceLabel?.SetText(string.Empty);
        }

        /// <summary>
        /// Habilita/deshabilita el toggle de hold sin pisar el estado de bloqueo
        /// (un dado bloqueado por boss queda no-interactable aunque pidan true).
        /// Lo usa la animación de spin: un dado que todavía rueda no se puede holdear.
        /// </summary>
        public void SetHoldInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable && !_blocked;
        }

        /// <summary>Combat — toggle held visual. Sin efecto si el dado está bloqueado.</summary>
        /// <remarks>
        /// El visual de holdeado es el sprite <see cref="DiceShapeRole.Selected"/> del set: el
        /// tint azul que había acá antes lo pisaba y, encima, el ColorTint del Button ya lo
        /// multiplicaba por su cuenta. El glow pulsante de <see cref="DiceSlotJuice"/> sigue
        /// acompañando.
        /// </remarks>
        public void SetHeld(bool held)
        {
            _held = held;
            RefreshShape();
        }

        /// <summary>
        /// Boss 1 (§2) — marca el dado como bloqueado: grayed-out + ícono de candado, y desactiva
        /// el botón de hold. Al desbloquear, restaura el botón y el visual según el estado real.
        /// </summary>
        /// <param name="label">
        /// Qué se lo llevó, escrito sobre el candado. Con el Croupier es el número que cantó la
        /// ruleta, y es lo que ata el sector que va a caer con el dado que falta. <c>null</c> o vacío
        /// ⇒ candado pelado.
        /// </param>
        /// <remarks>
        /// La etiqueta <b>no</b> es la posición del dado: el índice sale de <c>número % dados</c>, así
        /// que con seis sectores y cinco dados el 6 confisca el primero. Dice <i>quién</i> se lo
        /// llevó; <i>cuál</i> ya lo marca el candado.
        /// </remarks>
        public void SetBlocked(bool blocked, string label = null)
        {
            _blocked = blocked;
            if (_lockIcon != null) _lockIcon.SetActive(blocked);
            if (_button != null) _button.interactable = !blocked;
            if (_background != null)
                _background.color = blocked ? BlockedColor : _defaultColor;

            if (_lockLabel != null)
            {
                bool hasLabel = blocked && !string.IsNullOrEmpty(label);
                _lockLabel.text = hasLabel ? label : string.Empty;
                _lockLabel.gameObject.SetActive(hasLabel);
            }

            RefreshShape();
        }

        /// <summary>
        /// Combat — apaga el slot: limpia el label y quita el tint de hold.
        /// Lo invoca <see cref="DiceZoneView"/> al iniciar y al cerrar el turno.
        /// </summary>
        /// <summary>Ancla del label "+N" (origen del vuelo en la secuencia). Puede ser null.</summary>
        public Rollgeon.UI.HUD.Breakdown.DiceContributionLabel ContributionLabel => _contribution;

        /// <summary>Graphic de fondo del slot — el juice del breakdown lo flashea/dimea.</summary>
        public UnityEngine.UI.Graphic BackgroundGraphic => _background;

        /// <summary>
        /// Combat — pinta u oculta el "+N" bajo el dado. Null ⇒ no contribuye.
        /// <paramref name="bonusPortion"/> = cuánto del total vino de encantamientos
        /// (se pinta en dorado); <paramref name="appearDelay"/> = stagger de aparición.
        /// </summary>
        public void SetContribution(int? amount, int bonusPortion = 0, float appearDelay = 0f)
        {
            if (_contribution == null) return;
            if (amount.HasValue) _contribution.Show(amount.Value, bonusPortion, appearDelay);
            else _contribution.Hide();
        }

        public void Clear()
        {
            CurrentFace = 0;
            _diceLabel?.SetText(string.Empty);
            _contribution?.Hide();
            // Un spin cancelado a mitad no debe dejar latcheado su lateral. El hover NO se
            // resetea acá: es de DiceSlotHoverJuice, que lo baja al desactivarse el slot —
            // si el slot sigue vivo y el puntero encima, el hover sigue siendo cierto.
            _spinRole = null;
            SetBlocked(false);
            SetHeld(false);
        }
    }
}
