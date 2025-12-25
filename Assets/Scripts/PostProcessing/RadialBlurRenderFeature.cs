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
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        public Settings settings = new Settings();
        private RadialBlurRenderPass renderPass;
        private Material material;

        public override void Create()
        {
            if (settings.shader == null)
            {
                Debug.LogWarning("[RadialBlurRenderFeature] Shader is not assigned");
                return;
            }

            material = CoreUtils.CreateEngineMaterial(settings.shader);
            renderPass = new RadialBlurRenderPass(material);
            renderPass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null || material == null)
                return;

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
        private object radialBlur; // Use object type to avoid compile-time dependency
        private RTHandle tempRTHandle;
        private static Type radialBlurType;

        private static readonly int BlurAngleDegreesID = Shader.PropertyToID("_BlurAngleDegrees");
        private static readonly int BlurIntensityID = Shader.PropertyToID("_BlurIntensity");
        private static readonly int BlurSamplesID = Shader.PropertyToID("_BlurSamples");

        public RadialBlurRenderPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("RadialBlur");

            // Cache RadialBlur type lookup
            if (radialBlurType == null)
            {
                radialBlurType = Type.GetType("FaeMaze.PostProcessing.RadialBlur, FaeMaze.PostProcessing")
                    ?? Type.GetType("FaeMaze.PostProcessing.RadialBlur, Assembly-CSharp");
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (radialBlurType == null)
                return;

            var stack = VolumeManager.instance.stack;

            // Use reflection to get RadialBlur component
            MethodInfo getComponentMethod = typeof(VolumeStack).GetMethod("GetComponent");
            if (getComponentMethod != null)
            {
                MethodInfo genericMethod = getComponentMethod.MakeGenericMethod(radialBlurType);
                radialBlur = genericMethod.Invoke(stack, null);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || radialBlur == null || !IsRadialBlurActive())
                return;

            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("RadialBlur");

            // Set shader properties using reflection
            material.SetFloat(BlurAngleDegreesID, GetFloatParameter("blurAngleDegrees"));
            material.SetFloat(BlurIntensityID, GetFloatParameter("blurIntensity"));
            material.SetFloat(BlurSamplesID, GetIntParameter("blurSamples"));

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

        private bool IsRadialBlurActive()
        {
            if (radialBlur == null)
                return false;

            MethodInfo isActiveMethod = radialBlur.GetType().GetMethod("IsActive");
            if (isActiveMethod != null)
            {
                return (bool)isActiveMethod.Invoke(radialBlur, null);
            }
            return false;
        }

        private float GetFloatParameter(string paramName)
        {
            FieldInfo field = radialBlur.GetType().GetField(paramName);
            if (field != null)
            {
                object parameter = field.GetValue(radialBlur);
                if (parameter != null)
                {
                    PropertyInfo valueProperty = parameter.GetType().GetProperty("value");
                    if (valueProperty != null)
                    {
                        return (float)valueProperty.GetValue(parameter);
                    }
                }
            }
            return 0f;
        }

        private float GetIntParameter(string paramName)
        {
            FieldInfo field = radialBlur.GetType().GetField(paramName);
            if (field != null)
            {
                object parameter = field.GetValue(radialBlur);
                if (parameter != null)
                {
                    PropertyInfo valueProperty = parameter.GetType().GetProperty("value");
                    if (valueProperty != null)
                    {
                        return (int)valueProperty.GetValue(parameter);
                    }
                }
            }
            return 0;
        }

        public void Dispose()
        {
            tempRTHandle?.Release();
        }
    }
}
