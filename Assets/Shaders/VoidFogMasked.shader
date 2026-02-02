Shader "Custom/VoidFogMasked"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.7, 0.75, 0.8, 1.0)
        _FogColorDark ("Fog Color Dark", Color) = (0.4, 0.45, 0.5, 1.0)
        _PathMask ("Path Mask", 2D) = "white" {}

        [Header(Cloud Shape)]
        _CloudScale ("Cloud Scale", Range(0.5, 10)) = 3.0
        _CloudDetail ("Cloud Detail", Range(0.5, 5)) = 2.0
        _CloudDensity ("Cloud Density", Range(0.1, 2)) = 0.8
        _CloudSharpness ("Cloud Sharpness", Range(1, 10)) = 3.0

        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 0.5)) = 0.05
        _WindDirection ("Wind Direction", Vector) = (1, 0.3, 0, 0)

        [Header(Lighting)]
        _LightDirection ("Light Direction", Vector) = (0.5, 1, 0, 0)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.3
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.6
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

            TEXTURE2D(_PathMask);
            SAMPLER(sampler_PathMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half4 _FogColorDark;
                float _CloudScale;
                float _CloudDetail;
                float _CloudDensity;
                float _CloudSharpness;
                float _WindSpeed;
                float4 _WindDirection;
                float4 _LightDirection;
                float _ShadowStrength;
                float _AmbientLight;
            CBUFFER_END

            // Improved hash function for better randomness
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Value noise with smooth interpolation
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Quintic interpolation for smoother results
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Fractal Brownian Motion for cloud-like patterns
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;

                for (int idx = 0; idx < octaves; idx++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                    // Rotate each octave slightly for more organic look
                    p = float2(p.x * 0.866 - p.y * 0.5, p.x * 0.5 + p.y * 0.866);
                }

                return value / maxValue;
            }

            // Billowy cloud function
            float cloudNoise(float2 p)
            {
                // Base cloud shape
                float baseCloud = fbm(p * _CloudScale, 5);

                // Add finer detail
                float detail = fbm(p * _CloudScale * _CloudDetail, 4) * 0.5;

                // Combine and shape
                float cloud = baseCloud + detail * 0.3;

                // Apply density and create billowy edges
                cloud = cloud * _CloudDensity;
                cloud = pow(saturate(cloud), 1.0 / _CloudSharpness);

                return cloud;
            }

            // Calculate pseudo-3D lighting for clouds
            float cloudLighting(float2 p, float cloudValue)
            {
                float2 lightDir = normalize(_LightDirection.xy);
                float offset = 0.02;

                // Sample nearby points to estimate "height" gradient
                float cloudLeft = cloudNoise(p - lightDir * offset);
                float cloudRight = cloudNoise(p + lightDir * offset);

                // Calculate lighting based on gradient
                float gradient = (cloudRight - cloudLeft) / (offset * 2.0);
                float lighting = saturate(_AmbientLight + gradient * _ShadowStrength);

                return lighting;
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
                // Sample the path mask (0 = no fog/path, 1 = full fog/void)
                float pathMask = SAMPLE_TEXTURE2D(_PathMask, sampler_PathMask, IN.uv).r;

                // Early out if completely transparent
                if (pathMask < 0.01)
                    return half4(0, 0, 0, 0);

                // Animate cloud position with wind
                float2 windOffset = _WindDirection.xy * _Time.y * _WindSpeed;
                float2 cloudUV = IN.worldPos.xy * 0.1 + windOffset;

                // Generate cloud pattern
                float cloudValue = cloudNoise(cloudUV);

                // Calculate lighting
                float lighting = cloudLighting(cloudUV, cloudValue);

                // Blend between light and dark fog colors based on lighting
                half3 fogCol = lerp(_FogColorDark.rgb, _FogColor.rgb, lighting);

                // Apply cloud shape to alpha
                // Make clouds more billowy by using the cloud value to modulate alpha
                float cloudAlpha = cloudValue;

                // Soften edges where cloud meets clear area
                cloudAlpha = smoothstep(0.1, 0.6, cloudAlpha);

                // Combine with path mask
                float finalAlpha = cloudAlpha * pathMask * _FogColor.a;

                // Add some variation at the edges for wispy look
                float edgeNoise = fbm(cloudUV * 4.0 + windOffset * 2.0, 3);
                float edgeFade = smoothstep(0.0, 0.3, pathMask);
                finalAlpha *= lerp(edgeNoise * 0.5 + 0.5, 1.0, edgeFade);

                return half4(fogCol, saturate(finalAlpha));
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
            sampler2D _PathMask;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float pathMask = tex2D(_PathMask, i.uv).r;
                if (pathMask < 0.01)
                    return fixed4(0, 0, 0, 0);
                return fixed4(_FogColor.rgb, _FogColor.a * pathMask * 0.8);
            }
            ENDCG
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
