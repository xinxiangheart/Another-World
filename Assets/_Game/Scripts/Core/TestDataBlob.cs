// 测试用——模拟大体积资源，验证多线程下载功能。发正式版时删除此文件。
// CI 构建时会在 zip 中加入 200MB 测试数据。
using UnityEngine;

public static class TestDataBlob
{
    // 预分配大数组撑大 IL 代码段
    public static readonly byte[] PaddingA = new byte[10 * 1024 * 1024]; // 10MB
    public static readonly byte[] PaddingB = new byte[10 * 1024 * 1024]; // 10MB
    public static readonly byte[] PaddingC = new byte[10 * 1024 * 1024]; // 10MB
    public static readonly byte[] PaddingD = new byte[10 * 1024 * 1024]; // 10MB

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        // 填充随机数据防止被优化掉
        for (int i = 0; i < PaddingA.Length; i += 4096)
        {
            PaddingA[i] = 0xFF;
            PaddingB[i] = 0xAA;
            PaddingC[i] = 0x55;
            PaddingD[i] = 0xCC;
        }
        Debug.Log("[TestDataBlob] Padding arrays initialized (" + (PaddingA.Length + PaddingB.Length + PaddingC.Length + PaddingD.Length) / 1048576 + " MB)");
    }
}
