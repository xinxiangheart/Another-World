Shader "AnotherWorld/BoardLayer"
{
    // 棋盘分层专用着色器（2026-09-23）
    // 背景：8 层原先用内置 Unlit/Transparent，那个着色器【没有 _Color 属性】、【混合模式写死】，
    //       于是材质里存的 _Color / _SrcBlend / _DstBlend 全是死数据 —— 单层亮暗怎么改都不生效。
    // 本 shader：输出 = tex2D(_MainTex) * _Color；rgb 管亮暗、a 管透明度。
    // 混合模式从材质读 _SrcBlend/_DstBlend，默认 5/10 = SrcAlpha/OneMinusSrcAlpha（与旧观感一致）；
    //       想让某层发光（加法），把该材质的 Dst Blend 改成 1（One）即可。
    // ZWrite 一律 Off：8 层几乎共面（z 只差 0.001），谁写深度谁就会把后面的层整片挡掉。
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color ("Tint RGB x Opacity A", Color) = (1,1,1,1)
        _SrcBlend ("Src Blend (5=SrcAlpha)", Float) = 5
        _DstBlend ("Dst Blend (10=1-SrcAlpha, 1=One 加法)", Float) = 10
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

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.texcoord) * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}