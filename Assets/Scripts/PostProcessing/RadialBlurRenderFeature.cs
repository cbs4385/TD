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
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Shader shader;
        }

        public Settings settings = new Settings();
        private RadialBlurRenderPass renderPass;
        private Material material;

        public override void Create()
        {
            if (settings.shader == null)
            {
                return;
            }

            material = CoreUtils.CreateEngineMaterial(settings.shader);
            renderPass = new RadialBlurRenderPass(material);
            renderPass.renderPassEvent = settings.renderPassEvent;
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

        // Unity 6 RenderGraph API using recommended AddBlitPass approach
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
