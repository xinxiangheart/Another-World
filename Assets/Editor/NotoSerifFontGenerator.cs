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

        // ASCII
        for (char c = ' '; c <= '~'; c++) set.Add(c);

        // 标点
        AddString(set, "　、。，！？；：“”‘’（）【】《》「」『』—…·×÷←→↑↓☆★○●◎◇◆□■△▲▽▼※");

        // ====== 扫描 CardData 资产 ======
        string[] cardGuids = AssetDatabase.FindAssets("t:CardData");
        foreach (string guid in cardGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // 方法1：直接加载 ScriptableObject 读取已解码的字符串（最可靠）
            CardData cd = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (cd != null)
            {
                AddString(set, cd.cardName);
                AddString(set, cd.prefix);
                AddString(set, cd.effect);
                AddString(set, cd.traits);
                AddString(set, cd.revengeEffect);
                AddString(set, cd.counterEffect);
                AddString(set, cd.counterTriggerCondition);
            }

            // 方法2：兜底——解析 YAML 源文件中 \uXXXX 转义
            try
            {
                string raw = File.ReadAllText(path, Encoding.UTF8);
                // 匹配 \uXXXX
                foreach (Match m in Regex.Matches(raw, @"\\u([0-9A-Fa-f]{4})"))
                {
                    uint code = uint.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    if (code > 127) set.Add(code);
                }
            }
            catch { }
        }

        // ChosenOneData
        foreach (string guid in AssetDatabase.FindAssets("t:Object", new[] { "Assets/_Game/Resources/ChosenOneData" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData cd = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (cd != null)
            {
                AddString(set, cd.cardName);
                AddString(set, cd.prefix);
                AddString(set, cd.effect);
                AddString(set, cd.traits);
                AddString(set, cd.revengeEffect);
            }
            // 兜底 YAML
            try
            {
                string raw = File.ReadAllText(path, Encoding.UTF8);
                foreach (Match m in Regex.Matches(raw, @"\\u([0-9A-Fa-f]{4})"))
                {
                    uint code = uint.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    if (code > 127) set.Add(code);
                }
            }
            catch { }
        }

        // .cs 脚本中的直写中文
        string dir = Application.dataPath + "/_Game/Scripts";
        if (Directory.Exists(dir))
            foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                try { foreach (char c in File.ReadAllText(f, Encoding.UTF8))
                    { ushort u = (ushort)c;
                      if ((u >= 0x4E00 && u <= 0x9FFF) || (u >= 0x3400 && u <= 0x4DBF) ||
                          (u >= 0x3000 && u <= 0x303F) || (u >= 0xFF00 && u <= 0xFFEF) ||
                          (u >= 0x2000 && u <= 0x206F)) set.Add(c); } } catch { }

        // 最后确保 TMP 特殊字符在里面
        set.Add(0x5F);  // _
        set.Add(0x2D);  // -
        set.Add(0x25A1); // □ (占位符)

        // ====== 输出 ======
        string outPath = "Assets/_Game/Fonts/GameCharacters.txt";
        string chars = new string(set.OrderBy(u => u).Select(u => (char)u).ToArray());
        File.WriteAllText(outPath, chars, new UTF8Encoding(true));
        AssetDatabase.Refresh();

        Debug.LogFormat("<color=green>[FontGen] {0} 已生成 ({1} 字符)</color>", outPath, set.Count);

        EditorUtility.DisplayDialog("字符集已生成",
            string.Format("共 {0} 个不重复字符\n保存至：\n{1}\n\n" +
            "下一步：\nWindow > TextMeshPro > Font Asset Creator\n" +
            "Character File 选此文件重新生成字体", set.Count, outPath), "OK");
    }

    static void AddString(HashSet<uint> s, string str)
    {
        if (string.IsNullOrEmpty(str)) return;
        foreach (char c in str) s.Add(c);
    }
}
