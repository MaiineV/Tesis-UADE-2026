using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class GodotParityFeature : ScriptableRendererFeature
{
    [Header("Pixelation")]
    [Range(1, 16)] public int pixelSize = 4;
    public bool autoTexelOffsetFromCamera = true;
    public Vector2 texelOffset = Vector2.zero;

    [Header("Colors")]
    public Color lineTint = Color.black;
    public Color creaseTint = new Color(0.833f, 0.833f, 0.833f, 1f);
    public bool flipPalettes = false;

    [Header("Line")]
    public bool lineOverlay = true;
    [Range(0f, 1f)] public float lineAlpha = 0.5f;

    [Header("Crease")]
    public bool creaseOverlay = true;
    [Range(0f, 1f)] public float creaseAlpha = 1f;

    [Header("Sampling")]
    [Range(0.5f, 4f)] public float kernelRadius = 1f;

    [Header("Line Detect")]
    [Range(0f, 1f)] public float zdeltaCutoff = 0.25f;
    [Range(0f, 1f)] public float angleZCutoff = 0.5f;
    public float angleZScale = 2f;

    [Header("Crease Detect")]
    public float convexCutoff = 0.10f;
    [Range(0f, 0.5f)] public float creaseFeather = 0f;
    public float concaveCutoff = 0.01f;
    public float concaveZCutoff = 0.5f;

    private GodotParityPass _pass;
    private Material _material;

    static readonly int s_PixelSizeId = Shader.PropertyToID("_PixelSize");
    static readonly int s_TexelOffsetId = Shader.PropertyToID("_TexelOffset");
    static readonly int s_PixelationScreenSizeId = Shader.PropertyToID("_PixelationScreenSize");
    static readonly int s_LineTintId = Shader.PropertyToID("_LineTint");
    static readonly int s_CreaseTintId = Shader.PropertyToID("_CreaseTint");
    static readonly int s_FlipPalettesId = Shader.PropertyToID("_FlipPalettes");
    static readonly int s_LineOverlayId = Shader.PropertyToID("_LineOverlay");
    static readonly int s_LineAlphaId = Shader.PropertyToID("_LineAlpha");
    static readonly int s_CreaseOverlayId = Shader.PropertyToID("_CreaseOverlay");
    static readonly int s_CreaseAlphaId = Shader.PropertyToID("_CreaseAlpha");
    static readonly int s_KernelRadiusId = Shader.PropertyToID("_KernelRadius");
    static readonly int s_ZDeltaCutoffId = Shader.PropertyToID("_ZDeltaCutoff");
    static readonly int s_AngleZCutoffId = Shader.PropertyToID("_AngleZCutoff");
    static readonly int s_AngleZScaleId = Shader.PropertyToID("_AngleZScale");
    static readonly int s_ConvexCutoffId = Shader.PropertyToID("_ConvexCutoff");
    static readonly int s_CreaseFeatherId = Shader.PropertyToID("_CreaseFeather");
    static readonly int s_ConcaveCutoffId = Shader.PropertyToID("_ConcaveCutoff");
    static readonly int s_ConcaveZCutoffId = Shader.PropertyToID("_ConcaveZCutoff");

    public override void Create()
    {
        var shader = Shader.Find("Hidden/Custom/GodotParity/Post");
        if (shader == null)
            return;

        _material = CoreUtils.CreateEngineMaterial(shader);
        _pass = new GodotParityPass(_material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        if (_material == null || _pass == null)
            return;

        var cam = renderingData.cameraData.camera;
        var runtimeTexelOffset = autoTexelOffsetFromCamera ? ComputeTexelOffset(cam) : texelOffset;

        _material.SetFloat(s_PixelSizeId, Mathf.Max(1, pixelSize));
        _material.SetVector(s_TexelOffsetId, new Vector4(runtimeTexelOffset.x, runtimeTexelOffset.y, 0f, 0f));
        _material.SetVector(s_PixelationScreenSizeId, new Vector4(
            cam.pixelWidth,
            cam.pixelHeight,
            1f / cam.pixelWidth,
            1f / cam.pixelHeight));

        _material.SetColor(s_LineTintId, lineTint);
        _material.SetColor(s_CreaseTintId, creaseTint);
        _material.SetFloat(s_FlipPalettesId, flipPalettes ? 1f : 0f);

        _material.SetFloat(s_LineOverlayId, lineOverlay ? 1f : 0f);
        _material.SetFloat(s_LineAlphaId, lineAlpha);
        _material.SetFloat(s_CreaseOverlayId, creaseOverlay ? 1f : 0f);
        _material.SetFloat(s_CreaseAlphaId, creaseAlpha);

        _material.SetFloat(s_KernelRadiusId, kernelRadius);
        _material.SetFloat(s_ZDeltaCutoffId, zdeltaCutoff);
        _material.SetFloat(s_AngleZCutoffId, angleZCutoff);
        _material.SetFloat(s_AngleZScaleId, angleZScale);
        _material.SetFloat(s_ConvexCutoffId, convexCutoff);
        _material.SetFloat(s_CreaseFeatherId, creaseFeather);
        _material.SetFloat(s_ConcaveCutoffId, concaveCutoff);
        _material.SetFloat(s_ConcaveZCutoffId, concaveZCutoff);

        _pass.requiresIntermediateTexture = true;
        renderer.EnqueuePass(_pass);
    }

    Vector2 ComputeTexelOffset(Camera cam)
    {
        if (cam == null || !cam.orthographic || cam.pixelHeight <= 0)
            return texelOffset;

        float pixelWorldSize = (cam.orthographicSize * 2f) / cam.pixelHeight;
        if (pixelWorldSize <= 1e-5f)
            return texelOffset;

        Vector3 pos = cam.transform.position;
        var snapped = new Vector2(
            Mathf.Round(pos.x / pixelWorldSize) * pixelWorldSize,
            Mathf.Round(pos.y / pixelWorldSize) * pixelWorldSize
        );
        var drift = snapped - new Vector2(pos.x, pos.y);
        return new Vector2(drift.x / pixelWorldSize, -drift.y / pixelWorldSize);
    }

#if URP_COMPATIBILITY_MODE
    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        CoreUtils.Destroy(_material);
    }
#endif

    class GodotParityPass : ScriptableRenderPass
    {
        readonly Material _material;
        static readonly MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();
        static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

#if URP_COMPATIBILITY_MODE
        RTHandle _copiedColor;
#endif

        public GodotParityPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("GodotParityPost");
        }

#if URP_COMPATIBILITY_MODE
        [Obsolete("CompatibilityMode")]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
#pragma warning disable CS0618
            ResetTarget();
#pragma warning restore CS0618

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthStencilFormat = GraphicsFormat.None;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _copiedColor, desc, name: "_GodotParityColorCopy");
        }

        [Obsolete("CompatibilityMode")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            var cmd = renderingData.commandBuffer;

            using (new ProfilingScope(cmd, profilingSampler))
            {
                var rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);
                CoreUtils.SetRenderTarget(cmd, _copiedColor);
                Blitter.BlitTexture(rasterCmd, cameraData.renderer.cameraColorTargetHandle, new Vector4(1, 1, 0, 0), 0f, false);
                CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);

                s_PropertyBlock.Clear();
                s_PropertyBlock.SetTexture(s_BlitTextureId, _copiedColor);
                s_PropertyBlock.SetVector(s_BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
            }
        }

        public void Dispose()
        {
            _copiedColor?.Release();
        }
