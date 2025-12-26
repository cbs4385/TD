Shader "Hidden/PostProcess/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            float _ClearRadiusPercent;  // Clear radius as percentage (80 = center 80% is clear, outer 20% is blurred)
            float _BlurIntensity;       // Intensity of the blur effect
            float _BlurSamples;         // Number of blur samples

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center (0.5, 0.5) in normalized screen space
                float2 center = float2(0.5, 0.5);
                float2 offset = uv - center;

                // Normalize distance (0 at center, ~0.707 at corners for square screen)
                float distanceFromCenter = length(offset);

                // Convert _ClearRadiusPercent from percentage to screen space radius
                // e.g., 80 means 80% of screen, which is 0.8 in normalized coords
                float clearRadius = _ClearRadiusPercent / 100.0;

                // If within the clear radius, return original pixel
                if (distanceFromCenter < clearRadius)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Calculate blur amount based on distance from clear radius
                // The further from center, the stronger the blur
                float normalizedDistance = (distanceFromCenter - clearRadius) / (0.707 - clearRadius);
                float blurAmount = saturate(normalizedDistance) * _BlurIntensity;

                // Apply multi-ring blur by sampling in concentric circles
                float4 color = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;
                float totalWeight = 0.0;

                // Sample in multiple rings for better blur quality
                int rings = 4;
                // Much stronger blur - scale based on distance and intensity
                // Far from center = much stronger blur
                float blurRadius = blurAmount * 0.08 * (1.0 + normalizedDistance * 4.0);

                [loop]
                for (int ring = 0; ring <= rings; ring++)
                {
                    float ringRadius = blurRadius * ((float)ring / (float)rings);

                    [loop]
                    for (int i = 0; i < samples; i++)
                    {
                        float angle = (float)i / (float)samples * 6.28318530718; // 2*PI
                        float2 sampleOffset = float2(cos(angle), sin(angle)) * ringRadius;
                        float2 sampleUV = uv + sampleOffset;

                        float weight = 1.0;
                        color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, sampleUV) * weight;
                        totalWeight += weight;
                    }
                }

                color /= totalWeight;
                return color;
            }
            ENDHLSL
        }
    }
}
