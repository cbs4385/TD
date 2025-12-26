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

                // Calculate distance from center (0.5, 0.5) in normalized screen space
                float2 center = float2(0.5, 0.5);
                float2 offset = uv - center;

                // Distance from center (0 at center, ~0.707 at corners)
                float distanceFromCenter = length(offset);

                // Maximum distance to screen edge (diagonal)
                float maxDistance = 0.707;

                // Convert percentage to actual radius threshold
                // 85% means blur starts at 85% from center toward edge
                // So clearRadiusNormalized = 0.85 * maxDistance
                float clearRadiusNormalized = (_BlurAngleDegrees / 100.0) * maxDistance;

                // Calculate how far we are from center to edge (0 = center, 1 = edge)
                float edgeFactor = distanceFromCenter / maxDistance;

                // Calculate blur start point (0 to 1)
                float blurStartFactor = _BlurAngleDegrees / 100.0;

                // If we're inside the clear zone, no blur at all
                if (edgeFactor < blurStartFactor)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Calculate blur strength (0 at blur start, 1 at edge)
                float blurStrength = (edgeFactor - blurStartFactor) / (1.0 - blurStartFactor);
                blurStrength = saturate(blurStrength) * _BlurIntensity;

                // Sample original pixel
                float4 originalColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);

                // Apply radial blur sampling
                float4 blurColor = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;
                float totalWeight = 0.0;

                // Blur radius scales with distance and intensity
                float blurRadius = blurStrength * 0.05;

                [loop]
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)i / (float)samples * 6.28318530718; // 2*PI
                    float2 sampleOffset = float2(cos(angle), sin(angle)) * blurRadius;
                    float2 sampleUV = uv + sampleOffset;

                    blurColor += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, sampleUV);
                    totalWeight += 1.0;
                }

                blurColor /= totalWeight;

                // Lerp between original and blurred based on blur strength
                return lerp(originalColor, blurColor, blurStrength);
            }
            ENDHLSL
        }
    }
}
