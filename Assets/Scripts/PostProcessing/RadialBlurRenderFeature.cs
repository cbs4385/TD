using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace FaeMaze.PostProcessing
{
    public class RadialBlurRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        public Settings settings = new Settings();
        private RadialBlurRenderPass renderPass;
        private Material material;

        public override void Create()
        {
            Debug.Log($"[RadialBlurRenderFeature] Create() called. Shader assigned: {settings.shader != null}");

            if (settings.shader == null)
            {
                Debug.LogError("[RadialBlurRenderFeature] Shader is NULL! RadialBlur will not work.");
                return;
            }

            Debug.Log($"[RadialBlurRenderFeature] Creating material from shader: {settings.shader.name}");
            material = CoreUtils.CreateEngineMaterial(settings.shader);

            if (material == null)
            {
                Debug.LogError("[RadialBlurRenderFeature] Failed to create material from shader!");
                return;
            }

            Debug.Log("[RadialBlurRenderFeature] Material created successfully");
            renderPass = new RadialBlurRenderPass(material);
            renderPass.renderPassEvent = settings.renderPassEvent;
            Debug.Log($"[RadialBlurRenderFeature] RenderPass created with event: {settings.renderPassEvent}");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null || material == null)
            {
                // Log occasionally to avoid spam
                if (Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning($"[RadialBlurRenderFeature] AddRenderPasses skipped - renderPass null: {renderPass == null}, material null: {material == null}");
                }
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

        private static bool _hasLoggedOnce = false;

        // Unity 6 RenderGraph API using recommended AddBlitPass approach
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
            {
                if (!_hasLoggedOnce && Time.frameCount > 60)
                {
                    Debug.LogError("[RadialBlurRenderPass] RecordRenderGraph: material is null!");
                    _hasLoggedOnce = true;
                }
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.cameraType != CameraType.Game)
                return;

            // Get RadialBlur component from volume stack
            var stack = VolumeManager.instance.stack;
            var radialBlur = stack.GetComponent<RadialBlur>();

            if (radialBlur == null)
            {
                if (!_hasLoggedOnce && Time.frameCount > 60)
                {
                    Debug.LogWarning("[RadialBlurRenderPass] RadialBlur component not found in volume stack");
                    _hasLoggedOnce = true;
                }
                return;
            }

            if (!radialBlur.IsActive())
            {
                if (!_hasLoggedOnce && Time.frameCount > 60)
                {
                    Debug.Log($"[RadialBlurRenderPass] RadialBlur found but not active. enabled.value={radialBlur.enabled.value}");
                    _hasLoggedOnce = true;
                }
                return;
            }

            // Log once that we're actually rendering
            if (!_hasLoggedOnce)
            {
                Debug.Log($"[RadialBlurRenderPass] RadialBlur IS ACTIVE - blurAngle={radialBlur.blurAngleDegrees.value}, intensity={radialBlur.blurIntensity.value}");
                _hasLoggedOnce = true;
            }

            // Set shader properties
            material.SetFloat(BlurAngleDegreesID, radialBlur.blurAngleDegrees.value);
            material.SetFloat(BlurIntensityID, radialBlur.blurIntensity.value);
            material.SetFloat(BlurSamplesID, radialBlur.blurSamples.value);
            material.SetFloat(VignetteCoverageID, radialBlur.vignetteCoverage.value);
            material.SetFloat(VignetteIntensityID, radialBlur.vignetteIntensity.value);

            // Get source texture
            TextureHandle source = resourceData.activeColorTexture;

            // Create a temporary destination texture using the recommended pattern
            var desc = renderGraph.GetTextureDesc(resourceData.cameraColor);
            desc.name = "_RadialBlurDest";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            // Use the recommended AddBlitPass API for proper texture handling in builds
            RenderGraphUtils.BlitMaterialParameters blitParams = new(source, destination, material, 0);
            renderGraph.AddBlitPass(blitParams, passName: "RadialBlur Blit");

            // Update frame data to point to the blitted result
            resourceData.cameraColor = destination;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