#endif

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
            if (!resourcesData.cameraColor.IsValid())
                return;

            var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
            targetDesc.name = "_GodotParityColorCopy";
            targetDesc.clearBuffer = false;

            TextureHandle source = resourcesData.activeColorTexture;
            TextureHandle copiedColor = renderGraph.CreateTexture(targetDesc);
            renderGraph.AddBlitPass(source, copiedColor, Vector2.one, Vector2.zero, passName: "GodotParity Copy Color");

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GodotParityPost", out var passData, profilingSampler))
            {
                passData.material = _material;
                passData.inputTexture = copiedColor;

                builder.UseTexture(copiedColor, AccessFlags.Read);
                if (resourcesData.cameraDepthTexture.IsValid())
                    builder.UseTexture(resourcesData.cameraDepthTexture);
                if (resourcesData.cameraNormalsTexture.IsValid())
                    builder.UseTexture(resourcesData.cameraNormalsTexture);

                builder.SetRenderAttachment(resourcesData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    s_PropertyBlock.Clear();
                    s_PropertyBlock.SetTexture(s_BlitTextureId, data.inputTexture);
                    s_PropertyBlock.SetVector(s_BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                    rgContext.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
                });
            }
        }

        class PassData
        {
            internal Material material;
            internal TextureHandle inputTexture;
        }
    }
}
