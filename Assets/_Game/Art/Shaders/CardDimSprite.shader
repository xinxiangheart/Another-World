Shader "AnotherWorld/CardDimSprite"
{
    // =========================================================================
    // CardDimSprite — 卡牌"非己方回合压暗"专用 UI 着色器
    // =========================================================================
    //
    // 为什么不用"盖一层半透明灰图"：
    //   灰图是 alpha 混合 —— out = lerp(卡面, 灰, a)。它同时干了两件坏事：
    //     ① 把黑位抬到灰（灰×a），卡面暗部整片发灰 → 读起来像蒙了张阴影纸；
    //     ② 把所有对比度乘 0.72，明度阶梯被压平 → 细节糊掉。
    //   本着色器改为对**卡面本身**做两步处理（顺序重要）：
    //     第一步 去饱和：rgb 向自身亮度靠拢，只降彩度，明度阶梯一点不动；
    //     第二步 压亮度：整体乘 _DimBrightness —— 黑仍是黑，对比度保留；
    //     第三步 叠环境色：加一点点 _DimTint×_DimLift 的冷调，给出"退到暗处/在阴影里"
    //            的观感（这一步才是"阴影感"的来源，而不是靠灰纸盖）。
    //   _DimAmount 为 0 时结果与 UI/Default 逐像素一致，所以能逐帧插值做淡入淡出。
    //
    // 用法（见 CardView.SetGroupDim）：
    //   每张卡一份材质实例，淡入淡出期间只改 _DimAmount（0→1），其余参数不动。
    // =========================================================================
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dim)]
        _DimAmount ("压暗强度 0-1（驱动端插值）", Range(0, 1)) = 0
        _DimSaturation ("压暗后保留的彩度", Range(0, 1)) = 0.12
        _DimBrightness ("压暗后亮度", Range(0, 1)) = 0.84
        _DimTint ("压暗环境色", Color) = (0.78, 0.82, 0.92, 1)
        _DimLift ("压暗环境色落地量", Range(0, 0.5)) = 0.04
        _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)

        // ── uGUI 标准（Mask / RectMask2D / 遮罩裁切沿用 UI/Default 的定义）──
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "DEFAULT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float  _DimAmount;
            float  _DimSaturation;
            float  _DimBrightness;
            fixed4 _DimTint;
            float  _DimLift;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // ① 去饱和：向自身亮度靠拢（只动彩度，不动明度结构）
                half lum = dot(color.rgb, half3(0.299, 0.587, 0.114));
                half3 desat = lerp(lum.xxx, color.rgb, _DimSaturation);
                // ② 压亮度（乘法，黑仍是黑）+ ③ 叠冷调环境色（给"在阴影里"的观感）
                half3 tint = _DimTint.rgb;
                half3 dimmed = min(desat * _DimBrightness + tint * _DimLift, half3(1, 1, 1));
                color.rgb = lerp(color.rgb, dimmed, _DimAmount);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
