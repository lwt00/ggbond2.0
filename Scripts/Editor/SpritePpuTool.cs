using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 美术辅助工具：批量设置选中图片的 Pixels Per Unit，解决「导入后角色/物体巨大」的问题。
/// 默认 Unity 的 PPU = 100，一张 512×512 的图会被当成 5.12 世界单位那么大；
/// 设成「PPU = 宽」后原生尺寸 = 1×1 世界单位，和游戏里白色方块的约定一致，
/// 这样角色组件上的 Size（0.6）就直接是最终世界尺寸，不会巨大。
/// 用法：Project 里选中图片（可多选，也可选文件夹），点菜单 Tools → 批量设置选中 Sprite PPU = 宽。
/// </summary>
public static class SpritePpuTool
{
    [MenuItem("Tools/批量设置选中 Sprite PPU = 宽（原生 1×1）")]
    public static void SetPpuToWidth()
    {
        var paths = CollectTexturePaths(Selection.objects);
        int changed = 0;
        foreach (var path in paths)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null || tex.width <= 0) continue;

            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = tex.width;
            ti.SaveAndReimport();
            changed++;
        }

        EditorUtility.DisplayDialog("完成",
            "已设置 " + changed + " 个 Sprite 的 PPU = 宽（原生 1×1）。\n（选了文件夹会连同里面的图片一起处理）", "好的");
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
