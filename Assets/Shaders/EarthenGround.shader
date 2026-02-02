Shader "Custom/EarthenGround"
{
    Properties
    {
        // 5-stop gradient based on actual EarthenRingGround material analysis
        _DeepShadow ("Deep Shadow (Crevices)", Color) = (0.309, 0.265, 0.243, 1.0)
        _DarkBase ("Dark Base", Color) = (0.349, 0.307, 0.280, 1.0)
        _MidTone ("Mid Tone", Color) = (0.455, 0.382, 0.356, 1.0)
        _LightMid ("Light Mid / Dusty", Color) = (0.507, 0.466, 0.442, 1.0)
        _Highlight ("Top Highlight (Ridges)", Color) = (0.584, 0.549, 0.539, 1.0)
        _NoiseScale ("Noise Scale", Range(0.5, 30)) = 8.0
        _DetailScale ("Detail Scale", Range(1, 100)) = 25.0
        _ColorVariation ("Color Variation", Range(0, 1)) = 0.6
        _EdgeDarkening ("Edge Darkening", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepShadow;
                half4 _DarkBase;
                half4 _MidTone;
                half4 _LightMid;
                half4 _Highlight;
                float _NoiseScale;
                float _DetailScale;
                float _ColorVariation;
                float _EdgeDarkening;
            CBUFFER_END

            // High quality hash - uses multiple primes to avoid patterns
            float hash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Value noise with smooth interpolation
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Quintic interpolation for smoother results
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float a = hash(i + float2(0.0, 0.0));
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Fractal Brownian Motion - unrolled for compatibility
            float fbm(float2 p)
            {
                float value = 0.0;
                float amp = 0.5;

                value += amp * noise(p); p *= 2.0; amp *= 0.5;
                value += amp * noise(p); p *= 2.0; amp *= 0.5;
                value += amp * noise(p); p *= 2.0; amp *= 0.5;
                value += amp * noise(p);

                return value;
            }

            // Voronoi - unrolled loops for compatibility
            float voronoi(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float minDist = 1.0;

                // Unrolled 3x3 neighbor check
                float2 n, cp, diff;
                float d;

                n = float2(-1, -1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 0, -1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 1, -1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2(-1,  0); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 0,  0); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 1,  0); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2(-1,  1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 0,  1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);
                n = float2( 1,  1); cp = float2(hash(i + n), hash(i + n + float2(0.5, 0.5))) * 0.5 + 0.25; diff = n + cp - f; d = dot(diff, diff); minDist = min(minDist, d);

                return sqrt(minDist);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // Sample the 5-stop gradient based on t (0-1)
            half4 SampleGradient(float t)
            {
                t = saturate(t);

                // Use smoothstep blending between gradient stops
                half4 color = _DeepShadow;
                color = lerp(color, _DarkBase, saturate(t * 4.0));
                color = lerp(color, _MidTone, saturate((t - 0.25) * 4.0));
                color = lerp(color, _LightMid, saturate((t - 0.5) * 4.0));
                color = lerp(color, _Highlight, saturate((t - 0.75) * 4.0));

                return color;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Use world position for seamless tiling across tiles
                float2 worldUV = IN.worldPos.xy;

                // Multiple noise layers at different frequencies for speckled look
                float noise1 = noise(worldUV * _NoiseScale * 0.5);
                float noise2 = noise(worldUV * _NoiseScale * 1.2);
                float noise3 = noise(worldUV * _NoiseScale * 2.5);
                float noise4 = noise(worldUV * _DetailScale);
                float noise5 = noise(worldUV * _DetailScale * 2.0);

                // FBM for larger variation
                float largeVariation = fbm(worldUV * _NoiseScale * 0.2);

                // Voronoi for irregular patches
                float voronoiPattern = voronoi(worldUV * _NoiseScale * 0.4);

                // Create speckled base
                float speckle = step(0.45, noise4) * step(noise4, 0.55) * 0.3;
                speckle = max(speckle, step(0.4, noise5) * step(noise5, 0.6) * 0.24);

                // Combine noises with more variation
                float colorMix = noise1 * 0.15 + noise2 * 0.25 + noise3 * 0.2 + largeVariation * 0.2 + noise4 * 0.2;

                // Add voronoi crevice influence
                float voronoiDark = smoothstep(0.0, 0.12, voronoiPattern);
                colorMix *= lerp(0.5, 1.0, voronoiDark);

                // Add random speckle highlights
                colorMix += speckle;

                // Expand range to use full gradient
                colorMix = saturate(colorMix * 1.3 - 0.1);

                // Sample the 5-stop gradient
                half4 groundColor = SampleGradient(colorMix);

                // Add fine grain speckle variation directly to color
                float grainSpeckle = (noise5 - 0.5) * 0.12 * _ColorVariation;
                groundColor.rgb += grainSpeckle;

                // Random light/dark spots for more texture
                float spots = noise(worldUV * _DetailScale * 1.5);
                float spotIntensity = smoothstep(0.6, 0.65, spots) - smoothstep(0.35, 0.4, spots) * 0.5;
                groundColor.rgb += spotIntensity * 0.08;

                // Edge darkening based on UV (subtle, for depth between tiles)
                float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
                float edgeFactor = smoothstep(0.0, 0.08, min(edgeDist.x, edgeDist.y));
                groundColor.rgb *= lerp(1.0 - _EdgeDarkening, 1.0, edgeFactor);

                // Very subtle directional lighting
                float3 lightDir = normalize(float3(0.2, 0.4, -1.0));
                float ndotl = dot(IN.worldNormal, lightDir) * 0.08 + 0.92;
                groundColor.rgb *= ndotl;

                groundColor.a = 1.0;
                return groundColor;
            }
            ENDHLSL
        }

        // Depth pass for shadows
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthOnlyFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    // NO FALLBACK - if this shader fails, we want to see the error, not silently use wrong shader
    // FallBack Off would show magenta, which is better for debugging than wrong colors
    FallBack Off
}
