using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 美术辅助工具：给「地板砖 / 墙砖」这类平铺图做平铺准备（把 Wrap Mode 设成 Repeat）。
/// 注意：砖块大小已经不用这里设了——直接在场景里选中 Map 物体，在 Inspector 拖
/// 「Tile Size（每块砖世界尺寸）」，越小砖越多、纹理越清晰，运行时实时生效。
/// 参考：房间边长 16、通道宽 4、墙厚 1、角色约 0.6，地砖建议 0.5~1.5、墙砖 ≤1。
/// 用法：Project 里选中地板/墙图片（可多选），菜单 Tools → 美术辅助 → 地板墙瓷砖平铺准备...。
/// </summary>
public class TilePpuTool : EditorWindow
{
    [MenuItem("Tools/美术辅助/地板墙瓷砖平铺准备...")]
    static void Open()
    {
        GetWindow<TilePpuTool>("地板墙瓷砖平铺准备");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "选中 Project 里的「地板砖 / 墙砖」图片（可多选），点下面按钮，把 Wrap Mode 设成 Repeat（平铺必需）。\n\n" +
            "【砖块大小在这里设】直接在场景里选中 Map 物体，在 Inspector 拖\n" +
            "「Tile Size（每块砖世界尺寸）」——越小砖越多、纹理越清晰，运行时实时重建，不用重导入。",
            MessageType.Info);

        if (GUILayout.Button("把选中图片设为 Repeat（可平铺）", GUILayout.Height(30)))
            Apply();
    }

    void Apply()
    {
        var paths = CollectTexturePaths(Selection.objects);
        int changed = 0;
        foreach (var path in paths)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.wrapMode = TextureWrapMode.Repeat; // 平铺必需
            ti.SaveAndReimport();
            changed++;
        }

        EditorUtility.DisplayDialog("完成",
            "已把 " + changed + " 张图设为 Repeat（可平铺）。\n砖块大小请到 Map 物体的「Tile Size」上实时拖。", "好的");
    }

    /// <summary>把选中对象（文件或文件夹）展开成图片路径列表。</summary>
    static List<string> CollectTexturePaths(Object[] objects)
    {
        var list = new List<string>();
        foreach (var obj in objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!list.Contains(p)) list.Add(p);
                }
            }
            else
            {
                if (!list.Contains(path)) list.Add(path);
            }
        }
        return list;
    }
}
