using UnityEditor;
using UnityEngine;

/// <summary>
/// 障碍生成器（菜单：Tools/关卡工具/障碍生成器...）：
///   批量给 3 个关卡生成房间内障碍（用墙的美术、带碰撞体挡路）。
///   每关用固定种子 → 每关障碍不同；会自动避开门口、方糖、敌人和出生点。
///   房间连接（布局）在 5 套预设形状里按关卡轮流换。
/// 两个按钮：
///   ① 批量重生成 5 关（布局 + 内容 + 障碍）
///   ② 只重生成障碍（保留当前布局和内容）
/// </summary>
public class ObstacleTool : EditorWindow
{
    const string LevelFolder = "Assets/Levels";
    const int LevelCount = 3;
    const float RoomSize = 16f;
    const float Gap = 6f;

    int obstaclesPerRoom = MapData.DefaultObstaclesPerRoom;
    float obstacleMinSize = MapData.DefaultObstacleMinSize;
    float obstacleMaxSize = MapData.DefaultObstacleMaxSize;
    float obstacleMargin = MapData.DefaultObstacleMargin;

    [MenuItem("Tools/关卡工具/障碍生成器...")]
    static void Open()
    {
        var w = GetWindow<ObstacleTool>("障碍生成器");
        w.obstaclesPerRoom = EditorPrefs.GetInt("ObstacleTool.perRoom", MapData.DefaultObstaclesPerRoom);
        w.obstacleMinSize = EditorPrefs.GetFloat("ObstacleTool.minSize", MapData.DefaultObstacleMinSize);
        w.obstacleMaxSize = EditorPrefs.GetFloat("ObstacleTool.maxSize", MapData.DefaultObstacleMaxSize);
        w.obstacleMargin = EditorPrefs.GetFloat("ObstacleTool.margin", MapData.DefaultObstacleMargin);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "批量给 3 个关卡生成障碍（用墙的美术，带碰撞体挡路）。\n" +
            "每关用固定种子，障碍不同；自动避开门口、方糖、敌人和出生点。\n" +
            "房间连接（布局）也在 5 套预设形状里轮流换。",
            MessageType.Info);

        obstaclesPerRoom = EditorGUILayout.IntSlider("每房障碍数", obstaclesPerRoom, 0, 8);
        obstacleMinSize = EditorGUILayout.Slider("障碍最小尺寸", obstacleMinSize, 0.8f, 6f);
        obstacleMaxSize = EditorGUILayout.Slider("障碍最大尺寸", obstacleMaxSize, 1f, 8f);
        if (obstacleMaxSize < obstacleMinSize) obstacleMaxSize = obstacleMinSize;
        obstacleMargin = EditorGUILayout.Slider("距墙留边（障碍不贴墙）", obstacleMargin, 1f, 6f);

        EditorGUILayout.Space();

        if (GUILayout.Button("① 批量重生成 3 关（布局+内容+障碍）", GUILayout.Height(30)))
            RegenerateAll(true);

        if (GUILayout.Button("② 只重生成障碍（保留当前布局/内容）", GUILayout.Height(30)))
            RegenerateAll(false);
    }

    void SavePrefs()
    {
        EditorPrefs.SetInt("ObstacleTool.perRoom", obstaclesPerRoom);
        EditorPrefs.SetFloat("ObstacleTool.minSize", obstacleMinSize);
        EditorPrefs.SetFloat("ObstacleTool.maxSize", obstacleMaxSize);
        EditorPrefs.SetFloat("ObstacleTool.margin", obstacleMargin);
    }

    void RegenerateAll(bool full)
    {
        SavePrefs();
        if (!AssetDatabase.IsValidFolder(LevelFolder))
            AssetDatabase.CreateFolder("Assets", "Levels");

        int changed = 0;
        for (int level = 1; level <= LevelCount; level++)
        {
            string path = LevelFolder + "/Level_" + level.ToString("00") + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(data, path);
            }

            if (full || data.rooms == null || data.rooms.Count == 0)
            {
                data.rooms = MapData.BuildLevel(level, RoomSize, Gap,
                    obstaclesPerRoom, obstacleMinSize, obstacleMaxSize, obstacleMargin);
                MapData.ApplyLevelParams(data, level);
            }
            else
            {
                MapData.GenerateObstacles(data.rooms, level,
                    obstaclesPerRoom, obstacleMinSize, obstacleMaxSize, obstacleMargin);
            }

            EditorUtility.SetDirty(data);
            changed++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("障碍生成器",
            "已处理 " + changed + " 个关卡。\n" +
            (full ? "布局 / 内容 / 障碍 都已重生成。\n" : "障碍已重生成（保留布局和内容）。\n") +
            "回到游戏场景点 Play 即可看到。", "好的");
    }
}
