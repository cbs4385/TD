Shader "Custom/FocalPointBolt"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        // Render after the void fog so stencil is already written
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
            Blend SrcAlpha One // Additive blending for glow
            Cull Off

            // Only render where stencil is NOT 1 (i.e., not in fog areas)
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return IN.color * _Color;
            }
            ENDHLSL
        }
    }

    // Fallback for older/integrated GPUs (no stencil)
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" }
        LOD 50

        Pass
        {
            ZWrite Off
            Blend SrcAlpha One
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color * _Color;
            }
            ENDCG
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
