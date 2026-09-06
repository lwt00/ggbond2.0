using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Video;

/// <summary>
/// UI 管理器（手动版）：
///   - 开始界面 / 结算界面：由你在场景里手动创建（Canvas + 面板 + 按钮 + 文字），拖拽赋值，自由调整。
///   - HUD（HP 条 / 警报 / 方糖 / 提示）：仍由代码自动生成（放进你指定的 Canvas）。
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("画布与字体")]
    [Tooltip("场景里手动创建的 Canvas（拖进来）。HUD 会自动生成到它下面")]
    public Canvas canvas;
    [Tooltip("中文字体 TMP Font Asset（HUD 和按钮文字会统一套用）")]
    public TMP_FontAsset fontAsset;

    [Header("剧情对话（自动生成）")]
    [Tooltip("对话框背景贴图（拖进去则替换默认黑条；留空 = 默认半透明黑条）")]
    public Sprite dialogueBoxSprite;
    [Tooltip("对话正文颜色（默认黑色，配合浅色对话框美术用）")]
    public Color dialogueTextColor = Color.black;
    [Tooltip("继续提示文字颜色（默认黑色半透明）")]
    public Color dialogueHintColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("开始界面（手动创建）")]
    [Tooltip("开始界面根物体（含开始/退出两个按钮）")]
    public GameObject startPanel;
    [Tooltip("「开始游戏」按钮")]
    public Button startButton;
    [Tooltip("「退出游戏」按钮")]
    public Button quitButton;

    [Header("结算界面（手动创建）")]
    [Tooltip("结算界面根物体")]
    public GameObject endingPanel;
    [Tooltip("结算标题文字")]
    public TextMeshProUGUI endingTitle;
    [Tooltip("结算描述文字")]
    public TextMeshProUGUI endingDesc;
    [Tooltip("「重新开始」按钮")]
    public Button restartButton;

    [Header("过关界面（手动创建）")]
    [Tooltip("过关弹窗根物体（含「下一关」按钮）")]
    public GameObject clearPanel;
    [Tooltip("过关标题文字（如「第一关 通过」）")]
    public TextMeshProUGUI clearTitle;
    [Tooltip("过关描述文字（可选，如击杀/吸糖统计）")]
    public TextMeshProUGUI clearDesc;
    [Tooltip("「下一关」按钮")]
    public Button nextButton;

    [Header("选关界面（游戏中按 Esc 暂停弹出，自动生成）")]
    [Tooltip("是否启用 Esc 选关暂停菜单（默认启用）")]
    public bool enableLevelSelect = true;
    [Tooltip("选关列表（留空 = 默认生成 3 关 Level_1~Level_3）")]
    public List<LevelEntry> selectLevels = new List<LevelEntry>();

    [Header("开场视频（点「开始游戏」后播放，留空 = 跳过）")]
    [Tooltip("开场视频文件（.mp4 等）。留空 = 不播视频，直接开始游戏")]
    public VideoClip introVideo;

    [Header("危险警示（屏幕边缘泛红）")]
    [Tooltip("敌人逼近/警报升级时，屏幕边缘泛红的最大不透明度（0~1）")]
    public float dangerMaxAlpha = 0.45f;

    // HUD（自动生成）
    private GameObject hudRoot;
    private Image hpFill;
    private TextMeshProUGUI hpText, alertText, sugarText, killText, hintText, messageText;
    private float messageTimer;
    private bool endingShown;

    // 开场视频（点「开始游戏」后播；留空 = 跳过）
    private VideoPlayer introPlayer;
    private RawImage introVideoImage;
    private Image introWhiteOverlay;   // 白色全屏遮罩（盖住地图 + 所有 UI）
    private bool introVideoPlaying;
    private float introSkipGuard;
    private Coroutine introCo;      // 视频 prepare→play 协程
    private bool gameplayStarted;   // 本场景是否已经真正开始游戏（防止重复 StartLevel）

    // 抉择弹窗（二选一：继续杀戮 / 消灭自己）
    private GameObject choicePanel;
    private TextMeshProUGUI choiceTitle, choiceDesc;
    private Button choiceButtonA, choiceButtonB;
    private TextMeshProUGUI choiceLabelA, choiceLabelB;
    private System.Action choiceOnA, choiceOnB;

    // 危险覆盖层
    private Image dangerOverlay;
    private float dangerLevel;  // 敌人逼近的持续值（0~1，每帧由 GameManager 驱动）
    private float pulseValue;   // 警报升级的瞬态脉冲（随时间衰减）

    // 氛围遮罩（最终抉择：GROW = 黑红压暗渐入 / NO = 暖光提亮），颜色和目标透明度渐变过渡
    private Image ambienceOverlay;
    private Color ambienceTargetColor = Color.clear;

    // 小地图（右上角）
    private RectTransform minimapRoot;
    private Image minimapBg;
    private Image playerDot;
    private List<Image> roomImages = new List<Image>();
    private List<Image> minimapLines = new List<Image>();
    private float minimapScale = 1f;
    private Vector2 minimapWorldMin, minimapWorldMax, minimapLocalCenter;
    private const float MinimapSize = 180f;
    private const float MinimapPad = 10f;
    private bool minimapRoomsBuilt;

    // 剧情对话（打字机）
    private GameObject dialogueRoot;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI dialogueHint;
    private string[] dialogueLines;
    private int dialogueIndex;
    private int dialogueVisibleChars;
    private float dialogueCharTimer;
    private bool dialogueActive;
    private bool dialogueTyping;
    private System.Action dialogueOnDone;
    private float dialoguePrevTimeScale;
    private const float DialogueCharsPerSecond = 45f;
    private const float TypingSoundSeconds = 1f;
    private const float DialogueAutoAdvanceDelay = 1.6f; // 打完一行后自动切下一行（最后一行自动关闭）
    private float dialogueIdleTimer;                     // 打完后停留计时
    private const float DialogueMaxDuration = 20f;       // 兜底：超过 20 秒强制关闭（防任何异常卡住）
    private float dialogueSafetyTimer;                   // 整段对话总计时

    // 选关暂停菜单（Esc）
    private GameObject selectPanel;
    private bool selectOpen;
    private float selectPrevTimeScale = 1f;
    private static readonly string[] LevelCN = { "零", "一", "二", "三", "四", "五" };

    /// <summary>是否有剧情对话正在播放（PlayerController 据此禁用攻击输入）。</summary>
    public static bool DialogueActive { get; private set; }

    void Start()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        EnsureCanvas();
        EnsureEventSystem();

        BuildHUD();
        BuildLevelSelectPanel();

        // 从选关/下一关进入时跳过「开始界面」直接开玩；否则照旧暂停 + 显示开始界面
        bool skipStart = LevelFlow.skipStartPanel;
        LevelFlow.skipStartPanel = false; // 只生效一次
        Time.timeScale = skipStart ? 1f : 0f;
        if (hudRoot != null) hudRoot.SetActive(skipStart);
        if (startPanel != null) startPanel.SetActive(!skipStart);
        if (endingPanel != null) endingPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);

        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitGame);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextLevel);

        ApplyFont();

        // 从选关/下一关直接进游戏（跳过开始界面）：立刻起 BGM + 播开场对话
        if (skipStart && GameManager.Instance != null)
        {
            gameplayStarted = true; // 跳过了开始界面，本场景已开玩
            GameManager.Instance.StartLevel();
        }
    }

    void Update()
    {
        // 选关暂停菜单打开时：只处理 Esc 关闭，其它 UI 全停
        if (selectOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseLevelSelect();
            return;
        }

        // 开场视频播放中：0.3 秒后任意键/点击跳过
        if (introVideoPlaying)
        {
            if (introSkipGuard > 0f) introSkipGuard -= Time.unscaledDeltaTime;
            else if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) OnIntroVideoEnd(introPlayer);
        }

        UpdateDialogue();

        UpdateDangerOverlay();

        UpdateAmbienceOverlay();

        if (endingShown)
        {
            if (Input.GetKeyDown(KeyCode.R)) OnRestart(); // 结算时也能按 R 重开
            return;
        }

        UpdateHUD();

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.text = "";
        }

        // Esc：游戏中（不在对话、不在开始界面）弹出选关暂停菜单
        if (enableLevelSelect && !DialogueActive && hudRoot != null && hudRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            OpenLevelSelect();
    }

    void OnStartGame()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        PlayIntroVideo(); // 先播开场视频（留空则直接开始），视频结束后才起 BGM + 开玩
    }

    /// <summary>播放开场视频；没拖视频、或本次运行已经播过，就直接开始。</summary>
    void PlayIntroVideo()
    {
        if (startPanel != null) startPanel.SetActive(false);

        // 视频只在「最最开始第一次点开始游戏」播一次；重开 / 下一关再来都不再播
        if (introVideo == null || LevelFlow.introPlayed) { StartGameplay(); return; }
        LevelFlow.introPlayed = true; // 开始播就标记，本次运行不再重播

        if (introPlayer == null) BuildIntroVideo();
        Time.timeScale = 0f;                              // 视频期间：暂停游戏的一切
        if (hudRoot != null) hudRoot.SetActive(false);    // 确保 HUD 不露出来
        if (introWhiteOverlay != null) introWhiteOverlay.gameObject.SetActive(true); // 白布盖住地图和 UI
        introVideoImage.gameObject.SetActive(true);       // 视频盖在最上层
        introPlayer.clip = introVideo;
        introVideoPlaying = true;
        introSkipGuard = 0.3f; // 前 0.3 秒忽略跳过输入，避免点「开始游戏」的那一下误跳过

        // 先 Prepare 再 Play：避免未准备好就 Play 导致视频不播 / loopPointReached 提前触发
        if (introCo != null) StopCoroutine(introCo);
        introCo = StartCoroutine(IntroPlayRoutine());
    }

    /// <summary>等视频准备好后播放；准备失败/超时则直接开玩，绝不卡白屏。</summary>
    System.Collections.IEnumerator IntroPlayRoutine()
    {
        introPlayer.Prepare();
        float t = 0f;
        while (!introPlayer.isPrepared && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 用户在准备期间已经跳过（按了任意键）→ 不再播放，直接结束
        if (!introVideoPlaying) yield break;

        if (introPlayer.isPrepared)
        {
            introPlayer.Play(); // 播完由 loopPointReached → OnIntroVideoEnd 进入游戏
        }
        else
        {
            // 视频准备失败：不要卡住，直接开始游戏
            OnIntroVideoEnd(introPlayer);
        }
    }

    /// <summary>视频播完（或被跳过）后：起 BGM + 播开场对话 + 显示房间。</summary>
    void OnIntroVideoEnd(VideoPlayer vp)
    {
        if (!introVideoPlaying) return;
        introVideoPlaying = false;
        if (introPlayer != null) introPlayer.Stop();
        StartGameplay();
    }

    /// <summary>真正开始游戏（起 BGM + 播本关开场对话）。</summary>
    void StartGameplay()
    {
        if (gameplayStarted) return; // 防止视频结束 + 跳过 双重触发导致对话重复播
        gameplayStarted = true;

        if (introVideoImage != null) introVideoImage.gameObject.SetActive(false);
        if (introWhiteOverlay != null) introWhiteOverlay.gameObject.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(true);
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.StartLevel();
    }

    /// <summary>运行时自动搭一个全屏视频层（RawImage + VideoPlayer）。</summary>
    void BuildIntroVideo()
    {
        if (canvas == null) EnsureCanvas();

        // 白色全屏遮罩：盖住地图 + 所有 UI（HUD / 对话框 / 开始界面），保证视频下方不露出任何东西
        var whiteGo = new GameObject("IntroWhite", typeof(RectTransform), typeof(Image));
        whiteGo.transform.SetParent(canvas.transform, false);
        var wrt = whiteGo.GetComponent<RectTransform>();
        wrt.anchorMin = Vector2.zero;
        wrt.anchorMax = Vector2.one;
        wrt.offsetMin = Vector2.zero;
        wrt.offsetMax = Vector2.zero;
        whiteGo.transform.SetAsLastSibling();
        introWhiteOverlay = whiteGo.GetComponent<Image>();
        introWhiteOverlay.color = Color.white; // 纯白不透明

        // 视频层：盖在白色上面（最上层）
        var go = new GameObject("IntroVideo", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling(); // 视频盖在最上层

        introVideoImage = go.GetComponent<RawImage>();
        introVideoImage.color = Color.white;

        introPlayer = go.AddComponent<VideoPlayer>();
        introPlayer.playOnAwake = false;
        introPlayer.isLooping = false;
        introPlayer.renderMode = VideoRenderMode.RenderTexture;
        introPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        introPlayer.targetTexture = new RenderTexture(1920, 1080, 0);
        introPlayer.loopPointReached += OnIntroVideoEnd;
        introVideoImage.texture = introPlayer.targetTexture;

        whiteGo.SetActive(false);
        go.SetActive(false);
    }

    void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void UpdateHUD()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;

        var h = GameManager.Instance.player.GetComponent<Health>();
        if (h != null)
        {
            if (hpFill != null) hpFill.fillAmount = h.Ratio;
            if (hpText != null) hpText.text = "HP " + h.currentHp + " / " + h.maxHp;
        }
        if (alertText != null) alertText.text = "警报等级 " + GameManager.Instance.alertLevel;
        if (sugarText != null) sugarText.text = "方糖 " + GameManager.Instance.absorbedSugar + " / " + GameManager.Instance.totalSugar;
        if (killText != null && GameManager.Instance.player.stats != null)
            killText.text = "击杀 " + GameManager.Instance.player.stats.killCount;

        UpdateMinimap();
    }

    /// <summary>显示一条短暂提示（约 2.5 秒后消失）。</summary>
    public void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        messageTimer = 2.5f;
    }

    /// <summary>设置「敌人逼近」危险覆盖层强度（0~1，每帧由 GameManager 驱动）。</summary>
    public void SetDanger(float t)
    {
        dangerLevel = Mathf.Clamp01(t);
    }

    /// <summary>警报升级时触发一次屏幕边缘泛红脉冲。</summary>
    public void PulseAlert()
    {
        pulseValue = 1f;
    }

    void UpdateDangerOverlay()
    {
        if (dangerOverlay == null) return;
        float a = Mathf.Max(dangerLevel, pulseValue) * dangerMaxAlpha;
        dangerOverlay.color = new Color(1f, 0f, 0f, a);
        if (pulseValue > 0f)
            pulseValue = Mathf.MoveTowards(pulseValue, 0f, Time.unscaledDeltaTime * 2.5f);
    }

    /// <summary>切换氛围遮罩（最终抉择 GROW/NO）：黑红压暗或暖光提亮，逐帧渐变到目标色。</summary>
    public void SetAmbienceOverlay(Color c)
    {
        if (ambienceOverlay == null) BuildAmbienceOverlay();
        ambienceTargetColor = c;
    }

    void BuildAmbienceOverlay()
    {
        if (canvas == null) EnsureCanvas();
        var go = new GameObject("AmbienceOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // 放在 HUD 最底层（危险红晕下面）：盖住地图和世界，但不挡 HUD 文字
        go.transform.SetAsFirstSibling();
        ambienceOverlay = go.GetComponent<Image>();
        ambienceOverlay.sprite = WhiteSprite;
        ambienceOverlay.color = Color.clear;
        ambienceOverlay.raycastTarget = false;
    }

    void UpdateAmbienceOverlay()
    {
        if (ambienceOverlay == null) return;
        Color c = ambienceOverlay.color;
        float k = Time.unscaledDeltaTime * 0.6f; // 约 1.5 秒完成过渡
        c.r = Mathf.MoveTowards(c.r, ambienceTargetColor.r, k);
        c.g = Mathf.MoveTowards(c.g, ambienceTargetColor.g, k);
        c.b = Mathf.MoveTowards(c.b, ambienceTargetColor.b, k);
        c.a = Mathf.MoveTowards(c.a, ambienceTargetColor.a, k);
        ambienceOverlay.color = c;
    }

    /// <summary>显示结算画面并暂停游戏。</summary>
    public void ShowEnding(string title, string desc)
    {
        endingShown = true;
        Time.timeScale = 0f;
        if (endingPanel != null) endingPanel.SetActive(true);
        if (endingTitle != null) endingTitle.text = title;
        if (endingDesc != null) endingDesc.text = desc;
    }

    /// <summary>显示过关弹窗并暂停（点「下一关」进下一关）。没手动搭过关弹窗就自动生成一个，避免误进结局。</summary>
    public void ShowClear(string title, string desc)
    {
        if (clearPanel == null) BuildClearPanel(); // 没搭过关弹窗 → 自动生成
        if (clearPanel == null) { ShowEnding(title, desc); return; } // 兜底
        endingShown = true;
        Time.timeScale = 0f;
        clearPanel.SetActive(true);
        if (clearTitle != null) clearTitle.text = title;
        if (clearDesc != null) clearDesc.text = desc;
    }

    /// <summary>显示「二选一」抉择弹窗并暂停游戏。点任一按钮恢复时间并回调对应分支。</summary>
    public void ShowChoice(string title, string desc, string optA, string optB, System.Action onA, System.Action onB)
    {
        if (choicePanel == null) BuildChoicePanel();
        if (choicePanel == null) { (onB ?? onA)?.Invoke(); return; } // 兜底

        choiceOnA = onA;
        choiceOnB = onB;
        Time.timeScale = 0f;
        if (choiceTitle != null) choiceTitle.text = title;
        if (choiceDesc != null) choiceDesc.text = desc;
        if (choiceLabelA != null) choiceLabelA.text = optA;
        if (choiceLabelB != null) choiceLabelB.text = optB;
        choicePanel.SetActive(true);
    }

    void CloseChoicePanel()
    {
        Time.timeScale = 1f;
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    void OnChoiceA()
    {
        CloseChoicePanel();
        var a = choiceOnA; choiceOnA = null; choiceOnB = null;
        a?.Invoke();
    }

    void OnChoiceB()
    {
        CloseChoicePanel();
        var b = choiceOnB; choiceOnA = null; choiceOnB = null;
        b?.Invoke();
    }

    /// <summary>「下一关」按钮：加载下一关场景（L1→L2→L3）。</summary>
    void OnNextLevel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        int next = (GameManager.Instance != null ? GameManager.Instance.currentLevel : 1) + 1;
        LevelFlow.skipStartPanel = true;
        SceneManager.LoadScene("Level_" + next);
    }

    /// <summary>没手动搭过关弹窗时，运行时自动生成一个（标题 + 统计 + 「下一关」按钮），保证能进下一关。</summary>
    void BuildClearPanel()
    {
        if (canvas == null) EnsureCanvas();

        var go = new GameObject("ClearPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();

        var dim = CreateImage(go.transform, "Dim", new Color(0f, 0f, 0f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;

        clearTitle = CreateText(go.transform, "Title", "过关", 64, new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(800, 90), TextAlignmentOptions.Center);
        clearDesc = CreateText(go.transform, "Desc", "", 30, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 80), TextAlignmentOptions.Center);
        clearDesc.color = new Color(0.95f, 0.96f, 1f, 1f);

        nextButton = CreateButton(go.transform, "NextBtn", "下一关", new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(320, 80), OnNextLevel);

        clearPanel = go;
        clearPanel.SetActive(false);
    }

    /// <summary>没手动搭抉择弹窗时，运行时自动生成（标题 + 描述 + 两个按钮），保证结局分支能选。</summary>
    void BuildChoicePanel()
    {
        if (canvas == null) EnsureCanvas();

        var go = new GameObject("ChoicePanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();

        var dim = CreateImage(go.transform, "Dim", new Color(0f, 0f, 0f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;

        choiceTitle = CreateText(go.transform, "Title", "抉择", 56, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 80), TextAlignmentOptions.Center);
        choiceDesc = CreateText(go.transform, "Desc", "", 30, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1200, 160), TextAlignmentOptions.Center);
        choiceDesc.color = new Color(0.95f, 0.96f, 1f, 1f);

        choiceButtonA = CreateButton(go.transform, "ChoiceA", "继续杀戮", new Vector2(0.5f, 0.5f), new Vector2(-200, -80), new Vector2(360, 80), OnChoiceA);
        choiceButtonB = CreateButton(go.transform, "ChoiceB", "消灭自己", new Vector2(0.5f, 0.5f), new Vector2(200, -80), new Vector2(360, 80), OnChoiceB);
        choiceLabelA = choiceButtonA.GetComponentInChildren<TextMeshProUGUI>();
        choiceLabelB = choiceButtonB.GetComponentInChildren<TextMeshProUGUI>();

        choicePanel = go;
        choicePanel.SetActive(false);
    }

    // ---------- 剧情对话（打字机） ----------

    /// <summary>建对话面板（底部半透明黑条 + 打字机文字 + 继续提示），盖在 HUD 上面。</summary>
    void BuildDialogue()
    {
        if (canvas == null) EnsureCanvas();

        dialogueRoot = new GameObject("Dialogue", typeof(RectTransform));
        dialogueRoot.transform.SetParent(canvas.transform, false);
        var rt = dialogueRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        dialogueRoot.transform.SetAsLastSibling(); // 盖在 HUD / 面板之上

        var panelImg = CreateImage(dialogueRoot.transform, "Panel", new Color(0.02f, 0.02f, 0.04f, 0.88f),
            new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1640, 250));
        panelImg.raycastTarget = false;
        if (dialogueBoxSprite != null)
        {
            panelImg.sprite = dialogueBoxSprite; // 用美术替换默认黑条
            panelImg.color = Color.white;        // 美术原样显示，不再染色
        }

        dialogueText = CreateText(dialogueRoot.transform, "Text", "", 32,
            new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(1520, 130), TextAlignmentOptions.Center);
        // 没拖浅色对话框美术时用默认深色黑条 → 文字用白色；拖了浅色美术才用配置的黑色
        dialogueText.color = (dialogueBoxSprite != null) ? dialogueTextColor : Color.white;

        dialogueHint = CreateText(dialogueRoot.transform, "Hint", "空格 / 点击 跳过", 20,
            new Vector2(0.5f, 0), new Vector2(0, 45), new Vector2(500, 40), TextAlignmentOptions.Center);
        dialogueHint.color = (dialogueBoxSprite != null) ? dialogueHintColor : new Color(1f, 1f, 1f, 0.6f);

        dialogueRoot.SetActive(false);
    }

    /// <summary>播放一段剧情对话：逐字打出，暂停游戏，空格/回车/点击继续，结束恢复。</summary>
    public void PlayDialogue(string[] lines, System.Action onDone = null)
    {
        if (lines == null || lines.Length == 0) { onDone?.Invoke(); return; }
        if (dialogueRoot == null) BuildDialogue();

        // 重入保护：若上一段对话还没结束而再次触发，先强制收尾，避免覆盖时间缩放状态、
        // 导致对话结束后游戏卡死或对话框残留。这样多段对话（开场 + NPC + 剧情叠加）也稳。
        if (dialogueActive)
            EndDialogue();

        dialogueLines = lines;
        dialogueOnDone = onDone;
        dialogueActive = true;
        DialogueActive = true;
        dialogueRoot.SetActive(true);

        dialoguePrevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        dialogueSafetyTimer = 0f;
        StartLine(0);
    }

    void StartLine(int i)
    {
        dialogueIndex = i;
        dialogueVisibleChars = 0;
        dialogueCharTimer = 0f;
        dialogueIdleTimer = 0f;
        dialogueTyping = true;
        if (dialogueHint != null) dialogueHint.gameObject.SetActive(false);
        // 每行开始打字时，打字音循环响 1 秒后自动停
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTypingLoop(TypingSoundSeconds);
    }

    void UpdateDialogue()
    {
        if (!dialogueActive || dialogueLines == null || dialogueIndex >= dialogueLines.Length) return;
        string full = dialogueLines[dialogueIndex];

        // 兜底：整段对话超过上限强制关闭，绝不让对话框一直挂着
        dialogueSafetyTimer += Time.unscaledDeltaTime;
        if (dialogueSafetyTimer >= DialogueMaxDuration) { EndDialogue(); return; }

        // 按空格 / 回车 / 点击任意一处：直接跳过整段对话（立刻关闭对话框）
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetMouseButtonDown(0))
        {
            EndDialogue();
            return;
        }

        if (dialogueTyping)
        {
            dialogueCharTimer += Time.unscaledDeltaTime * DialogueCharsPerSecond;
            int target = Mathf.Min(full.Length, Mathf.FloorToInt(dialogueCharTimer));
            if (target > dialogueVisibleChars)
            {
                dialogueVisibleChars = target;
                if (dialogueText != null) dialogueText.text = full.Substring(0, dialogueVisibleChars);
            }
            if (dialogueVisibleChars >= full.Length)
            {
                dialogueTyping = false;
                if (dialogueHint != null) dialogueHint.gameObject.SetActive(true);
            }
        }
        else
        {
            // 打完一行后自动切下一行；最后一行自动关闭，避免空对话框一直挡着
            dialogueIdleTimer += Time.unscaledDeltaTime;
            if (dialogueIdleTimer >= DialogueAutoAdvanceDelay)
            {
                if (dialogueIndex < dialogueLines.Length - 1)
                    StartLine(dialogueIndex + 1);
                else
                    EndDialogue();
            }
        }
    }

    void EndDialogue()
    {
        dialogueActive = false;
        DialogueActive = false;
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.StopTyping();
        Time.timeScale = dialoguePrevTimeScale;
        var cb = dialogueOnDone;
        dialogueOnDone = null;
        cb?.Invoke();
    }

    // ---------- 构建 ----------

    TMP_FontAsset Font => fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;

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
    private static Sprite _whiteSprite;

    void EnsureCanvas()
    {
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            return;
        }

        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = cgo.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
    }

    void BuildHUD()
    {
        if (canvas == null) return;

        hudRoot = new GameObject("HUD", typeof(RectTransform));
        hudRoot.transform.SetParent(canvas.transform, false);
        var hudRt = hudRoot.GetComponent<RectTransform>();
        hudRt.anchorMin = Vector2.zero;
        hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = Vector2.zero;
        hudRoot.transform.SetAsFirstSibling(); // HUD 放最下面，让开始/结算面板盖住它

        Transform c = hudRoot.transform;

        // 危险覆盖层（屏幕边缘泛红，放在最底层避免遮住文字；由 SetDanger/PulseAlert 控制透明度）
        dangerOverlay = CreateImage(c, "DangerVignette", new Color(1f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dangerOverlay.sprite = ColorBlockFactory.VignetteSprite;
        dangerOverlay.raycastTarget = false;
        var drt = dangerOverlay.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;

        // HP 条（左上）
        CreateImage(c, "HPBg", new Color(0, 0, 0, 0.6f), new Vector2(0, 1), new Vector2(30, -30), new Vector2(260, 30));
        hpFill = CreateImage(c, "HPFill", new Color(0.95f, 0.35f, 0.35f), new Vector2(0, 1), new Vector2(33, -33), new Vector2(254, 24));
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillAmount = 1f;

        hpText = CreateText(c, "HPText", "HP 100 / 100", 22, new Vector2(0, 1), new Vector2(30, -62), new Vector2(260, 30), TextAlignmentOptions.TopLeft);

        // 警报等级（顶部中间）
        alertText = CreateText(c, "AlertText", "警报等级 0", 26, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(400, 40), TextAlignmentOptions.Top);

        // 方糖计数（右上角小地图左侧，小地图占了右上角）
        sugarText = CreateText(c, "SugarText", "方糖 0 / 0", 24, new Vector2(1, 1), new Vector2(-230, -30), new Vector2(210, 40), TextAlignmentOptions.TopRight);

        // 击杀数（右上角，方糖下方）
        killText = CreateText(c, "KillText", "击杀 0", 24, new Vector2(1, 1), new Vector2(-230, -70), new Vector2(210, 40), TextAlignmentOptions.TopRight);

        // 底部操作提示（解锁块数跟随 Player 上的 PlayerStats.attackUnlockCount）
        int unlockCount = (GameManager.Instance != null && GameManager.Instance.player != null && GameManager.Instance.player.stats != null)
            ? GameManager.Instance.player.stats.attackUnlockCount : 2;
        hintText = CreateText(c, "HintText", "WASD 移动 | 靠近方糖长按 E 吸收 | 空格/左键 攻击（吸收 " + unlockCount + " 块后解锁）", 20,
            new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1600, 40), TextAlignmentOptions.Bottom);
        hintText.color = new Color(1, 1, 1, 0.6f);

        // 中间提示消息
        messageText = CreateText(c, "MessageText", "", 30, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1200, 80), TextAlignmentOptions.Center);
        messageText.color = new Color(1f, 0.9f, 0.4f);

        BuildMinimap();
        BuildDialogue();
    }

    // ---------- 小地图 ----------

    void BuildMinimap()
    {
        var rootGo = new GameObject("Minimap", typeof(RectTransform));
        rootGo.transform.SetParent(hudRoot.transform, false);
        minimapRoot = rootGo.GetComponent<RectTransform>();
        minimapRoot.anchorMin = new Vector2(1, 1);
        minimapRoot.anchorMax = new Vector2(1, 1);
        minimapRoot.pivot = new Vector2(1, 1);
        minimapRoot.anchoredPosition = new Vector2(-16, -16);
        minimapRoot.sizeDelta = new Vector2(MinimapSize, MinimapSize);

        minimapBg = CreateImage(minimapRoot, "Bg", new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MinimapSize, MinimapSize));
        minimapBg.raycastTarget = false;

        var dotGo = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(minimapRoot, false);
        playerDot = dotGo.GetComponent<Image>();
        playerDot.sprite = WhiteSprite;
        playerDot.color = new Color(1f, 1f, 1f, 1f);
        playerDot.raycastTarget = false;
        var dotRt = playerDot.rectTransform;
        dotRt.anchorMin = Vector2.zero;
        dotRt.anchorMax = Vector2.zero;
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.sizeDelta = new Vector2(10, 10);
    }

    /// <summary>房间块在 GameManager 填好 roomCenters 后延迟创建（避免 Start 顺序问题）。</summary>
    void EnsureMinimapRooms()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.roomCenters == null || gm.roomCenters.Count == 0) return;
        if (minimapRoomsBuilt) return;
        minimapRoomsBuilt = true;

        float half = gm.roomHalfSize;
        minimapWorldMin = new Vector2(float.MaxValue, float.MaxValue);
        minimapWorldMax = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in gm.roomCenters)
        {
            minimapWorldMin = Vector2.Min(minimapWorldMin, c - new Vector2(half, half));
            minimapWorldMax = Vector2.Max(minimapWorldMax, c + new Vector2(half, half));
        }
        float innerW = MinimapSize - MinimapPad * 2f;
        float innerH = MinimapSize - MinimapPad * 2f;
        float worldW = Mathf.Max(0.001f, minimapWorldMax.x - minimapWorldMin.x);
        float worldH = Mathf.Max(0.001f, minimapWorldMax.y - minimapWorldMin.y);
        minimapScale = Mathf.Min(innerW / worldW, innerH / worldH);
        minimapLocalCenter = new Vector2(MinimapSize * 0.5f, MinimapSize * 0.5f);

        // 房间之间的连线（先画，让房间方块盖住线端点，只在房间之间露出连线）
        for (int i = 0; i < gm.roomCenters.Count - 1; i++)
            minimapLines.Add(CreateMinimapLink(gm.roomCenters[i], gm.roomCenters[i + 1]));

        for (int i = 0; i < gm.roomCenters.Count; i++)
        {
            var img = CreateImage(minimapRoot, "Room_" + i, RoomColor(i), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float s = half * 2f * minimapScale;
            rt.sizeDelta = new Vector2(s, s);
            roomImages.Add(img);
        }
        if (playerDot != null) playerDot.transform.SetAsLastSibling(); // 玩家点盖在房间块上
    }

    Color RoomColor(int i)
    {
        var gm = GameManager.Instance;
        if (gm != null && i == gm.portalRoomIndex) return new Color(0.4f, 0.8f, 0.5f, 0.85f); // 传送门房绿
        return new Color(0.4f, 0.45f, 0.55f, 0.85f); // 普通房灰蓝
    }

    Vector2 WorldToMinimapLocal(Vector2 w)
    {
        Vector2 worldCenter = (minimapWorldMin + minimapWorldMax) * 0.5f;
        return minimapLocalCenter + (w - worldCenter) * minimapScale;
    }

    /// <summary>在两个房间中心之间画一条细线，标出它们相连。</summary>
    Image CreateMinimapLink(Vector2 worldA, Vector2 worldB)
    {
        var img = CreateImage(minimapRoot, "Link", new Color(0.95f, 0.9f, 0.5f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 a = WorldToMinimapLocal(worldA);
        Vector2 b = WorldToMinimapLocal(worldB);
        Vector2 mid = (a + b) * 0.5f;
        float len = Vector2.Distance(a, b);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(len, 4f); // 4px 粗的连线
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        return img;
    }

    void UpdateMinimap()
    {
        var gm = GameManager.Instance;
        if (gm == null || minimapRoot == null) return;
        EnsureMinimapRooms();
        if (roomImages.Count == 0) return;

        Vector2 p = gm.PlayerPosition;
        if (playerDot != null) playerDot.rectTransform.anchoredPosition = WorldToMinimapLocal(p);

        int cur = -1;
        float half = gm.roomHalfSize;
        for (int i = 0; i < gm.roomCenters.Count; i++)
        {
            Vector2 c = gm.roomCenters[i];
            if (Mathf.Abs(p.x - c.x) <= half && Mathf.Abs(p.y - c.y) <= half) { cur = i; break; }
        }
        for (int i = 0; i < roomImages.Count; i++)
        {
            var img = roomImages[i];
            if (img == null) continue;
            img.color = (i == cur) ? new Color(1f, 0.85f, 0.35f, 0.95f) : RoomColor(i);
            img.rectTransform.anchoredPosition = WorldToMinimapLocal(gm.roomCenters[i]);
        }
    }

    void ApplyFont()
    {
        if (fontAsset == null) return;
        if (hpText != null) hpText.font = fontAsset;
        if (alertText != null) alertText.font = fontAsset;
        if (sugarText != null) sugarText.font = fontAsset;
        if (killText != null) killText.font = fontAsset;
        if (hintText != null) hintText.font = fontAsset;
        if (messageText != null) messageText.font = fontAsset;
        if (dialogueText != null) dialogueText.font = fontAsset;
        if (dialogueHint != null) dialogueHint.font = fontAsset;
        if (endingTitle != null) endingTitle.font = fontAsset;
        if (endingDesc != null) endingDesc.font = fontAsset;
        if (clearTitle != null) clearTitle.font = fontAsset;
        if (clearDesc != null) clearDesc.font = fontAsset;
        ApplyFontToButton(startButton);
        ApplyFontToButton(quitButton);
        ApplyFontToButton(restartButton);
        ApplyFontToButton(nextButton);
    }

    void ApplyFontToButton(Button b)
    {
        if (b == null || fontAsset == null) return;
        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.font = fontAsset;
    }

    // ---------- 辅助 ----------

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

    Image CreateImage(Transform parent, string name, Color color, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetupRect(go, anchor, anchoredPos, size);
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        return img;
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

    // ---------- 选关暂停菜单（Esc） ----------

    [System.Serializable]
    public class LevelEntry
    {
        [Tooltip("关卡显示名（按钮文字）")]
        public string displayName = "第一关";
        [Tooltip("关卡场景名（需已加入 Build Settings）")]
        public string sceneName = "Level_1";
    }

    List<LevelEntry> DefaultLevels()
    {
        var list = new List<LevelEntry>();
        for (int i = 1; i <= 3; i++)
            list.Add(new LevelEntry { displayName = "第" + LevelCN[i] + "关", sceneName = "Level_" + i });
        return list;
    }

    /// <summary>UI 按钮需要 EventSystem；场景里若没手动摆，运行时自动补。</summary>
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>建「选择关卡」暂停菜单（默认隐藏，游戏中按 Esc 弹出）。</summary>
    void BuildLevelSelectPanel()
    {
        if (canvas == null) EnsureCanvas();

        selectPanel = new GameObject("LevelSelect", typeof(RectTransform));
        selectPanel.transform.SetParent(canvas.transform, false);
        var rt = selectPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        selectPanel.transform.SetAsLastSibling();

        // 全屏暗色遮罩（挡住下层 + 挡住点击穿透）
        var dim = CreateImage(selectPanel.transform, "Dim", new Color(0f, 0f, 0f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dim.raycastTarget = true;
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;

        CreateText(selectPanel.transform, "Title", "选择关卡", 56, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(800, 80), TextAlignmentOptions.Center);

        var list = (selectLevels != null && selectLevels.Count > 0) ? selectLevels : DefaultLevels();
        float startY = 120f;
        float step = 100f;
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            float y = startY - i * step;
            CreateButton(selectPanel.transform, "LevelBtn_" + i, entry.displayName,
                new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 80),
                () => LoadLevelFromSelect(entry.sceneName));
        }
        CreateButton(selectPanel.transform, "ResumeBtn", "继续游戏", new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(240, 64), CloseLevelSelect);

        selectPanel.SetActive(false);
    }

    void OpenLevelSelect()
    {
        if (selectPanel == null) BuildLevelSelectPanel();
        if (selectOpen) return;
        selectOpen = true;
        selectPrevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        selectPanel.SetActive(true);
    }

    void CloseLevelSelect()
    {
        if (!selectOpen) return;
        selectOpen = false;
        Time.timeScale = selectPrevTimeScale;
        if (selectPanel != null) selectPanel.SetActive(false);
    }

    void LoadLevelFromSelect(string sceneName)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        Time.timeScale = 1f;
        LevelFlow.skipStartPanel = true; // 选关进入后直接开玩（跳过该关开始界面）
        SceneManager.LoadScene(sceneName);
    }

    Button CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
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
