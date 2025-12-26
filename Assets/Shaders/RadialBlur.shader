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

            // Try to pull in the URP Blit bindings when available; fall back to locally
            // declaring the texture/sampler if the include is missing in the installed
            // package version.
            #if defined(UNITY_SHADER_INCLUDE_TEST)
                #if UNITY_SHADER_INCLUDE_TEST("Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl")
                    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl"
                #endif
            #endif

            #if !defined(UNIVERSAL_BLIT_INCLUDED)
                TEXTURE2D_X(_BlitTexture);
                SAMPLER(sampler_BlitTexture);

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float2 texcoord   : TEXCOORD0;
                };

                struct Varyings
                {
                    float4 positionCS : SV_POSITION;
                    float2 texcoord   : TEXCOORD0;
                };

                Varyings Vert(Attributes input)
                {
                    Varyings output;
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                    output.texcoord = input.texcoord;
                    return output;
                }
            #endif

            float _BlurAngleDegrees;  // Clear radius as percentage (10 = 10% of screen radius is clear)
            float _BlurIntensity;      // Intensity of the blur effect
            float _BlurSamples;        // Number of blur samples

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Simple passthrough for testing
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
            }
            ENDHLSL
        }
    }
}
