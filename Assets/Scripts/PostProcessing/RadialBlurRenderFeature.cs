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

        private static readonly int BlurAngleDegreesID = Shader.PropertyToID("_BlurAngleDegrees");
        private static readonly int BlurIntensityID = Shader.PropertyToID("_BlurIntensity");
        private static readonly int BlurSamplesID = Shader.PropertyToID("_BlurSamples");
        private static readonly int VignetteCoverageID = Shader.PropertyToID("_VignetteCoverage");
        private static readonly int VignetteIntensityID = Shader.PropertyToID("_VignetteIntensity");

        public RadialBlurRenderPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("RadialBlur");
        }

        // Unity 6 RenderGraph API
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
            float angleValue = radialBlur.blurAngleDegrees.value;
            float intensityValue = radialBlur.blurIntensity.value;
            float samplesValue = radialBlur.blurSamples.value;
            float vignetteCoverageValue = radialBlur.vignetteCoverage.value;
            float vignetteIntensityValue = radialBlur.vignetteIntensity.value;

            material.SetFloat(BlurAngleDegreesID, angleValue);
            material.SetFloat(BlurIntensityID, intensityValue);
            material.SetFloat(BlurSamplesID, samplesValue);
            material.SetFloat(VignetteCoverageID, vignetteCoverageValue);
            material.SetFloat(VignetteIntensityID, vignetteIntensityValue);

            // Verify what was actually set
            float verifyAngle = material.GetFloat(BlurAngleDegreesID);
            float verifyIntensity = material.GetFloat(BlurIntensityID);

            Debug.Log($"[RadialBlurRenderPass] RecordRenderGraph: Set angle={angleValue}, intensity={intensityValue}, samples={samplesValue}, vignette={vignetteCoverageValue}/{vignetteIntensityValue}");

            // Get source texture
            TextureHandle source = resourceData.activeColorTexture;

            // Create a temporary destination texture
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_RadialBlurDest", false);

            Debug.Log($"[RadialBlurRenderPass] RecordRenderGraph: Created textures, source valid={source.IsValid()}, dest valid={destination.IsValid()}");

            // Apply radial blur from source to destination
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Radial Blur", out var passData, profilingSampler))
            {
                passData.material = material;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Debug.Log($"[RadialBlurRenderPass] RenderFunc executing: material={data.material != null}, source valid={data.source.IsValid()}");

                    // CRITICAL FIX: Explicitly set the source texture to _MainTex
                    data.material.SetTexture("_MainTex", data.source);

                    // Apply radial blur from source to destination
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    Debug.Log("[RadialBlurRenderPass] RenderFunc: Blit completed");
                });
            }

            Debug.Log("[RadialBlurRenderPass] RecordRenderGraph: Updating cameraColor to destination");
            // Update the camera color to use the blurred result
            resourceData.cameraColor = destination;
            Debug.Log("[RadialBlurRenderPass] RecordRenderGraph: Complete");
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
