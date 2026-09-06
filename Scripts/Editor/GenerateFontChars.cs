using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 工具：扫描 Assets/Scripts 下所有 .cs 的字符串字面量，提取游戏用到的全部字符，
/// 复制到剪贴板（并写一份到 Assets/Fonts/字符列表.txt）。
/// 自动识别 UTF-8 / GBK 编码（Windows 中文环境下脚本常被存成 GBK）。
/// 菜单：Tools → 生成字体字符列表
/// </summary>
public static class GenerateFontChars
{
    [MenuItem("Tools/生成字体字符列表")]
    public static void Generate()
    {
        string scriptsDir = Path.Combine(Application.dataPath, "Scripts");
        if (!Directory.Exists(scriptsDir))
        {
            EditorUtility.DisplayDialog("生成失败", "找不到目录：\n" + scriptsDir, "好的");
            return;
        }

        var chars = new HashSet<char>();
        var regex = new Regex("\"([^\"]*)\"");
        foreach (var file in Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories))
        {
            string text = ReadText(file);
            foreach (Match m in regex.Matches(text))
            {
                string s = Regex.Replace(m.Groups[1].Value, @"\\.", ""); // 去掉 \n 等转义
                foreach (char c in s) chars.Add(c);
            }
        }

        // 动态数字（HP/方糖/警报/还差 的数字是代码算出来的）+ 空格
        for (char c = '0'; c <= '9'; c++) chars.Add(c);
        chars.Add(' ');

        var sorted = new List<char>(chars);
        sorted.Sort();
        string result = new string(sorted.ToArray());

        // 写文件备查（用 UTF-8）
        string dir = Path.Combine(Application.dataPath, "Fonts");
        Directory.CreateDirectory(dir);
        string outPath = Path.Combine(dir, "字符列表.txt");
        File.WriteAllText(outPath, result, Encoding.UTF8);
        AssetDatabase.Refresh();

        // 复制到剪贴板
        GUIUtility.systemCopyBuffer = result;

        Debug.Log("已生成 " + sorted.Count + " 个字符，已复制到剪贴板，并写入 " + outPath + "\n" + result);
        EditorUtility.DisplayDialog("生成完成",
            "共 " + sorted.Count + " 个字符，已复制到剪贴板。\n\n去 Window → TextMeshPro → Font Asset Creator：\n1. Source Font File 选中文字体\n2. Character Set 选 Custom Characters\n3. 输入框 Ctrl+V 粘贴\n4. Generate Font Atlas → Save",
            "好的");
    }

    /// <summary>读文本：自动识别 UTF-8（含/不含 BOM）或 GBK。</summary>
    static string ReadText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        // UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        // 严格 UTF-8 校验
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { }

        // 回退 GBK（codepage 936）
        try { return Encoding.GetEncoding(936).GetString(bytes); }
        catch { return Encoding.Default.GetString(bytes); }
    }
}
