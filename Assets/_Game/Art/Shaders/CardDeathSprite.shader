// ============================================================================
// CardDeathSprite — 普通退场"残影"专用：自下而上 灰→黑 渐变 与 淡出 重叠。
// 只用于克隆的退场残影 SpriteRenderer；不触碰真卡/共享材质。
//   _Death 0→1 : 自下而上的"压暗"波前（n < _Death 的底部区域变 _TintColor）
//   _Fade  0→1 : 自下而上的"淡出"波前（落后 _Death → 未完全变黑就开始消失）
//   _MinY/_MaxY: 整卡底部/顶部世界 Y，归一为 0(下)→1(上)
// ============================================================================
Shader "Custom/CardDeathSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Death ("压暗波前 0-1", Range(0, 1)) = 0
        _Fade  ("淡出波前 0-1", Range(0, 1)) = 0
        _TintColor ("压暗目标色(驱动端 灰→黑)", Color) = (0.45, 0.45, 0.45, 1)
        _MinY ("整卡底部世界Y", Float) = 0
        _MaxY ("整卡顶部世界Y", Float) = 1
        _Soft ("波前过渡宽度(归一化Y)", Range(0.001, 0.4)) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off
        Fog { Mode Off }

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
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                float worldY : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Death;
            float _Fade;
            fixed4 _TintColor;
            float _MinY;
            float _MaxY;
            float _Soft;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                float w = mul(unity_ObjectToWorld, v.vertex).y;
                float range = _MaxY - _MinY;
                o.worldY = range > 0.0001 ? (w - _MinY) / range : 0.0;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
                col.a = col.a * _TintColor.a;

                float n = saturate(i.worldY);   // 0=底部 1=顶部
                float halfSoft = _Soft * 0.5;

                // 压暗波：n 低于 _Death 的底部区域 → 趋近 _TintColor（驱动端 灰→黑）
                float darkLow = _Death - halfSoft;
                float darkHigh = _Death + halfSoft;
                float darkK = 1.0 - smoothstep(darkLow, darkHigh, n);
                col.rgb = lerp(col.rgb, _TintColor.rgb, saturate(darkK));

                // 淡出波：n 低于 _Fade 的底部区域 → 透明度消失（_Fade 落后 _Death → 重叠）
                float fadeLow = _Fade - halfSoft;
                float fadeHigh = _Fade + halfSoft;
                float fadeK = 1.0 - smoothstep(fadeLow, fadeHigh, n);
                col.a = col.a * saturate(1.0 - fadeK);

                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
