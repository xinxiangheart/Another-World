Shader "AnotherWorld/BoardRingSpin"
{
    // 棋盘中央两圈环：极慢自转，且【只转环带】——环带以外的内容一动不动（2026-09-24）
    //
    // 需求：金环（Board_Sigil，L3）极慢顺时针转；蓝环（Board_Rune，L4）极慢逆时针转；
    //       两者反向同时成立，而中央的六芒星【不能动】。
    //
    // 难点：六芒星与蓝环画在【同一张贴图】Board_Rune.png 上
    //       （实测：六芒星 r 0~252px、蓝环 r 292~336px），整层旋转会把六芒星一起带走。
    //       所以本 shader 不转物体、也不转整层，而是在片元里【按半径开一个环带窗口】，
    //       只让环带内的采样坐标旋转：
    //           r 落在 [_InnerR, _OuterR] 之内 → 采样旋转后的 uv（会转）
    //           r 落在环带之外                → 原样采样（不动）
    //       窗口两侧各留 _Soft 宽的软过渡，避免出现一条硬切边。
    //       于是同一张图里，圈外的六芒星 / 中心菱形静止，圈内的环照转。
    //
    // 半径口径：以贴图【横向半宽】为 1 的归一化单位（u 单位）：
    //       r = length( (uv - _Center) * float2(1, _Aspect) )，_Aspect = 贴图高/宽。
    //       换算：像素半径 = r * 贴图宽(2048)，即 u = 像素半径 / 2048。
    //       本工程（2048x1152）：0.1318 = 270px、0.1929 = 395px。
    //
    // 转向：_Speed 单位是【度/秒】。正数 = 屏幕上顺时针，负数 = 屏幕上逆时针。
    //       「屏幕上的方向」已按实机对过：棋盘贴图在实机里【没有左右镜像】——
    //       用 Board_Motes 的 192 颗微尘与实机截图做对位，未镜像匹配 z=16.0、
    //       镜像假设只有 z=0.3；拟合出的缩放 s=0.776 与相机几何算出的 0.7785 一致。
    //       故 uv 里的顺时针 = 屏幕上的顺时针。
    //       速度参考：1 度/秒 → 转一圈 360 秒（6 分钟）。
    //
    // 参数（材质 Inspector 里可实时调，运行时也生效）：
    //   _Speed    度/秒，正=顺时针、负=逆时针
    //   _InnerR   环带内边界（u 单位）
    //   _OuterR   环带外边界（u 单位）
    //   _Soft     两侧软过渡宽度（u 单位）—— 太小会出现硬切边
    //   _CenterX / _CenterY  旋转中心（0.5,0.5 = 贴图正中）
    //   _Aspect   贴图 高/宽（2048x1152 → 0.5625）；不填会把圆转成椭圆
    //   _Color / _SrcBlend / _DstBlend 与 BoardLayer 一致，保证换 shader 后颜色与混合不变
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color ("Tint RGB x Opacity A", Color) = (1,1,1,1)
        _SrcBlend ("Src Blend (5=SrcAlpha)", Float) = 5
        _DstBlend ("Dst Blend (10=1-SrcAlpha, 1=One Additive)", Float) = 10

        _Speed ("Spin Degrees Per Second (+ = clockwise)", Range(-30, 30)) = 1
        _InnerR ("Band Inner Radius (u)", Range(0, 0.7)) = 0.13
        _OuterR ("Band Outer Radius (u)", Range(0, 0.7)) = 0.2
        _Soft ("Band Edge Softness (u)", Range(0.0005, 0.05)) = 0.005
        _CenterX ("Centre X (u)", Range(0, 1)) = 0.5
        _CenterY ("Centre Y (v)", Range(0, 1)) = 0.5
        _Aspect ("Texture Height / Width", Range(0.1, 4)) = 0.5625
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
            float _Speed;
            float _InnerR;
            float _OuterR;
            float _Soft;
            float _CenterX;
            float _CenterY;
            float _Aspect;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 ctr = float2(_CenterX, _CenterY);
                float2 d   = i.texcoord - ctr;
                float2 p   = float2(d.x, d.y * _Aspect);   // 校正成等比，半径才是圆而不是椭圆
                float  r   = length(p);

                // 环带窗口：带内 1、带外 0，两侧各 _Soft 宽软过渡
                float sm  = max(0.0001, _Soft);
                float win = saturate((r - _InnerR) / sm) * saturate((_OuterR - r) / sm);

                // 采样坐标转 +a → 贴图内容在屏幕上随之顺时针转（_Speed > 0）
                float a  = radians(_Speed * _Time.y);
                float sn, cs;
                sincos(a, sn, cs);
                float2 q   = float2(cs * p.x - sn * p.y, sn * p.x + cs * p.y);
                float2 uv2 = float2(q.x, q.y / _Aspect) + ctr;

                fixed4 colStatic = tex2D(_MainTex, i.texcoord);
                fixed4 colSpin   = tex2D(_MainTex, uv2);
                return lerp(colStatic, colSpin, win) * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
