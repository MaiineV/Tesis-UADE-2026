using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class PixelationFeature : ScriptableRendererFeature
{
    [Header("Pixelation Settings")]
    [Range(1, 16)] public int pixelSize = 6;
    [Range(0f, 2f)] public float normalEdgeStrength = 0.3f;
    [Range(0f, 1f)] public float depthEdgeStrength = 0.4f;

    private PixelationPass m_Pass;
    private Material m_Material;

    // Static shader property IDs for performance
    static readonly int s_PixelSizeId = Shader.PropertyToID("_PixelSize");
    static readonly int s_NormalEdgeStrengthId = Shader.PropertyToID("_NormalEdgeStrength");
    static readonly int s_DepthEdgeStrengthId = Shader.PropertyToID("_DepthEdgeStrength");
    static readonly int s_ScreenSizeId = Shader.PropertyToID("_ScreenSize");

    public override void Create()
    {
        var shader = Shader.Find("Custom/Pixelation");
        if (shader == null) return;

        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new PixelationPass(m_Material);
        m_Pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        if (m_Material == null || m_Pass == null) return;

        var cam = renderingData.cameraData.camera;
        m_Material.SetFloat(s_PixelSizeId, pixelSize);
        m_Material.SetFloat(s_NormalEdgeStrengthId, normalEdgeStrength);
        m_Material.SetFloat(s_DepthEdgeStrengthId, depthEdgeStrength);
        m_Material.SetVector(s_ScreenSizeId, new Vector4(
            cam.pixelWidth, cam.pixelHeight,
            1f / cam.pixelWidth, 1f / cam.pixelHeight));

        m_Pass.requiresIntermediateTexture = true;
        renderer.EnqueuePass(m_Pass);
    }

#if URP_COMPATIBILITY_MODE
    protected override void Dispose(bool disposing)
    {
        m_Pass?.Dispose();
        CoreUtils.Destroy(m_Material);
    }
#endif

    // Inner render pass class
    class PixelationPass : ScriptableRenderPass
    {
        Material m_Material;
        static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();
        static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

#if URP_COMPATIBILITY_MODE
        RTHandle m_CopiedColor;
#endif

        public PixelationPass(Material material)
        {
            m_Material = material;
            profilingSampler = new ProfilingSampler("Pixelation");
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
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_CopiedColor, desc, name: "_PixelationColorCopy");
        }

        [Obsolete("CompatibilityMode")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            var cmd = renderingData.commandBuffer;

            using (new ProfilingScope(cmd, profilingSampler))
            {
                var rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);

                // Copy camera color to temp
                CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
                Blitter.BlitTexture(rasterCmd, cameraData.renderer.cameraColorTargetHandle, new Vector4(1, 1, 0, 0), 0f, false);

                // Set render target back to camera color
                CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);

                // Draw fullscreen with pixelation material
                s_PropertyBlock.Clear();
                s_PropertyBlock.SetTexture(s_BlitTextureId, m_CopiedColor);
                s_PropertyBlock.SetVector(s_BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
            }
        }

        public void Dispose()
        {
            m_CopiedColor?.Release();
        }
#endif

        // Render Graph path
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!resourcesData.cameraColor.IsValid()) return;

            // Copy active color to temp texture
            var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
            targetDesc.name = "_PixelationColorCopy";
            targetDesc.clearBuffer = false;

            TextureHandle source = resourcesData.activeColorTexture;
            TextureHandle copiedColor = renderGraph.CreateTexture(targetDesc);
            renderGraph.AddBlitPass(source, copiedColor, Vector2.one, Vector2.zero, passName: "Pixelation Copy Color");

            // Main pixelation pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelation", out var passData, profilingSampler))
            {
                passData.material = m_Material;
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
