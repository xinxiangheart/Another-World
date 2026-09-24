Shader "AnotherWorld/BoardMotesTwinkle"
{
    // 棋盘微尘层：运行时闪烁（2026-09-23）
    // 目标：画面上的微尘不再「220 颗一起常亮」，而是像星星那样 ——
    //       同一时间只有一部分亮着，每颗按自己的节拍慢慢明灭。
    //
    // 做法：贴图（Board_Motes.png）里每颗微尘的位置、大小、基础透明度一律不动，
    //       只把它的 alpha 乘一个随时间变化的系数 k：
    //         k = 0  → 这一瞬不显示这颗（＝「不是所有微尘都会在屏幕上显示」）
    //         k = 1  → 到了这颗最亮的一刻，与静态贴图同亮
    //
    // 相位：把画面切成 _Density×_Density 的格子（默认约 79px 一格，与微尘平均间距同量级），
    //       每格从哈希取一个【均匀分布】的随机相位。微尘落在哪格就用哪格的相位 ——
    //       于是每颗各闪各的，画面不会整体呼吸，也不会整体漂移。
    //       注意必须用「每格一个常量哈希」，不能用插值噪声：插值后的值集中在 0.5 附近，
    //       相位会挤在一起，220 颗就变成整片同步呼吸了（实测 min 17 / max 84）。
    //
    // 阈值由「同一时间可见比例」反推：单正弦下 可见比例 = (1 - asin(t)/90°)/2 → t = cos(pi * visible)。
    // 输出 = tex2D(_MainTex) * _Color * k；混合模式照旧从材质读 _SrcBlend/_DstBlend。
    //
    // 参数（材质 Inspector 可实时调，运行时也生效）：
    //   _Cycle   闪烁周期（秒）—— 越大越慢
    //   _Visible 同一时间可见比例 —— 0.3 表示任意时刻约三成的微尘亮着（220 颗里约 66 颗）
    //   _Density 相位格子密度 —— 越大越接近「每颗各闪各的」；太小会几颗一组同步
    //   _Soft    明暗过渡柔和度 —— 0=硬淡入淡出，1=整个半周期都在渐变
    //   _Second / _SecondMix 第二层闪烁（同期不同相位）：给每颗不同的明暗幅度。
    //            _SecondMix = 0 时完全不启用（默认）；>0 时可见比例会略低于 _Visible
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color ("Tint RGB x Opacity A", Color) = (1,1,1,1)
        _SrcBlend ("Src Blend (5=SrcAlpha)", Float) = 5
        _DstBlend ("Dst Blend (10=1-SrcAlpha, 1=One Additive)", Float) = 10
        _Cycle ("Blink Cycle Seconds", Range(1, 40)) = 8
        _Visible ("Visible Fraction", Range(0.02, 1)) = 0.3
        _Density ("Blink Cell Density", Range(4, 80)) = 26
        _Soft ("Blink Softness", Range(0.02, 1)) = 0.75
        _Second ("Second Layer Period x", Range(0.2, 4)) = 1
        _SecondMix ("Second Layer Mix", Range(0, 1)) = 0
        _Aspect ("Canvas Height/Width", Range(0.1, 3)) = 0.5625
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
            float _Cycle;
            float _Visible;
            float _Density;
            float _Soft;
            float _Second;
            float _SecondMix;
            float _Aspect;

            // 无正弦哈希（Dave Hoskins）：够随机，也没有 sin 的精度条纹
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // 每格一个随机相位（格内恒定，不插值）
                float2 np = float2(uv.x, uv.y * _Aspect) * max(1.0, _Density);
                float ph1 = hash12(floor(np)) * 6.2831853;

                float w = 6.2831853 / max(0.5, _Cycle);
                float s1 = sin(_Time.y * w + ph1);

                float ph2 = hash12(floor(np * 1.61 + float2(31.0, 17.0))) * 6.2831853;
                float s2 = sin(_Time.y * w * max(0.05, _Second) + ph2);

                float s = lerp(s1, s2, saturate(_SecondMix));

                // 阈值由「同一时间可见比例」反推（单正弦精确）
                float lo = cos(3.14159265 * saturate(_Visible));
                float hi = lo + max(0.02, min(1.0, _Soft)) * (1.0 - lo);
                float k = smoothstep(lo, hi, s);

                fixed4 c = tex2D(_MainTex, uv);
                c.rgb *= _Color.rgb;
                c.a *= _Color.a * k;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}