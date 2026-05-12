Shader "UI/BeatGrid"
{
    Properties
    {
        _MainTex        ("Texture",        2D)            = "white" {}
        _GridColor      ("Grid Color",     Color)         = (0.3, 0.85, 1.0, 0.4)
        _GridDensity    ("Grid Density",   Range(2, 50))  = 12
        _LineWidth      ("Line Width",     Range(0.001, 0.1)) = 0.015
        _GridScale      ("Grid Scale",     Range(0.5, 2.0))   = 1.0
        _PulseIntensity ("Pulse Intensity",Range(0, 2))   = 1.0
        _BaseAlpha      ("Base Alpha",     Range(0, 1))   = 0.15
        _UserIntensity  ("User Intensity", Range(0, 1))   = 1.0
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
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4    _GridColor;
            float     _GridDensity;
            float     _LineWidth;
            float     _GridScale;
            float     _PulseIntensity;
            float     _BaseAlpha;
            float     _UserIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Scale from center for pulse expand/contract
                float2 centered = i.uv - 0.5;
                centered /= _GridScale;
                float2 uv = centered + 0.5;

                // Grid cell coordinates
                float2 grid      = uv * _GridDensity;
                float2 gridFract = abs(frac(grid) - 0.5);  // 0 = on line, 0.5 = cell centre

                // Distance from nearest line in UV space
                float distToLine = min(gridFract.x, gridFract.y) / _GridDensity;

                // Anti-aliased line mask
                float lineMask = 1.0 - smoothstep(_LineWidth * 0.5, _LineWidth * 1.5, distToLine);

                // Combine brightness and user intensity
                float alpha = lineMask * _BaseAlpha * _PulseIntensity * _UserIntensity;
                fixed4 col  = _GridColor;
                col.rgb    *= _PulseIntensity;
                col.a       = alpha;

                return col;
            }
            ENDCG
        }
    }
}
