Shader "AnotherWorld/CardComposite"
{
    // =========================================================================
    // CardComposite — 三层贴图合成着色器（动态方案B）
    // =========================================================================
    //
    // 在 GPU 上把底图 → 边框 → 卡面按顺序混合成一张最终画面。
    // 边框和卡面都带透明通道，Alpha=0 的区域透出下层。
    //
    // 用法：
    //   material.SetTexture("_BgTex", bgTexture);
    //   material.SetTexture("_BorderTex", borderTexture);
    //   material.SetTexture("_ArtTex", artTexture);
    //
    // 不需要每张卡独立的 prefab——全部卡共用同一个材质实例。
    // =========================================================================

    Properties
    {
        _BgTex    ("底图 (Background)", 2D) = "white" {}
        _BorderTex("边框 (Border)",      2D) = "white" {}
        _ArtTex   ("卡面 (Card Art)",    2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _BgTex;
            sampler2D _BorderTex;
            sampler2D _ArtTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1) 底图 —— 不透明底色
                fixed4 bg = tex2D(_BgTex, i.uv);

                // 2) 边框 —— 叠加（Alpha 混合）
                fixed4 border = tex2D(_BorderTex, i.uv);
                fixed3 c = lerp(bg.rgb, border.rgb, border.a);

                // 3) 卡面 —— 叠加
                fixed4 art = tex2D(_ArtTex, i.uv);
                c = lerp(c, art.rgb, art.a);

                return fixed4(c, 1);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
