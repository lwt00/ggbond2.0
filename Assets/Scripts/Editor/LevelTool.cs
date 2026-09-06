using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 关卡生成工具（菜单：Tools/关卡工具/...）：
///   ① 生成关卡数据资产 Level_01~03（默认内容，难度递增）。
///   ② 由「当前打开的场景」作为模板，复制出 3 个关卡场景（各指定对应关卡数据），
///      再生成一个选关场景，并把它们按顺序写进 Build Settings（选关在最前）。
/// 运行时代码在 nm，此工具在你运行 Unity 后手动执行，直接作用于当前工程。
/// </summary>
public static class LevelTool
{
    const string LevelFolder = "Assets/Levels";
    const string SceneFolder = "Assets/Scenes";
    const int LevelCount = 3;
    const float RoomSize = 16f;
    const float Gap = 6f;

    static readonly string[] CN = { "零", "一", "二", "三", "四", "五" };

    // ---------- ① 生成关卡数据资产 ----------

    [MenuItem("Tools/关卡工具/① 生成关卡数据资产 (Level_01~03)")]
    static void GenerateLevelAssets()
    {
        EnsureFolder(LevelFolder);
        for (int level = 1; level <= LevelCount; level++)
        {
            var data = GetOrCreateLevelAsset(level);
            FillLevel(data, level);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("关卡工具", "已生成 " + LevelCount + " 个关卡数据资产到 " + LevelFolder + "。", "确定");
    }

    // ---------- ② 生成关卡场景 + 选关场景 ----------

    [MenuItem("Tools/关卡工具/② 由当前场景生成关卡场景 + 选关场景")]
    static void GenerateScenes()
    {
        var template = EditorSceneManager.GetActiveScene();
        if (!template.IsValid() || string.IsNullOrEmpty(template.path))
        {
            EditorUtility.DisplayDialog("关卡工具",
                "请先打开你搭好的游戏场景（作为模板），再执行本工具。", "确定");
            return;
        }
        if (template.isDirty) EditorSceneManager.SaveScene(template); // 用当前已保存内容作模板

        string templatePath = template.path;
        if (!EditorUtility.DisplayDialog("关卡工具",
            "将基于当前场景生成 " + LevelCount + " 个关卡场景 + 1 个选关场景，并写入 Build Settings。\n\n模板：" + templatePath,
            "生成", "取消")) return;

        EnsureFolder(LevelFolder);
        EnsureFolder(SceneFolder);
        EnsureLevelAssets();

        // 复用游戏场景里的中文字体（选关界面也要用，否则中文显示成框框）
        TMP_FontAsset font = null;
        var templateUi = Object.FindObjectOfType<UIManager>();
        if (templateUi != null) font = templateUi.fontAsset;

        var levelScenePaths = new List<string>();
        for (int level = 1; level <= LevelCount; level++)
        {
            var data = AssetDatabase.LoadAssetAtPath<LevelData>(LevelFolder + "/Level_" + level.ToString("00") + ".asset");
            string scenePath = SceneFolder + "/Level_" + level + ".unity";
            CreateLevelScene(templatePath, scenePath, data);
            levelScenePaths.Add(scenePath);
        }

        string selectPath = SceneFolder + "/LevelSelect.unity";
        CreateSelectScene(selectPath, levelScenePaths, font);

        SetupBuildSettings(selectPath, levelScenePaths);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("关卡工具",
            "完成：\n" + selectPath + "（选关）\n" + string.Join("\n", levelScenePaths) +
            "\n\n已加入 Build Settings，选关场景在最前。\n当前已打开选关场景，可直接 Play 测试。", "确定");
    }

    // ---------- 内部实现 ----------

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static LevelData GetOrCreateLevelAsset(int level)
    {
        string path = LevelFolder + "/Level_" + level.ToString("00") + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (existing != null) return existing;
        var data = ScriptableObject.CreateInstance<LevelData>();
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    static void FillLevel(LevelData data, int level)
    {
        data.rooms = MapData.BuildLevel(level, RoomSize, Gap);
        MapData.ApplyLevelParams(data, level);
        EditorUtility.SetDirty(data);
    }

    static void EnsureLevelAssets()
    {
        for (int level = 1; level <= LevelCount; level++)
        {
            var data = GetOrCreateLevelAsset(level);
            if (data.rooms == null || data.rooms.Count == 0) FillLevel(data, level);
        }
        AssetDatabase.SaveAssets();
    }

    static void CreateLevelScene(string templatePath, string outPath, LevelData data)
    {
        if (!AssetDatabase.CopyAsset(templatePath, outPath))
        {
            Debug.LogError("[关卡工具] 复制模板场景失败：" + outPath);
            return;
        }
        var scene = EditorSceneManager.OpenScene(outPath, OpenSceneMode.Single);
        var mg = Object.FindObjectOfType<MapGenerator>();
        if (mg != null)
        {
            mg.levelData = data;
            EditorUtility.SetDirty(mg);
        }
        else
        {
            Debug.LogWarning("[关卡工具] 场景里没找到 MapGenerator：" + outPath);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void CreateSelectScene(string outPath, List<string> levelScenePaths, TMP_FontAsset font)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 摄像机 + 音频监听（避免 "No cameras rendering / No audio listeners" 警告）
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";

        // UI 事件系统（按钮点击需要）
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject("LevelSelect");
        var ctrl = go.AddComponent<LevelSelectController>();
        ctrl.fontAsset = font; // 复用游戏场景里的中文字体
        ctrl.levels = new List<LevelSelectController.LevelEntry>();
        for (int i = 0; i < levelScenePaths.Count; i++)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(levelScenePaths[i]);
            ctrl.levels.Add(new LevelSelectController.LevelEntry
            {
                displayName = "第" + CN[i + 1] + "关",
                sceneName = sceneName
            });
        }
        EditorSceneManager.SaveScene(scene, outPath);
    }

    static void SetupBuildSettings(string selectPath, List<string> levelScenePaths)
    {
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(selectPath, true) };
        foreach (var p in levelScenePaths)
            scenes.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
