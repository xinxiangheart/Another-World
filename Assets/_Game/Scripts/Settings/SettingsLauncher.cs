using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>接线标记，避免重复挂监听。</summary>
public class SettingsLauncherTag : MonoBehaviour { }

/// <summary>
/// 把场景里「已经存在」的设置按钮接到 SettingsPanel —— 不需要改场景文件、不需要手动拖引用。
///
/// 规则
/// · 名字为 Setting / Settings / 设置 的按钮，且没有 SettingsButton 组件
///   → 点击直接开关全局设置面板（Lobby 的预留按钮走这条）
/// · 该按钮上已有 SettingsButton（Game 场景的「设置 / 投降」面板）
///   → 往它的面板底部注入一个「游戏设置」按钮，两者并存、互不遮挡
///
/// 每次加载场景都会重新扫一遍，因此 Lobby / Game / 以后新增的场景都自动生效。
/// </summary>
public static class SettingsLauncher
{
    static readonly string[] TargetNames =
    {
        "Setting", "setting", "Settings", "settings",
        "SettingButton", "SettingsButton", "设置", "设置按钮", "系统设置"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Hook(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hook(scene);
    }

    static void Hook(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            Walk(roots[i].transform);
        }
    }

    static void Walk(Transform t)
    {
        if (t == null) return;

        if (IsTargetName(t.name))
        {
            var tag = t.GetComponent<SettingsLauncherTag>();
            if (tag == null)
            {
                var btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    t.gameObject.AddComponent<SettingsLauncherTag>();

                    var sb = t.GetComponent<SettingsButton>();
                    if (sb != null)
                    {
                        // Game 场景：保留它原本的「投降」面板，在面板里加一条入口
                        SettingsPanel.AttachButtonTo(sb.settingsPanel);
                    }
                    else
                    {
                        btn.onClick.AddListener(SettingsPanel.Toggle);
                    }
                }
            }
        }

        for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i));
    }

    static bool IsTargetName(string name)
    {
        for (int i = 0; i < TargetNames.Length; i++)
            if (string.Equals(name, TargetNames[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}