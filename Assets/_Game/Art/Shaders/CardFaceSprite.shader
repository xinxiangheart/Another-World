Shader "AnotherWorld/CardFaceSprite"
{
    // 卡面 Sprite 专用着色器 —— 复刻旧卡 CardCutout 方案：
    //   alpha-test(clip) + ZWrite On → 主体写深度，正确遮挡后面的槽位/棋盘 UI（3D 深度遮挡，非硬排 sortingOrder）
    //   透明区域 clip 丢弃、不写深度 → 圆角/镂空正常，不误挡后面
    //   Cull Off → 透视倾斜下双面可见
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite On
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Cutoff;
            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;
                clip(c.a - _Cutoff); // 透明区域不写深度
                return c;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
