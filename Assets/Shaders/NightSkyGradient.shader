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
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100

        Pass
        {
            ZWrite Off
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
                // Calculate gradient based on UV.y (vertical position)
                // Use smoothstep for a nice soft transition
                float gradientT = smoothstep(_GradientCenter - _GradientSoftness, _GradientCenter + _GradientSoftness, i.uv.y);

                // Lerp between bottom (forest) and top (night sky)
                fixed4 color = lerp(_BottomColor, _TopColor, gradientT);
                color.a = 1.0;

                return color;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
