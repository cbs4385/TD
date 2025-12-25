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

            float _BlurAngleDegrees;  // Angle in degrees from center where blur starts
            float _BlurIntensity;      // Intensity of the blur effect
            float _BlurSamples;        // Number of blur samples

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center (0.5, 0.5)
                float2 center = float2(0.5, 0.5);
                float2 offset = uv - center;

                // Calculate angle from center based on screen aspect and camera FOV
                // Approximate angle in degrees (assuming 60 degree FOV)
                float distance = length(offset);
                float angleFromCenter = distance * 60.0; // Rough approximation

                // If within the clear angle, return original pixel
                if (angleFromCenter < _BlurAngleDegrees)
                {
                    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);
                }

                // Apply radial blur
                float blurAmount = saturate((angleFromCenter - _BlurAngleDegrees) / 10.0) * _BlurIntensity;

                float4 color = float4(0, 0, 0, 0);
                int samples = (int)_BlurSamples;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / (float)samples;
                    float2 sampleOffset = offset * t * blurAmount * 0.05;
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
