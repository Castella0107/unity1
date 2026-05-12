Shader "UI/JacketBlur"
{
    Properties
    {
        _MainTex    ("Texture",    2D)            = "white" {}
        _BlurSize   ("Blur Size",  Range(0, 10))  = 4
        _Brightness ("Brightness", Range(0, 1))   = 0.5
        _Tint       ("Tint Color", Color)         = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _BlurSize;
            float     _Brightness;
            fixed4    _Tint;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy * _BlurSize;

                // 9-tap Gaussian approximation (weights sum to 1.0)
                fixed4 sum = tex2D(_MainTex, i.uv)                          * 0.20;

                sum += tex2D(_MainTex, i.uv + float2( ts.x,    0)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2(-ts.x,    0)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2(    0,  ts.y)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2(    0, -ts.y)) * 0.10;

                sum += tex2D(_MainTex, i.uv + float2( ts.x,  ts.y)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2(-ts.x,  ts.y)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2( ts.x, -ts.y)) * 0.10;
                sum += tex2D(_MainTex, i.uv + float2(-ts.x, -ts.y)) * 0.10;

                sum.rgb *= _Tint.rgb * _Brightness;
                sum.a   *= _Tint.a * i.color.a;

                return sum;
            }
            ENDCG
        }
    }
}
