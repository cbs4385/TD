Shader "Custom/DevourDust"
{
    Properties
    {
        _MainTex ("Dust Texture (EarthenGroundTexture)", 2D) = "white" {}
        _Alpha ("Overall Alpha", Range(0, 1)) = 0.7

        [Header(Cloud Shape)]
        _CloudScale ("Cloud Scale", Range(1, 20)) = 8.0
        _CloudDetail ("Cloud Detail", Range(0.5, 5)) = 2.5
        _CloudDensity ("Cloud Density", Range(0.1, 3)) = 1.5
        _CloudSharpness ("Cloud Sharpness", Range(1, 10)) = 3.0

        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 1)) = 0.2
        _TurbulenceSpeed ("Turbulence Speed", Range(0, 2)) = 0.5
        _TextureScrollSpeed ("Texture Scroll Speed", Range(0, 0.5)) = 0.1

        [Header(Edge Fade)]
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Alpha;

            float _CloudScale;
            float _CloudDetail;
            float _CloudDensity;
            float _CloudSharpness;

            float _WindSpeed;
            float _TurbulenceSpeed;
            float _TextureScrollSpeed;
            float _EdgeFade;

            // Hash function for noise
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

            // Fractal Brownian Motion for layered noise
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;

                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                    // Rotate each octave for more organic look
                    p = float2(p.x * 0.866 - p.y * 0.5, p.x * 0.5 + p.y * 0.866);
                }

                return value / maxValue;
            }

            // Churning dust cloud noise
            float dustNoise(float2 p, float time)
            {
                // Base large-scale clouds
                float2 windOffset = float2(time * _WindSpeed, time * _WindSpeed * 0.7);
                float baseCloud = fbm((p + windOffset) * _CloudScale, 4);

                // Turbulent detail layer moving in different direction
                float2 turbOffset = float2(-time * _TurbulenceSpeed * 0.5, time * _TurbulenceSpeed);
                float detail = fbm((p + turbOffset) * _CloudScale * _CloudDetail, 3);

                // Combine layers
                float cloud = baseCloud * 0.7 + detail * 0.3;
                cloud = cloud * _CloudDensity;

                // Apply sharpness
                cloud = pow(saturate(cloud), 1.0 / _CloudSharpness);

                return cloud;
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
                // Calculate distance from center (UV 0.5, 0.5 is center)
                float2 centerOffset = i.uv - float2(0.5, 0.5);
                float distFromCenter = length(centerOffset) * 2.0; // 0 at center, 1 at edge

                // Circular mask with soft edge fade
                float circleMask = 1.0 - saturate((distFromCenter - (1.0 - _EdgeFade)) / _EdgeFade);
                circleMask = circleMask * circleMask; // Quadratic falloff

                // Early out if outside circle
                if (circleMask < 0.01)
                    return fixed4(0, 0, 0, 0);

                // Generate animated dust pattern for alpha modulation
                float dustValue = dustNoise(i.worldPos.xy * 0.15, _Time.y);

                // Animated texture UVs - scroll and distort with noise
                float2 texScrollOffset = float2(_Time.y * _TextureScrollSpeed, _Time.y * _TextureScrollSpeed * 0.7);
                float2 noiseOffset = float2(
                    dustNoise(i.worldPos.xy * 0.1, _Time.y * 0.5) - 0.5,
                    dustNoise(i.worldPos.xy * 0.1 + 100, _Time.y * 0.5) - 0.5
                ) * 0.3;

                // Sample texture with animated UVs (tile it across the area)
                float2 texUV = i.worldPos.xy * 0.3 + texScrollOffset + noiseOffset;
                fixed4 texColor = tex2D(_MainTex, texUV);

                // Final alpha combines circle mask, dust pattern, and overall alpha
                float alpha = circleMask * dustValue * _Alpha;

                // Boost visibility
                alpha = saturate(alpha * 1.8);

                return fixed4(texColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
