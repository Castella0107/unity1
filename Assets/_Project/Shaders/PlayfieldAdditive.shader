// プレイフィールド用: 頂点カラー × テクスチャの加算合成シェーダ (グロー用)。
// PlayfieldAlpha と同様、renderQueue 直指定で描画順を厳密制御する。
Shader "Playfield/Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        ColorMask RGB
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _BaseColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Blend SrcAlpha One がアルファ乗算を行うため、ここでは乗算しない
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
