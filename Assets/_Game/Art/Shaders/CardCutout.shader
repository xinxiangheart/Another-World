Shader "AnotherWorld/CardCutout"
{
    // =========================================================================
    // CardCutout — 卡牌专用 alpha-test 着色器（解决透视下卡牌被槽位遮挡）
    // =========================================================================
    //
    // 卡牌贴图（Card000_Back/Front 等）是硬边透明：主体 alpha=1，圆角 alpha=0，
    // 无半透明渐变。因此用 AlphaTest(clip) 替代 AlphaBlend：
    //   - 圆角透明像素 clip 丢弃 → 圆角正确
    //   - 主体写深度(ZWrite On) → 后面的槽位 UI(Transparent队列)读深度被正确遮挡
    //
    // 队列 AlphaTest(2450)：在 Geometry(2000) 之后、Transparent(3000) 之前，
    // 保证卡牌先于槽位 UI 渲染，且写深度。
    // =========================================================================

    Properties
    {
        _MainTex ("卡面 (Card Texture)", 2D) = "white" {}
        _Color  ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            ZWrite On
            Cull Back
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv     : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                clip(col.a - 0.5);   // 圆角透明区域丢弃，主体写深度
                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
