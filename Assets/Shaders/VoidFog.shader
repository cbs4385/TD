Shader "Custom/VoidFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.03, 0.85)
        _EdgeFade ("Edge Fade", Range(0.0, 0.5)) = 0.15
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 2.0
        _NoiseSpeed ("Noise Speed", Range(0, 1)) = 0.1
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _EdgeFade;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
            CBUFFER_END

            // Simple noise function
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int idx = 0; idx < 3; idx++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Calculate edge fade (fade out near edges of quad)
                float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
                float edgeFade = smoothstep(0.0, _EdgeFade, min(edgeDist.x, edgeDist.y));

                // Animated noise for fog variation
                float2 noiseUV = IN.worldPos.xy * _NoiseScale + _Time.y * _NoiseSpeed;
                float noiseValue = fbm(noiseUV);

                // Apply noise to alpha
                float alpha = _FogColor.a * edgeFade;
                alpha *= (1.0 - _NoiseStrength + noiseValue * _NoiseStrength * 2.0);

                // Clamp alpha
                alpha = saturate(alpha);

                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    // Fallback for older/integrated GPUs
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" }
        LOD 50

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

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
            };

            fixed4 _FogColor;
            float _EdgeFade;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 edgeDist = min(i.uv, 1.0 - i.uv);
                float edgeFade = smoothstep(0.0, _EdgeFade, min(edgeDist.x, edgeDist.y));
                return fixed4(_FogColor.rgb, _FogColor.a * edgeFade);
            }
            ENDCG
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
