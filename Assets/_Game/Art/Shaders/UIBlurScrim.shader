Shader "AnotherWorld/UIScrimBlur"
{
    // 大厅「卡牌详情」底下那层模糊幕：抓当前屏幕 → 9x9 高斯模糊 → 再按 _Tint 压暗。
    // ⚠ 2026-09-29：原来是「等权盒式（5x5，权重全是 1）」—— 平顶核 + tap 间距一放，
    //   缩到屏幕上就是用户说的「类马赛克」。现改高斯（权重往外递减），没有方块感。
    // ⚠ 内置管线（Built-in RP）+ Canvas 是 Screen Space - Overlay（Lobby.unity），GrabPass 抓的是
    //    「本件之前已经画完的东西」—— 也就是卡牌总览那一屏（HUD 层是 Canvas 最后一个子物体，
    //    还排在它后面，所以抓不到、也不会被模糊，正好符合「HUD 永远压在面板之上」）。
    //    抓不到就退成一整块 _Tint 色（见 C# 侧：材质建不起来就不挂它，改纯色 Image）。
    Properties
    {
        // ⚠ `_MainTex` 必须有：`Image` / `ScrollRect` 每帧都会去读材质的 `_MainTex`（`CanvasRenderer` 把图集/白图绑在这个名字上），
        //   没声明就一直刷「doesn't have a texture property '_MainTex'」。留着它、并真的采一下（没 sprite 时它就是 1x1 白图 ⇒ 视觉不变）。
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Tint  ("Tint (rgb 用来压暗 / a = 压暗强度)", Color) = (0.027, 0.043, 0.070, 0.62)
        // _Blur = **相邻两次采样之间隔几个屏幕 texel**，不是「模糊半径」—— 核固定 9x9 高斯（见 frag）。
        //   1.0 = 逐 texel 采（最细、开销最大）；2.5 = 隔 2.5 个采一个（默认）；再大就是更大尺度的糊。
        _Blur  ("Blur (相邻 tap 的间隔 / 屏幕 texel)", Range(0.5, 6)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        GrabPass { "_AwScrimGrab" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AwScrimGrab;
            float4 _AwScrimGrab_TexelSize;
            fixed4 _Tint;
            float  _Blur;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv      : TEXCOORD1;
                fixed4 color   : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float2 step = _AwScrimGrab_TexelSize.xy * _Blur;

                // 9x9 **高斯**核（sigma = 2.2 tap；1/(2*sigma^2) = 0.10331）。
                // ⚠ 别退回等权盒式：等权核是平顶（中间一整块同亮度）+ 方格间距 = 类马赛克。
                //   高斯的权往外递减，才对。x/y 都是常量，编译器会把 exp 折成常数。
                fixed4 sum = 0;
                float wsum = 0;
                [unroll] for (int y = -4; y <= 4; y++)
                {
                    [unroll] for (int x = -4; x <= 4; x++)
                    {
                        float w = exp(-(float)(x * x + y * y) * 0.10331);
                        sum  += tex2D(_AwScrimGrab, uv + float2(x, y) * step) * w;
                        wsum += w;
                    }
                }
                fixed4 c = sum / wsum;

                // 压暗：把抓来的画面往 _Tint.rgb 上拉 _Tint.a —— 保持「透明幕」的手感，不是盖一张纯色板
                c.rgb = lerp(c.rgb, _Tint.rgb, _Tint.a);
                c.a = 1;
                return c * i.color * tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }

    Fallback Off
}
