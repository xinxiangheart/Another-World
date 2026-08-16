using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键把 3D 棋盘模型（Back.fbx + Board.mat）放进 Game 场景。
/// 卡牌 3D 世界位置在 z=-5.5，棋盘放在卡牌后方（z=-4），正面朝向摄像机。
/// </summary>
public class Board3DSetup
{
    const string FBX_PATH = "Assets/_Game/Art/Models/Board/Back.fbx";
    const string MAT_PATH = "Assets/_Game/Art/Materials/Board.mat";

    [MenuItem("Tools/放置 3D 棋盘")]
    public static void PlaceBoard()
    {
        if (GameObject.Find("Board3D") != null)
        {
            EditorUtility.DisplayDialog("3D 棋盘", "场景中已存在 Board3D，请先「Tools/移除 3D 棋盘」。", "确定");
            return;
        }

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (fbx == null) { Debug.LogError("[Board3DSetup] 找不到模型: " + FBX_PATH); return; }
        if (mat == null) { Debug.LogError("[Board3DSetup] 找不到材质: " + MAT_PATH); return; }

        var board = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        board.name = "Board3D";

        var rend = board.GetComponentInChildren<MeshRenderer>();
        if (rend != null) rend.sharedMaterial = mat;

        // 定位：棋盘中心对齐卡牌区域中心 (0, ~1)。相机 z=-16.22 看向 +Z，越负越近。
        // 棋盘厚 1.6，朝相机面(正面转 180° 后朝 -Z)在 z = 中心 - 0.8。
        // 要让贴图面在卡牌(-6)后面(更远)，贴图面 z=-5.5 → 中心 = -5.5 + 0.8 = -4.7。
        board.transform.position = new Vector3(0f, 1f, -4.7f);
        // 卡牌 3D 实例化均用 Quaternion.Euler(0,180,0) 让正面朝向摄像机(-Z)。
        // 棋盘 FBX 正面为 +Z，同样旋转 180° 朝向摄像机。
        board.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        EditorUtility.SetDirty(board);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Board3DSetup] 3D 棋盘已放置到 (0, 1, -4)。若方向/位置/尺寸不对，先「Tools/移除 3D 棋盘」再调整。");
    }

    [MenuItem("Tools/移除 3D 棋盘")]
    public static void RemoveBoard()
    {
        var go = GameObject.Find("Board3D");
        if (go == null) { Debug.Log("[Board3DSetup] 场景中没有 Board3D。"); return; }
        Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Board3DSetup] 已移除 Board3D。");
    }
}
