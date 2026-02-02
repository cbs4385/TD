Shader "Custom/NightSkyGradient"
{
    Properties
    {
        _BottomColor ("Bottom Color (Forest)", Color) = (0.039, 0.039, 0.039, 1)
        _TopColor ("Top Color (Night Sky)", Color) = (0.05, 0.05, 0.15, 1)
        _GradientCenter ("Gradient Center Y", Range(0, 1)) = 0.3
        _GradientSoftness ("Gradient Softness", Range(0.01, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BottomColor;
                half4 _TopColor;
                float _GradientCenter;
                float _GradientSoftness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Calculate gradient based on UV.y (vertical position)
                // Use smoothstep for a nice soft transition
                float gradientT = smoothstep(_GradientCenter - _GradientSoftness, _GradientCenter + _GradientSoftness, IN.uv.y);

                // Lerp between bottom (forest) and top (night sky)
                half4 color = lerp(_BottomColor, _TopColor, gradientT);
                color.a = 1.0;

                return color;
            }
            ENDHLSL
        }
    }

    // Fallback for older systems
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 50

        Pass
        {
            ZWrite Off
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

            fixed4 _BottomColor;
            fixed4 _TopColor;
            float _GradientCenter;
            float _GradientSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float gradientT = smoothstep(_GradientCenter - _GradientSoftness, _GradientCenter + _GradientSoftness, i.uv.y);
                fixed4 color = lerp(_BottomColor, _TopColor, gradientT);
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
