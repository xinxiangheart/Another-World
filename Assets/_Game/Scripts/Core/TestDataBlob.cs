// 测试用——模拟大体积资源，验证多线程下载功能。发正式版时删除此文件。
using UnityEngine;

public static class TestDataBlob
{
    public static readonly byte[] PadA = new byte[5 * 1024 * 1024];
    public static readonly byte[] PadB = new byte[5 * 1024 * 1024];

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        for (int i = 0; i < PadA.Length; i += 4096) { PadA[i] = 0xFF; PadB[i] = 0xAA; }
        Debug.Log("[TestDataBlob] Padding: " + (PadA.Length + PadB.Length) / 1048576 + " MB");
    }
}
