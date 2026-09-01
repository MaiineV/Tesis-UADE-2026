using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Entities.Visuals
{
    [AddComponentMenu("Rollgeon/Entities/World Space Health Bar")]
    public sealed class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image con tipo Filled (Horizontal). fillAmount refleja HP ratio.")]
        private Image _fillImage;

        [SerializeField]
        [Tooltip("Texto numerico de HP. Null = sin texto.")]
        private TextMeshProUGUI _hpText;

        [SerializeField]
        [Tooltip("Formato del texto. {0} = current, {1} = max.")]
        private string _textFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Root de la barra. Se desactiva cuando la entidad muere.")]
        private GameObject _barRoot;

        [SerializeField]
        [Tooltip("Offset local respecto al pawn (Y = altura sobre la cabeza).")]
        private Vector3 _offset = new Vector3(0f, 2f, 0f);

        [SerializeField]
        [Tooltip("orthographicSize de referencia (= CameraConfigSO.DefaultZoom) al que la " +
                 "barra queda a escala 1x. Con zoom out la barra crece para que los numeros " +
                 "sigan legibles (BUG-050).")]
        private float _referenceZoom = 9f;

        private Guid _entityGuid;
        private int _maxHp;
        private bool _bound;
        private Vector3 _baseScale = Vector3.one;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void Initialize(Guid entityGuid, int currentHp, int maxHp)
        {
            if (_bound) Teardown();

            _entityGuid = entityGuid;
            _maxHp = maxHp > 0 ? maxHp : 1;

            // En Initialize (no en Awake): los ~7 call sites que spawnean esta barra
            // terminan de parentear Fill/hpText recien antes de llamar Initialize, asi
            // que buscar los hijos antes no los encontraria.
            ApplyOverlayMaterials();

            _onDamageResolved = HandleDamageResolved;
            _onHealResolved = HandleHealResolved;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHealResolved);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = true;

            SetBarVisible(true);
            RefreshFill(currentHp);
        }

        public void Teardown()
        {
            if (!_bound) return;

            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            if (_onHealResolved != null)
            {
                TypedEvent<HealResolvedPayload>.Unsubscribe(_onHealResolved);
                _onHealResolved = null;
            }
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Teardown();
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.forward = cam.transform.forward;

                // Solo camaras ortograficas: con perspectiva el tamano en pantalla ya
                // varia con la distancia via el proyeccion, escalar ademas doblaria el
                // efecto.
                if (cam.orthographic)
                {
                    float scale = ComputeZoomScale(cam.orthographicSize, _referenceZoom);
                    transform.localScale = _baseScale * scale;
                }
            }

            transform.localPosition = _offset;
        }

        /// <summary>
        /// Factor de escala world-space contra el zoom (orthographicSize) actual.
        /// Metodo estatico puro — extraido de <see cref="LateUpdate"/> para poder
        /// testear el clamp sin camara ni instancia del componente (BUG-050).
        /// </summary>
        public static float ComputeZoomScale(float orthographicSize, float referenceZoom,
            float minScale = 1f, float maxScale = 2.2f)
        {
            if (referenceZoom <= 0f) return minScale;
            return Mathf.Clamp(orthographicSize / referenceZoom, minScale, maxScale);
        }

        // El material overlay lo comparte toda la UI world-space del juego; ver
        // WorldSpaceOverlayMaterials para el por que del ZTest y del cacheo estatico.
        private void ApplyOverlayMaterials() => WorldSpaceOverlayMaterials.Apply(gameObject);

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _entityGuid) return;
            ReadAndRefresh();
        }

        private void HandleHealResolved(HealResolvedPayload payload)
        {
            if (payload.TargetGuid != _entityGuid) return;
            ReadAndRefresh();
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid guid)) return;
            if (guid != _entityGuid) return;

            SetBarVisible(false);
        }

        private void ReadAndRefresh()
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return;

            int current = attrs.GetAttributeValue<Health, int>(_entityGuid);
            RefreshFill(current);
        }

        private void RefreshFill(int current)
        {
            float ratio = (float)current / _maxHp;
            if (_fillImage != null)
                _fillImage.fillAmount = ratio;
            if (_hpText != null)
                _hpText.text = string.Format(_textFormat, current, _maxHp);
        }

        private void SetBarVisible(bool visible)
        {
            if (_barRoot != null)
                _barRoot.SetActive(visible);
        }
    }
}
