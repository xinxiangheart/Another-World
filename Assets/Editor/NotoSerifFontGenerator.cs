using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GenerateFontCharSet
{
    [MenuItem("Tools/Generate Font Char Set")]
    public static void Generate()
    {
        var set = new HashSet<uint>();

        // 1. ASCII 可打印字符
        for (char c = ' '; c <= '~'; c++) set.Add(c);

        // 2. CJK 符号与标点
        for (uint u = 0x3000; u <= 0x303F; u++) set.Add(u); // CJK 符号和标点
        for (uint u = 0xFF00; u <= 0xFFEF; u++) set.Add(u); // 全角字符
        for (uint u = 0x2000; u <= 0x206F; u++) set.Add(u); // 通用标点

        // 3. 扫描项目中所有文件的中文字符
        string root = Application.dataPath;
        string[] exts = { "*.asset", "*.prefab", "*.unity", "*.cs", "*.txt" };
        foreach (string ext in exts)
            foreach (string f in Directory.GetFiles(root, ext, SearchOption.AllDirectories))
            {
                if (f.Contains("/Library/") || f.Contains("/PackageCache/")) continue;
                try
                {
                    string raw = File.ReadAllText(f, Encoding.UTF8);
                    // 直写中文
                    foreach (char c in raw) { ushort u = (ushort)c; if (u >= 0x4E00 && u <= 0x9FFF) set.Add(c); }
                    // \uXXXX 转义
                    foreach (Match m in Regex.Matches(raw, @"\\u([0-9A-Fa-f]{4})"))
                    {
                        uint code = uint.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                        if (code >= 0x4E00 && code <= 0x9FFF) set.Add(code);
                    }
                }
                catch { }
            }

        // 4. TMP 必需的特殊字符
        set.Add(0x5F);   // _
        set.Add(0x2D);   // -
        set.Add(0x25A1); // □

        // 5. 输出
        string outPath = "Assets/_Game/Fonts/GameCharacters.txt";
        string chars = new string(set.OrderBy(u => u).Select(u => (char)u).ToArray());
        File.WriteAllText(outPath, chars, new UTF8Encoding(true));
        AssetDatabase.Refresh();

        int hanzi = set.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        EditorUtility.DisplayDialog("字符集已生成",
            $"共 {set.Count} 字符（{hanzi} 个汉字 + 标点）\n" +
            $"保存至：{outPath}\n\n" +
            $"然后用 Font Asset Creator 生成一次", "OK");
        Debug.LogFormat("<color=green>[FontGen] {0} ({1} chars, {2} hanzi)</color>", outPath, set.Count, hanzi);
    }
}
