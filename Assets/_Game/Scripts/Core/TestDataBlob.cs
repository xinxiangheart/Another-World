// 测试用——模拟大体积资源，验证多线程下载功能。发正式版时删除此文件。
using UnityEngine;

public static class TestDataBlob
{
    public static readonly byte[] PaddingA = new byte[10 * 1024 * 1024];
    public static readonly byte[] PaddingB = new byte[10 * 1024 * 1024];
    public static readonly byte[] PaddingC = new byte[10 * 1024 * 1024];
    public static readonly byte[] PaddingD = new byte[10 * 1024 * 1024];

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
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
