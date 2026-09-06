using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// 选关场景控制器（现在只做「跳板」）：挂在「选关场景」上，Play 后立刻加载第 1 关。
/// 开始界面 / 选关界面都搬到游戏内（UIManager）：第 1 关显示手动做的开始界面，游戏中按 Esc 弹选关。
/// 保留本场景仅为了让 Play 有个入口，避免改 Build Settings 顺序。
/// </summary>
public class LevelSelectController : MonoBehaviour
{
    [System.Serializable]
    public class LevelEntry
    {
        [Tooltip("关卡显示名（按钮文字）")]
        public string displayName = "第一关";
        [Tooltip("关卡场景名（需已加入 Build Settings）")]
        public string sceneName = "Level_1";
    }

    [Header("字体")]
    [Tooltip("中文字体 TMP Font Asset（可选，留空用默认字体）")]
    public TMP_FontAsset fontAsset;

    [Header("关卡列表")]
    [Tooltip("关卡列表（顺序即按钮顺序）。留空 = 默认生成 5 关 Level_1~Level_5。")]
    public List<LevelEntry> levels = new List<LevelEntry>();

    static readonly string[] CN = { "零", "一", "二", "三", "四", "五" };

    private Canvas canvas;
    private GameObject startPanel;  // 开始界面
    private GameObject selectPanel; // 选关界面

    void Start()
    {
        // 开始/选关界面都搬到游戏内（UIManager）了：本场景只负责把 Play 直接带进第 1 关。
        // 第 1 关会显示你手动做的「开始界面」，点开始游戏→直接进对话；选关改成游戏中按 Esc 弹出。
        LevelFlow.skipStartPanel = false;
        SceneManager.LoadScene("Level_1");
    }

    void LoadLevel(string sceneName)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        LevelFlow.skipStartPanel = true; // 进关卡后跳过「开始界面」，直接开玩
        SceneManager.LoadScene(sceneName);
    }

    void OnStartClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        if (startPanel != null) startPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(true);
    }

    void OnBackClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        if (startPanel != null) startPanel.SetActive(true);
        if (selectPanel != null) selectPanel.SetActive(false);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>UI 按钮点击需要 EventSystem（选关场景没手动摆，运行时自动补）。</summary>
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>补一台摄像机 + 音频监听，避免「No cameras rendering / No audio listeners」警告。</summary>
    void EnsureCamera()
    {
        if (Camera.main != null) return;
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
    }

    // ---------- 构建 ----------

    TMP_FontAsset Font => fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;

    void EnsureCanvas()
    {
        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = cgo.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
    }

    void BuildUI()
    {
        if (canvas == null) return;

        // 全屏深色背景（让菜单有底，避免依赖场景相机）
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvas.transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = new Color(0.07f, 0.08f, 0.11f, 1f);
        bg.raycastTarget = false;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgGo.transform.SetAsFirstSibling();

        // 开始界面
        startPanel = MakePanel("StartPanel");
        CreateText(startPanel.transform, "Title", "生长", 96, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(800, 140), TextAlignmentOptions.Center);
        CreateText(startPanel.transform, "StartSub", "一个关于「生长」的俯视 2D 游戏", 24, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(900, 40), TextAlignmentOptions.Center);
        CreateButton(startPanel.transform, "StartBtn", "开始游戏", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(320, 80), OnStartClicked);
        CreateButton(startPanel.transform, "QuitBtn", "退出游戏", new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(320, 80), QuitGame);

        // 选关界面（初始隐藏）
        selectPanel = MakePanel("SelectPanel");
        CreateText(selectPanel.transform, "SelectTitle", "选择关卡", 56, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(800, 80), TextAlignmentOptions.Center);
        var list = levels != null && levels.Count > 0 ? levels : DefaultLevels();
        float startY = 120f;
        float step = 100f;
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            float y = startY - i * step;
            CreateButton(selectPanel.transform, "LevelBtn_" + i, entry.displayName,
                new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 80),
                () => LoadLevel(entry.sceneName));
        }
        CreateButton(selectPanel.transform, "BackBtn", "返回", new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(240, 64), OnBackClicked);
        selectPanel.SetActive(false);
    }

    /// <summary>建一个铺满屏幕的空面板（开始/选关界面共用）。</summary>
    GameObject MakePanel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    List<LevelEntry> DefaultLevels()
    {
        var list = new List<LevelEntry>();
        for (int i = 1; i <= 5; i++)
            list.Add(new LevelEntry { displayName = "第" + CN[i] + "关", sceneName = "Level_" + i });
        return list;
    }

    // ---------- 辅助（与 UIManager 同款） ----------

    static Sprite _whiteSprite;
    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return _whiteSprite;
        }
    }

    RectTransform SetupRect(GameObject go, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI CreateText(Transform parent, string name, string content, int fontSize,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetupRect(go, anchor, anchoredPos, size);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = Font;
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.enableWordWrapping = true;
        return t;
    }

    Button CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetupRect(go, anchor, anchoredPos, size);
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = new Color(0.16f, 0.22f, 0.35f);
        cb.highlightedColor = new Color(0.28f, 0.38f, 0.55f);
        cb.pressedColor = new Color(0.10f, 0.14f, 0.25f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = textGo.GetComponent<TextMeshProUGUI>();
        t.font = Font;
        t.text = label;
        t.fontSize = 30;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        return btn;
    }
}
