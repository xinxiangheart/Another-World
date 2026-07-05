using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Game intro popup — scrollable document with editable text sections + images.
/// Data saved to persistentDataPath/intro_data.json.
/// </summary>
public class GameIntroPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public Transform sectionContainer;
    public GameObject sectionPrefab;        // must have GameIntroSection component
    public Button addSectionButton;
    public Button saveButton;
    public Button closeButton;

    private List<GameIntroSection> _sections = new List<GameIntroSection>();
    private string _savePath;

    void Awake()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "intro_data.json");
        if (addSectionButton != null) addSectionButton.onClick.AddListener(() => AddSection());
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);
        EnsureSectionLayout();
    }

    void EnsureSectionLayout()
    {
        if (sectionContainer == null) return;
        // Force vertical stacking so instantiated sections don't overlap
        var vlg = sectionContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = sectionContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 10;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var fitter = sectionContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = sectionContainer.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Load();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void AddSection(string text = "", string imagePath = "")
    {
        if (sectionPrefab == null || sectionContainer == null) return;

        GameObject go = Instantiate(sectionPrefab, sectionContainer);
        var section = go.GetComponent<GameIntroSection>();
        if (section == null) { Destroy(go); return; }

        section.Init(text, imagePath);
        _sections.Add(section);
    }

    void ClearSections()
    {
        foreach (var s in _sections)
            if (s != null) Destroy(s.gameObject);
        _sections.Clear();
    }

    // ===== Persistence =====

    [System.Serializable]
    class SectionData
    {
        public string text;
        public string imagePath; // relative path under persistentDataPath/images/
    }

    [System.Serializable]
    class IntroData
    {
        public List<SectionData> sections = new List<SectionData>();
    }

    public void Save()
    {
        var data = new IntroData();
        foreach (var s in _sections)
        {
            if (s == null) continue;
            string img = s.GetImagePath();
            data.sections.Add(new SectionData
            {
                text = s.GetText(),
                imagePath = img
            });
        }
        string json = JsonUtility.ToJson(data, true);
        Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
        File.WriteAllText(_savePath, json);
        Debug.Log($"[GameIntroPanel] Saved to {_savePath}");
    }

    void Load()
    {
        ClearSections();
        if (!File.Exists(_savePath)) return;

        string json = File.ReadAllText(_savePath);
        var data = JsonUtility.FromJson<IntroData>(json);
        if (data?.sections == null) return;

        foreach (var sd in data.sections)
            AddSection(sd.text ?? "", sd.imagePath ?? "");
    }
}
