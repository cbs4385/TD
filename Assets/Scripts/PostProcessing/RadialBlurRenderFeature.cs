using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;
using System.Reflection;

namespace FaeMaze.PostProcessing
{
    public class RadialBlurRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Shader shader;
        }

        public Settings settings = new Settings();
        private RadialBlurRenderPass renderPass;
        private Material material;

        public override void Create()
        {
            Debug.Log($"[RadialBlurRenderFeature] Create() called, shader={settings.shader?.name ?? "null"}");

            if (settings.shader == null)
            {
                Debug.LogWarning("[RadialBlurRenderFeature] Shader is not assigned");
                return;
            }

            material = CoreUtils.CreateEngineMaterial(settings.shader);
            renderPass = new RadialBlurRenderPass(material);
            renderPass.renderPassEvent = settings.renderPassEvent;

            Debug.Log($"[RadialBlurRenderFeature] Created material and render pass successfully, event={settings.renderPassEvent}");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null || material == null)
            {
                return;
            }

            renderer.EnqueuePass(renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderPass?.Dispose();
                CoreUtils.Destroy(material);
            }
        }
    }

    public class RadialBlurRenderPass : ScriptableRenderPass
    {
        private Material material;
        private RadialBlur radialBlur;
        private RTHandle tempRTHandle;

        private static readonly int BlurAngleDegreesID = Shader.PropertyToID("_BlurAngleDegrees");
        private static readonly int BlurIntensityID = Shader.PropertyToID("_BlurIntensity");
        private static readonly int BlurSamplesID = Shader.PropertyToID("_BlurSamples");

        public RadialBlurRenderPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("RadialBlur");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var stack = VolumeManager.instance.stack;
            radialBlur = stack.GetComponent<RadialBlur>();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || radialBlur == null || !radialBlur.IsActive())
                return;

            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("RadialBlur");

            // Set shader properties directly
            material.SetFloat(BlurAngleDegreesID, radialBlur.blurAngleDegrees.value);
            material.SetFloat(BlurIntensityID, radialBlur.blurIntensity.value);
            material.SetFloat(BlurSamplesID, radialBlur.blurSamples.value);

            // Get source
            var source = cameraData.renderer.cameraColorTargetHandle;

            // Create temporary RT for destination using modern URP API
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRTHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempRadialBlur");

            // Blit with radial blur shader
            Blitter.BlitCameraTexture(cmd, source, tempRTHandle, material, 0);
            Blitter.BlitCameraTexture(cmd, tempRTHandle, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Unity 6 / URP 17 RenderGraph API
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.cameraType != CameraType.Game)
                return;

            // Get RadialBlur component from volume stack
            var stack = VolumeManager.instance.stack;
            var radialBlur = stack.GetComponent<RadialBlur>();

            if (radialBlur == null || !radialBlur.IsActive())
                return;

            // Set shader properties
            material.SetFloat(BlurAngleDegreesID, radialBlur.blurAngleDegrees.value);
            material.SetFloat(BlurIntensityID, radialBlur.blurIntensity.value);
            material.SetFloat(BlurSamplesID, radialBlur.blurSamples.value);

            // Get source texture
            TextureHandle source = resourceData.activeColorTexture;

            // Create a temporary destination texture
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_RadialBlurDest", false);

            // Pass 1: Apply radial blur from source to destination
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Radial Blur", out var passData, profilingSampler))
            {
                passData.material = material;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Apply radial blur from source to destination
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: Copy the blurred result back to the camera color
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Copy Radial Blur Result", out var passData, profilingSampler))
            {
                passData.source = destination;

                builder.UseTexture(destination, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Copy blurred result back to source (simple copy, no material)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), null, 0);
                });
            }
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempTexture;
        }

        public void Dispose()
        {
            tempRTHandle?.Release();
        }
    }
}
