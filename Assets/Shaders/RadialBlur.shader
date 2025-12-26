Shader "Hidden/PostProcess/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurAngleDegrees ("Clear Radius Percentage", Float) = 85.0
        _BlurIntensity ("Blur Intensity", Float) = 0.5
        _BlurSamples ("Blur Samples", Float) = 8.0
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

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float _BlurAngleDegrees;  // Clear radius as percentage (10 = 10% of screen radius is clear)
            float _BlurIntensity;      // Intensity of the blur effect
            float _BlurSamples;        // Number of blur samples

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center
                float2 center = float2(0.5, 0.5);
                float distanceFromCenter = length(uv - center);

                // Max distance is from center to corner (~0.707)
                float maxDistance = 0.707;

                // Normalize distance (0 = center, 1 = corner)
                float normalizedDist = saturate(distanceFromCenter / maxDistance);

                // Calculate where blur should start (blurAngleDegrees% toward edge)
                float blurStart = _BlurAngleDegrees / 100.0;

                // If we're inside the clear zone, return unblurred
                if (normalizedDist < blurStart)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Calculate blur strength from 0 (at blur start) to intensity (at edge)
                float blurFactor = (normalizedDist - blurStart) / max(1.0 - blurStart, 0.001);
                blurFactor = saturate(blurFactor) * _BlurIntensity;

                // If blur is essentially zero, return original
                if (blurFactor < 0.001)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Sample original color
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);

                // Simple box blur with clamped UVs
                float4 blurAccum = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;
                float blurRadius = blurFactor * 0.02; // Small blur radius

                [loop]
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)i / (float)samples * 6.28318530718;
                    float2 offset = float2(cos(angle), sin(angle)) * blurRadius;
                    float2 sampleUV = saturate(uv + offset); // Clamp to [0,1]

                    blurAccum += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, sampleUV);
                }

                float4 blurredColor = blurAccum / (float)samples;

                // Blend original and blurred based on blur factor
                return lerp(color, blurredColor, blurFactor);
            }
            ENDHLSL
        }
    }
}
