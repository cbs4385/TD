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
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            fixed4 _DeepShadow;
            fixed4 _DarkBase;
            fixed4 _MidTone;
            fixed4 _LightMid;
            fixed4 _Highlight;
            float _NoiseScale;
            float _DetailScale;
            float _ColorVariation;
            float _EdgeDarkening;

            // Hash functions for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal Brownian Motion for more natural look
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value;
            }

            // Voronoi-like pattern for dirt clumps
            float voronoi(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float minDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 cellPoint = hash(i + neighbor) * 0.5 + 0.25;
                        float2 diff = neighbor + cellPoint - f;
                        float dist = length(diff);
                        minDist = min(minDist, dist);
                    }
                }
                return minDist;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            // Sample the 5-stop gradient based on t (0-1)
            fixed4 SampleGradient(float t)
            {
                t = saturate(t);

                if (t < 0.25)
                {
                    // Deep shadow to Dark base
                    return lerp(_DeepShadow, _DarkBase, t / 0.25);
                }
                else if (t < 0.5)
                {
                    // Dark base to Mid tone
                    return lerp(_DarkBase, _MidTone, (t - 0.25) / 0.25);
                }
                else if (t < 0.75)
                {
                    // Mid tone to Light mid
                    return lerp(_MidTone, _LightMid, (t - 0.5) / 0.25);
                }
                else
                {
                    // Light mid to Highlight
                    return lerp(_LightMid, _Highlight, (t - 0.75) / 0.25);
                }
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Use world position for seamless tiling across tiles
                float2 worldUV = i.worldPos.xy;

                // Multiple noise layers at different frequencies for speckled look
                float noise1 = noise(worldUV * _NoiseScale * 0.5);
                float noise2 = noise(worldUV * _NoiseScale * 1.2);
                float noise3 = noise(worldUV * _NoiseScale * 2.5);
                float noise4 = noise(worldUV * _DetailScale);        // Fine speckle
                float noise5 = noise(worldUV * _DetailScale * 2.0);  // Extra fine speckle

                // FBM for larger variation
                float largeVariation = fbm(worldUV * _NoiseScale * 0.2);

                // Voronoi for irregular patches
                float voronoiPattern = voronoi(worldUV * _NoiseScale * 0.4);

                // Create highly speckled base by combining sharp noise transitions
                float speckle1 = step(0.45, noise4) * step(noise4, 0.55) ? 1.0 : 0.0;
                float speckle2 = step(0.4, noise5) * step(noise5, 0.6) ? 0.8 : 0.0;
                float speckleIntensity = max(speckle1, speckle2) * 0.3;

                // Combine noises with more variation
                float colorMix = noise1 * 0.15 + noise2 * 0.25 + noise3 * 0.2 + largeVariation * 0.2 + noise4 * 0.2;

                // Add voronoi crevice influence
                float voronoiDark = smoothstep(0.0, 0.12, voronoiPattern);
                colorMix *= lerp(0.5, 1.0, voronoiDark);

                // Add random speckle highlights
                colorMix += speckleIntensity;

                // Expand range to use full gradient
                colorMix = saturate(colorMix * 1.3 - 0.1);

                // Sample the 5-stop gradient
                fixed4 groundColor = SampleGradient(colorMix);

                // Add fine grain speckle variation directly to color
                float grainSpeckle = (noise5 - 0.5) * 0.12 * _ColorVariation;
                groundColor.rgb += grainSpeckle;

                // Random light/dark spots for more texture
                float spots = noise(worldUV * _DetailScale * 1.5);
                float spotIntensity = smoothstep(0.6, 0.65, spots) - smoothstep(0.35, 0.4, spots) * 0.5;
                groundColor.rgb += spotIntensity * 0.08;

                // Edge darkening based on UV (subtle, for depth between tiles)
                float2 edgeDist = min(i.uv, 1.0 - i.uv);
                float edgeFactor = smoothstep(0.0, 0.08, min(edgeDist.x, edgeDist.y));
                groundColor.rgb *= lerp(1.0 - _EdgeDarkening, 1.0, edgeFactor);

                // Very subtle directional lighting
                float3 lightDir = normalize(float3(0.2, 0.4, -1.0));
                float ndotl = dot(i.worldNormal, lightDir) * 0.08 + 0.92;
                groundColor.rgb *= ndotl;

                groundColor.a = 1.0;
                return groundColor;
            }
            ENDCG
        }
    }

    // Fallback for older/integrated GPUs - simplified noise
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 50

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _DarkBase;
            fixed4 _MidTone;
            float _NoiseScale;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float n = noise(i.worldPos.xy * _NoiseScale);
                return lerp(_DarkBase, _MidTone, n);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
