Shader "Hidden/PostProcess/RadialBlur"
{
    Properties
    {
        _BlurAngleDegrees ("Clear Radius Percentage", Float) = 85.0
        _BlurIntensity ("Blur Intensity", Float) = 0.5
        _BlurSamples ("Blur Samples", Float) = 8.0
        _VignetteCoverage ("Vignette Coverage Percentage", Float) = 0.0
        _VignetteIntensity ("Vignette Darkness Intensity", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "RadialBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Unity 6 Render Graph uses _BlitTexture (defined in Blit.hlsl)
            // sampler_LinearClamp is already defined in Core.hlsl

            float _BlurAngleDegrees;     // Clear radius as percentage (10 = 10% of screen radius is clear)
            float _BlurIntensity;        // Intensity of the blur effect
            float _BlurSamples;          // Number of blur samples
            float _VignetteCoverage;     // Vignette coverage as percentage (0-100)
            float _VignetteIntensity;    // Vignette darkness intensity (0-1)

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center
                float2 center = float2(0.5, 0.5);
                float distanceFromCenter = length(uv - center);

                // Normalize distance (0 = center, 1 = corner)
                float normalizedDist = distanceFromCenter / 0.707;

                // Calculate where blur should start (blurAngleDegrees% toward edge)
                float blurStart = _BlurAngleDegrees / 100.0;

                // If we're inside the clear zone, return unblurred
                if (normalizedDist < blurStart)
                {
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                }

                // Calculate blur strength from 0 (at blur start) to intensity (at edge)
                float blurFactor = (normalizedDist - blurStart) / max(1.0 - blurStart, 0.001);
                blurFactor = saturate(blurFactor) * _BlurIntensity;

                // Apply radial blur by sampling multiple points in a circle
                float4 blurAccum = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;

                // Blur radius increases with distance from center and intensity
                float blurRadius = blurFactor * 0.03;

                [loop]
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)i / (float)samples * 6.28318530718; // 2*PI
                    float2 offset = float2(cos(angle), sin(angle)) * blurRadius;
                    float2 sampleUV = uv + offset;

                    blurAccum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                }

                float4 finalColor = blurAccum / (float)samples;

                // Apply vignette effect
                // Calculate where vignette should start (inverse of coverage percentage)
                float vignetteStart = 1.0 - (_VignetteCoverage / 100.0);

                // Calculate vignette darkening factor
                float vignetteFactor = 0.0;
                if (normalizedDist > vignetteStart)
                {
                    // Gradually darken from vignette start to edge
                    vignetteFactor = (normalizedDist - vignetteStart) / max(1.0 - vignetteStart, 0.001);
                    vignetteFactor = saturate(vignetteFactor) * _VignetteIntensity;
                }

                // Apply darkening
                finalColor.rgb *= (1.0 - vignetteFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }

    // Fallback for older/integrated GPUs - simpler blur (vignette only)
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 50
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "RadialBlurFallback"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _VignetteCoverage;
            float _VignetteIntensity;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Simple vignette only (no blur)
                float2 center = float2(0.5, 0.5);
                float distanceFromCenter = length(uv - center);
                float normalizedDist = distanceFromCenter / 0.707;

                float vignetteStart = 1.0 - (_VignetteCoverage / 100.0);
                float vignetteFactor = 0.0;
                if (normalizedDist > vignetteStart)
                {
                    vignetteFactor = (normalizedDist - vignetteStart) / max(1.0 - vignetteStart, 0.001);
                    vignetteFactor = saturate(vignetteFactor) * _VignetteIntensity;
                }

                color.rgb *= (1.0 - vignetteFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
