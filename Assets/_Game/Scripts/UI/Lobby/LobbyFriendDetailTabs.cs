using UnityEngine;

/// <summary>好友详情左侧四个 tab 的控制器：选中一格 → 换板贴图 + 只开对应那块内容。</summary>
/// <remarks>2026-09-27：用户「……初始进入位于好友列表格子……四个格子每个格子的内容都将不一样」。
///
/// 四格自上而下 = 好友列表 / 添加好友 / 申请列表 / 黑名单；右侧那个独立大格子里放着四块内容，
/// 同一时刻只有当前选中那格的内容可见。初始格由 <see cref="startIndex"/> 定（0 = 好友列表），
/// **每次面板打开都会回到它** —— 本组件挂在面板根上，面板 closeOnStart 关掉时整棵不激活，
/// 再 Open 就会走一遍 OnEnable。
/// </remarks>
public class LobbyFriendDetailTabs : MonoBehaviour
{
    [Header("四个 tab（自上而下：好友列表 / 添加好友 / 申请列表 / 黑名单）")]
    public LobbyFriendTab[] tabs;

    [Header("右侧大格子里的四块内容（与 tabs 一一对应）")]
    public GameObject[] contents;

    [Header("初始进入哪一格（0 = 好友列表）")]
    public int startIndex;

    /// <summary>当前选中的是第几格。</summary>
    public int Current { get; private set; }

    void OnEnable()
    {
        if (!Application.isPlaying) return;    // 编辑态由构建脚本摆好初始态
        Select(startIndex);
    }

    /// <summary>切到第 index 格（越界即回到第 0 格）。</summary>
    public void Select(int index)
    {
        if (tabs == null || tabs.Length == 0) return;
        if (index < 0 || index >= tabs.Length) index = 0;
        Current = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == null) continue;
            tabs[i].SetOn(i == index);
        }

        if (contents == null) return;
        for (int i = 0; i < contents.Length; i++)
        {
            if (contents[i] == null) continue;
            contents[i].SetActive(i == index);
        }
    }
}