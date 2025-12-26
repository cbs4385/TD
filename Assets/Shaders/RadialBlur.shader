Shader "Hidden/PostProcess/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurAngleDegrees ("Clear Radius Percentage", Float) = 85.0
        _BlurIntensity ("Blur Intensity", Float) = 0.5
        _BlurSamples ("Blur Samples", Float) = 4.0
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

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float _BlurAngleDegrees;     // Clear radius as percentage (10 = 10% of screen radius is clear)
            float _BlurIntensity;        // Intensity of the blur effect
            float _BlurSamples;          // Number of blur samples
            float _VignetteCoverage;     // Vignette coverage as percentage (0-100)
            float _VignetteIntensity;    // Vignette darkness intensity (0-1)

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Calculate distance from center and keep the vignette perfectly round
                // by correcting for aspect ratio (so 1,1 reaches the screen corners)
                float2 center = float2(0.5, 0.5);
                float2 aspectCorrectedUV = (uv - center);
                aspectCorrectedUV.x *= (_ScreenParams.x / _ScreenParams.y);
                float distanceFromCenter = length(aspectCorrectedUV);

                // Normalize distance (0 = center, 1 = furthest corner after aspect correction)
                float maxDistance = length(float2(0.5 * (_ScreenParams.x / _ScreenParams.y), 0.5));
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

                    blurAccum += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, sampleUV);
                }

                float4 finalColor = blurAccum / (float)samples;

                // Apply vignette effect with rounded falloff
                // Calculate where vignette should start (inverse of coverage percentage)
                float vignetteStart = saturate(1.0 - (_VignetteCoverage / 100.0));

                // Calculate vignette darkening factor with smoother roundness
                float vignetteFactor = saturate((normalizedDist - vignetteStart) / max(1.0 - vignetteStart, 0.001));
                vignetteFactor = pow(vignetteFactor, 2.0); // Rounded, smoother edge
                vignetteFactor *= _VignetteIntensity;

                // Apply darkening
                finalColor.rgb *= (1.0 - vignetteFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
