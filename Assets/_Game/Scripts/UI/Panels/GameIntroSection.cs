using UnityEngine;
using TMPro;

/// <summary>
/// 游戏介绍内容块 — 不做任何运行时处理，纯粹在 Editor 里像 Word 一样手动编排。
/// 每个 IntroSection 预制体自带 Text 子和 Image 子对象，你在 Inspector 里直接填内容。
/// </summary>
public class GameIntroSection : MonoBehaviour
{
    [Header("字体（拖给下面 Text 子和 Image 子对象用，不会自动设置）")]
    [Tooltip("仅作参考存储，实际需在子对象 TMP_Text 组件上单独指定")]
    public TMP_FontAsset fontAsset;
}
