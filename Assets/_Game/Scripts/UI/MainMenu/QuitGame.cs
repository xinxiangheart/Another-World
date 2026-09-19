using UnityEngine;

/// <summary>「退出游戏」按钮：编辑器里停播放，打包后走 Application.Quit。</summary>
public class QuitGame : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
