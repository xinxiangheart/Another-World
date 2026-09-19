using UnityEngine;

public class GoToGameScene : MonoBehaviour
{
    /// <summary>开始游戏：黑条扫屏切到大厅
    /// （黑条推进 / 全黑等待 / 渐入都由 SceneTransition 负责）。</summary>
    public void StartGame()
    {
        SceneTransition.LoadScene("Lobby");
    }
}
