using UnityEngine;
using UnityEditor;

/// <summary>
/// 关卡数据（LevelData）的自定义 Inspector：顶部可视化预览（房间/敌人/方糖），
/// 下方照常显示可编辑字段（房间列表、逻辑参数）；并支持一键用默认内容重建。
/// </summary>
[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var data = (LevelData)target;

        // 重建按钮
        int level = ParseLevel();
        if (GUILayout.Button("用默认内容重建本关（覆盖当前编辑）"))
        {
            if (EditorUtility.DisplayDialog("关卡工具",
                "将用默认内容覆盖本关（房间/敌人/方糖/逻辑参数）。\n确定？", "重建", "取消"))
            {
                data.rooms = MapData.BuildLevel(level > 0 ? level : 1, 16f, 6f);
                MapData.ApplyLevelParams(data, level > 0 ? level : 1);
                EditorUtility.SetDirty(data);
            }
        }

        EditorGUILayout.LabelField("关卡预览", EditorStyles.boldLabel);
        DrawPreview(data);

        EditorGUILayout.Space(8);
        DrawDefaultInspector();
    }

    /// <summary>从资产文件名解析关卡号（Level_03 → 3）。</summary>
    int ParseLevel()
    {
        string path = AssetDatabase.GetAssetPath(target);
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        int under = name.LastIndexOf('_');
        int n;
        if (under >= 0 && int.TryParse(name.Substring(under + 1), out n)) return n;
        return 0;
    }

    void DrawPreview(LevelData data)
    {
        if (data.rooms == null || data.rooms.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无房间数据。点上方按钮用默认内容生成，或手动添加房间。", MessageType.Info);
            return;
        }

        // 预留一块绘图区域（每次 layout 都保留同样大小）
        Rect area = GUILayoutUtility.GetRect(0, 340, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;

        // 计算世界包围盒
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var r in data.rooms)
        {
            Vector2 half = r.size * 0.5f;
            min = Vector2.Min(min, r.center - half);
            max = Vector2.Max(max, r.center + half);
        }
        Vector2 ext = max - min;
        if (ext.x < 0.001f || ext.y < 0.001f) return;

        // 内缩一点留白，并保持等比
        float pad = 12f;
        Rect inner = new Rect(area.x + pad, area.y + pad, area.width - pad * 2f, area.height - pad * 2f);
        float scale = Mathf.Min(inner.width / ext.x, inner.height / ext.y);
        Vector2 worldCenter = (min + max) * 0.5f;
        Vector2 screenCenter = inner.center;

        Vector2 W2S(Vector2 w)
        {
            Vector2 off = w - worldCenter;
            return new Vector2(screenCenter.x + off.x * scale, screenCenter.y - off.y * scale);
        }

        EditorGUI.DrawRect(area, new Color(0.07f, 0.07f, 0.09f, 1f));

        // 房间（传送门房绿色，普通房灰蓝）
        foreach (var r in data.rooms)
        {
            Vector2 c = W2S(r.center);
            float w = r.size.x * scale;
            float h = r.size.y * scale;
            var rect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            Color fill = r.isPortal ? new Color(0.15f, 0.4f, 0.25f) : new Color(0.22f, 0.24f, 0.3f);
            EditorGUI.DrawRect(rect, fill);
            DrawOutline(rect, new Color(0.45f, 0.48f, 0.55f));
        }

        // 房间内容标注：方糖数 / 敌人数（计数式，实际位置运行时散布）
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
        };
        labelStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
        foreach (var r in data.rooms)
        {
            int enemies = 0;
            if (r.enemySpawns != null)
                foreach (var s in r.enemySpawns) enemies += s.count;

            Vector2 c = W2S(r.center);
            float w = r.size.x * scale;
            float h = r.size.y * scale;

            string txt = r.isPortal ? "传送门" : "";
            if (r.hasNPC) txt += "NPC ";
            if (r.sugarCount > 0) txt += "糖" + r.sugarCount + " ";
            if (enemies > 0) txt += "敌" + enemies;
            if (string.IsNullOrEmpty(txt)) txt = "空";

            GUI.Label(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, 20f), txt, labelStyle);
        }

        // 障碍（灰紫方块，用墙的美术生成）
        foreach (var r in data.rooms)
        {
            foreach (var o in r.obstacles)
            {
                Vector2 c = W2S(o.center);
                float w = o.size.x * scale;
                float h = o.size.y * scale;
                EditorGUI.DrawRect(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), new Color(0.55f, 0.48f, 0.6f));
            }
        }
    }

    void DrawOutline(Rect r, Color c)
    {
        float t = 1f;
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    void DrawDot(Vector2 c, Color col, float size)
    {
        EditorGUI.DrawRect(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), col);
    }
}
