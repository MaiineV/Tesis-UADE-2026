using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Sube la paleta activa a la GPU como arrays globales de shader.
    /// Colocar en cualquier GameObject persistente de la escena (ej. GameManager).
    ///
    /// Se actualiza automáticamente cuando el PaletteAsset se modifica en el Editor
    /// gracias a PaletteAsset.OnPaletteChanged.
    /// </summary>
    public class GlobalPaletteManager : MonoBehaviour
    {
        [SerializeField] PaletteAsset _palette;

        static readonly int LightID  = Shader.PropertyToID("_PaletteLightColors");
        static readonly int MidID    = Shader.PropertyToID("_PaletteMidColors");
        static readonly int ShadowID = Shader.PropertyToID("_PaletteShadowColors");

        readonly Vector4[] _lightBuf  = new Vector4[PaletteAsset.MaxSlots];
        readonly Vector4[] _midBuf    = new Vector4[PaletteAsset.MaxSlots];
        readonly Vector4[] _shadowBuf = new Vector4[PaletteAsset.MaxSlots];

        public PaletteAsset Palette
        {
            get => _palette;
            set { _palette = value; Upload(); }
        }

        void OnEnable()
        {
            // Suscribirse al evento del asset para actualizarse cuando cambie
            PaletteAsset.OnPaletteChanged += OnAssetChanged;
            Upload();
        }

        void OnDisable()
        {
            PaletteAsset.OnPaletteChanged -= OnAssetChanged;
        }

        // Solo sube si el asset que cambió es el que este Manager está usando
        void OnAssetChanged(PaletteAsset changed)
        {
            if (changed == _palette) Upload();
        }

        public void Upload()
        {
            if (_palette == null) return;

            var slots = _palette.slots;
            int count = Mathf.Min(slots.Length, PaletteAsset.MaxSlots);

            for (int i = 0; i < count; i++)
            {
                _lightBuf[i]  = (Vector4)slots[i].ComputedLight;
                _midBuf[i]    = (Vector4)slots[i].ComputedMid;
                _shadowBuf[i] = (Vector4)slots[i].ComputedShadow;
            }

            Shader.SetGlobalVectorArray(LightID,  _lightBuf);
            Shader.SetGlobalVectorArray(MidID,    _midBuf);
            Shader.SetGlobalVectorArray(ShadowID, _shadowBuf);
        }
    }
}
