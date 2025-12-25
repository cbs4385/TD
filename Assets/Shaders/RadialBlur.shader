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

            float _BlurAngleDegrees;  // Clear radius as percentage (10 = 10% of screen radius is clear)
            float _BlurIntensity;      // Intensity of the blur effect
            float _BlurSamples;        // Number of blur samples

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center (0.5, 0.5) in normalized screen space
                float2 center = float2(0.5, 0.5);
                float2 offset = uv - center;

                // Normalize distance (0 at center, ~0.707 at corners for square screen)
                float distanceFromCenter = length(offset);

                // Convert _BlurAngleDegrees from percentage to screen space radius
                // e.g., 10 means 10% of screen, which is 0.1 in normalized coords
                float clearRadius = _BlurAngleDegrees / 100.0;

                // If within the clear radius, return original pixel
                if (distanceFromCenter < clearRadius)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Calculate blur amount based on distance from clear radius
                // The further from center, the stronger the blur
                float blurAmount = saturate((distanceFromCenter - clearRadius) / (0.5 - clearRadius)) * _BlurIntensity;

                // Apply box blur by sampling in a circular pattern
                float4 color = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;

                // Sample in a circular pattern around the current pixel
                float blurRadius = blurAmount * 0.1; // Scale blur radius

                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)i / (float)samples * 6.28318530718; // 2*PI
                    float2 sampleOffset = float2(cos(angle), sin(angle)) * blurRadius;
                    float2 sampleUV = uv + sampleOffset;
                    color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, sampleUV);
                }

                color /= (float)samples;
                return color;
            }
            ENDHLSL
        }
    }
}
