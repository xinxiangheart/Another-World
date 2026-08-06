using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum IntroSectionType { TextOnly, TextWithImage, ImageOnly }

/// <summary>
/// 游戏介绍内容块 — 在 Unity Editor 中直接填入文字/图片，运行时只读展示。
/// 挂在 ScrollView Content 下的每个内容项上。
/// </summary>
public class GameIntroSection : MonoBehaviour
{
    [Header("类型")]
    public IntroSectionType type = IntroSectionType.TextOnly;

    [Header("文字（TMP 富文本支持）")]
    [TextArea(3, 30)]
    public string textContent;
    [Tooltip("标题级样式（自动加粗放大）")]
    public bool isHeading;

    [Header("图片")]
    public Sprite sprite;
    [Tooltip("图片最大宽度，等比缩放")]
    public float imageMaxWidth = 600f;
    [Tooltip("图片外边距")]
    public float imagePadding = 10f;

    void Start()
    {
        // Remove any existing children (rebuild clean)
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        switch (type)
        {
            case IntroSectionType.TextOnly:
                BuildText();
                break;
            case IntroSectionType.ImageOnly:
                BuildImage();
                break;
            case IntroSectionType.TextWithImage:
                BuildText();
                BuildImage();
                break;
        }
    }

    void BuildText()
    {
        if (string.IsNullOrEmpty(textContent)) return;

        var go = new GameObject("Text");
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = textContent;
        tmp.fontSize = isHeading ? 28 : 20;
        tmp.fontStyle = isHeading ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = isHeading ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.2f, 0.2f, 0.2f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = true;

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.minHeight = 30;
    }

    void BuildImage()
    {
        if (sprite == null) return;

        var go = new GameObject("Image");
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = Color.white;

        float ratio = (float)sprite.rect.height / sprite.rect.width;
        float width = Mathf.Min(imageMaxWidth, sprite.rect.width);
        float height = width * ratio;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height + imagePadding * 2;
        le.minWidth = 100;
        le.minHeight = 50;
    }
}
