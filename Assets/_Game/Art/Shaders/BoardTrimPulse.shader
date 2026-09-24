Shader "AnotherWorld/BoardTrimPulse"
{
    // 棋盘金色贴边层：缓慢明灭（2026-09-24）
    //
    // 目标：板上那两圈金线（内圈跑道框 Board_Surface + 外框角饰 Board_Ornament）不再常亮，
    //       而是慢慢亮一下、再退回暗处 —— 暗的时间比亮的多。
    //
    // 做法：贴图一个字不动，只把整层的 alpha 乘一个随时间走的系数 k：
    //         k = _Floor  → 这一轮处在「暗」里的常态（内圈 0 = 直接看不见；外框 0.25 = 还剩四分之一）
    //         k = _Peak   → 一轮里最亮的一刻（默认 1.0 = 与原来一样亮；调到 0.85 就是「削一点」）
    //       一轮里只有 _Bright 这一段落在亮窗内（默认 0.4 = 四成时间亮、六成时间暗）。
    //
    // 亮窗形状（2026-09-24 改：上升与下降分开指定，不再是对称驼峰）
    //         t=0 ──_Rise──► 峰 ──_Fall──► t=_Bright ──►（暗）
    //         _Rise 是「亮起来的速度」：调大就亮得慢；_Fall 是「退下去的速度」。
    //         两者之和若超过 _Bright，会自动等比压进亮窗。
    //
    // 与微尘闪烁的区别：微尘是「每颗各闪各的」，所以要相位哈希；
    //       贴边是【整层一起明灭】—— 一圈边框分段乱闪会读成故障，不是呼吸。
    //
    // 参数（材质 Inspector 里可实时调，运行时也生效）：
    //   _Period   一轮多少秒（默认 6）—— 越大越慢
    //   _Bright   一轮里「亮」的占比（默认 0.4）—— 越小越难得一见
    //   _Rise     亮起来用掉一轮的多少（默认 0.26）—— 越大亮得越慢
    //   _Fall     退下去用掉一轮的多少（默认 0.14）
    //   _Floor    暗态明度倍数（默认 0）—— 0 = 暗的时候整层看不见
    //   _Peak     峰值明度倍数（默认 0.85）
    //   _Phase    起始相位 0..1（同一材质多处复用时错开，单层用不上）
    //   _PeakRGB  峰值时额外提亮的 RGB 倍数（默认 1.0 = 不提亮，只做透明度明灭）
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color ("Tint RGB x Opacity A", Color) = (1,1,1,1)
        _SrcBlend ("Src Blend (5=SrcAlpha)", Float) = 5
        _DstBlend ("Dst Blend (10=1-SrcAlpha, 1=One Additive)", Float) = 10
        _Period ("Pulse Period Seconds", Range(0.5, 60)) = 6
        _Bright ("Bright Fraction of Period", Range(0.02, 1)) = 0.4
        _Rise ("Rise Fraction of Period", Range(0.01, 1)) = 0.26
        _Fall ("Fall Fraction of Period", Range(0.01, 1)) = 0.14
        _Floor ("Dim Level", Range(0, 1)) = 0
        _Peak ("Peak Level", Range(0.05, 2)) = 0.85
        _Phase ("Phase Offset", Range(0, 1)) = 0
        _PeakRGB ("Extra RGB at Peak", Range(1, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        Lighting Off
        ZWrite Off
        Cull Back
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Period;
            float _Bright;
            float _Rise;
            float _Fall;
            float _Floor;
            float _Peak;
            float _Phase;
            float _PeakRGB;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 一轮里的进度 0..1
                float t  = frac(_Time.y / max(0.1, _Period) + _Phase);
                float br = saturate(_Bright);

                float ri = min(max(_Rise, 0.002), br);
                float fa = min(max(_Fall, 0.002), br);
                // 上升 + 下降 若超过亮窗，等比压进亮窗（保证峰一定落在窗内）
                float sum = ri + fa;
                if (sum > br) { float s = br / sum; ri *= s; fa *= s; }

                float rise = smoothstep(0.0, ri, t);
                float fall = 1.0 - smoothstep(max(ri, br - fa), br, t);
                float w = rise * fall;                       // 0 = 暗，1 = 峰

                float k = _Floor + (_Peak - _Floor) * w;

                fixed4 c = tex2D(_MainTex, i.texcoord);
                c.rgb *= _Color.rgb * (1.0 + max(0.0, _PeakRGB - 1.0) * w);
                c.a   *= _Color.a * k;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
