// プレイフィールド用: 頂点カラー × テクスチャの単純アルファ合成シェーダ。
// URP のマテリアル検証で renderQueue が正規化されないよう、素の unlit シェーダとして実装する
// (描画順はマテリアルの renderQueue 直指定で厳密に制御する)。
Shader "Playfield/Alpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        // FAR_WALL_SPEC: 1 で透明遮蔽壁クリップ有効 (ワールド z > _PlayfieldWallZ を破棄)。
        // 壁位置はグローバル _PlayfieldWallZ (CenterTrackVisuals が毎フレーム設定)。
        _WallClipOn ("Wall Clip On", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            float _WallClipOn;
            float _PlayfieldWallZ;   // グローバル: 透明遮蔽壁のワールド z (これより奥を破棄)

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
                float3 wpos  : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _BaseColor;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 透明遮蔽壁 (FAR_WALL_SPEC §2): 壁より奥はピクセル破棄 (ハードエッジ)。
                if (_WallClipOn > 0.5) clip(_PlayfieldWallZ - i.wpos.z);
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
