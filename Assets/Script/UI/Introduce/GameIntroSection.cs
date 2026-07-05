using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One editable section inside GameIntroPanel — text + optional image.
/// Attached to the section prefab.
/// </summary>
public class GameIntroSection : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField textField;
    public RawImage imageDisplay;
    public Button insertImageButton;
    public Button removeSectionButton;
    public LayoutElement imageLayout;

    private string _imagePath; // persistent storage path
    private Texture2D _loadedTex;
    private const float DEFAULT_IMAGE_HEIGHT = 250f;

    public string GetText() => textField != null ? textField.text : "";
    public string GetImagePath() => _imagePath;

    void Awake()
    {
        if (insertImageButton != null) insertImageButton.onClick.AddListener(PickImage);
        if (removeSectionButton != null) removeSectionButton.onClick.AddListener(Remove);
    }

    public void Init(string text, string imagePath)
    {
        if (textField != null) textField.text = text;

        if (!string.IsNullOrEmpty(imagePath))
        {
            _imagePath = imagePath;
            if (File.Exists(imagePath))
            {
                byte[] bytes = File.ReadAllBytes(imagePath);
                LoadImageFromBytes(bytes);
            }
        }
    }

    void PickImage()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("选择图片", "", "png,jpg,jpeg");
#elif UNITY_STANDALONE_WIN
        string path = WindowsFileDialog.Open("选择图片", "png,jpg,jpeg");
#else
        string path = "";
#endif
        if (string.IsNullOrEmpty(path)) return;

        // Copy to persistent data so it survives
        string dir = Path.Combine(Application.persistentDataPath, "images");
        Directory.CreateDirectory(dir);
        string dest = Path.Combine(dir, Path.GetFileName(path));
        File.Copy(path, dest, true);
        _imagePath = dest;

        byte[] bytes = File.ReadAllBytes(dest);
        LoadImageFromBytes(bytes);
    }

    void LoadImageFromBytes(byte[] bytes)
    {
        _loadedTex = new Texture2D(2, 2);
        _loadedTex.LoadImage(bytes);

        if (imageDisplay != null)
        {
            imageDisplay.texture = _loadedTex;
            imageDisplay.gameObject.SetActive(true);

            // Scale layout to image aspect ratio
            if (imageLayout != null)
                imageLayout.preferredHeight = DEFAULT_IMAGE_HEIGHT;
        }
    }

    public void Remove()
    {
        // Clean up image file
        if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            File.Delete(_imagePath);

        if (_loadedTex != null) Destroy(_loadedTex);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_loadedTex != null) Destroy(_loadedTex);
    }
}
