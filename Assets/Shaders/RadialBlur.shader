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

                // Sample the original color
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);

                // Calculate distance from center
                float2 center = float2(0.5, 0.5);
                float distanceFromCenter = length(uv - center);

                // Normalize distance (0 = center, ~0.707 = corner)
                float normalizedDist = distanceFromCenter / 0.707;

                // Calculate vignette darkness based on distance
                // blurAngleDegrees controls where darkening starts (0-100%)
                float vignetteStart = _BlurAngleDegrees / 100.0;

                // Calculate darkness factor (0 = no darkening, 1 = full darkening)
                float darkness = 0.0;
                if (normalizedDist > vignetteStart)
                {
                    // Progressive darkening from vignette start to edge
                    float edgeFactor = (normalizedDist - vignetteStart) / (1.0 - vignetteStart);
                    darkness = saturate(edgeFactor) * _BlurIntensity;
                }

                // Apply darkening by lerping toward black
                return lerp(color, float4(0, 0, 0, color.a), darkness);
            }
            ENDHLSL
        }
    }
}
