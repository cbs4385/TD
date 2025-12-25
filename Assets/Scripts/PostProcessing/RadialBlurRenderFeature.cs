using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            Debug.Log($"[RadialBlurRenderFeature] AddRenderPasses called, renderPass={renderPass != null}, material={material != null}");

            if (renderPass == null || material == null)
            {
                Debug.LogWarning($"[RadialBlurRenderFeature] Cannot add render pass: renderPass={renderPass != null}, material={material != null}");
                return;
            }

            renderer.EnqueuePass(renderPass);
            Debug.Log("[RadialBlurRenderFeature] Render pass enqueued successfully");
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
            if (material == null)
            {
                Debug.LogWarning("[RadialBlurRenderPass] Material is null");
                return;
            }

            if (radialBlur == null)
            {
                Debug.LogWarning("[RadialBlurRenderPass] RadialBlur component is null");
                return;
            }

            if (!radialBlur.IsActive())
            {
                Debug.LogWarning("[RadialBlurRenderPass] RadialBlur is not active");
                return;
            }

            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("RadialBlur");

            // Set shader properties directly
            material.SetFloat(BlurAngleDegreesID, radialBlur.blurAngleDegrees.value);
            material.SetFloat(BlurIntensityID, radialBlur.blurIntensity.value);
            material.SetFloat(BlurSamplesID, radialBlur.blurSamples.value);

            Debug.Log($"[RadialBlurRenderPass] Executing with angle={radialBlur.blurAngleDegrees.value}, intensity={radialBlur.blurIntensity.value}, samples={radialBlur.blurSamples.value}");

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

        public void Dispose()
        {
            tempRTHandle?.Release();
        }
    }
}
