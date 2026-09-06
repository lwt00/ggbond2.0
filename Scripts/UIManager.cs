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
    [Tooltip("开始界面根物体（含 Play / 开始 / 退出 三个按钮）")]
    public GameObject startPanel;
    [Tooltip("「Play」按钮（最先显示；点它播开场视频，播完再显示开始/退出）")]
    public Button playButton;
    [Tooltip("「开始」按钮（视频播完后显示；点它真正开始游戏）")]
    public Button startButton;
    [Tooltip("「退出游戏」按钮（视频播完后显示）")]
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

    [Header("开场视频（点「Play」后播放，留空 = 跳过）")]
    [Tooltip("第 1 段：4 秒开场动画（点 Play 后播，屏幕干净）。留空 = 跳过")]
    public VideoClip introVideo;
    [Tooltip("是否播放第 1 段开场视频（勾掉 = 跳过）")]
    public bool playIntroVideo = true;
    [Tooltip("第 2 段：循环背景视频（第 1 段播完后，在开始界面后面循环播放，直到点「开始游戏」）。留空 = 无背景")]
    public VideoClip loopVideo;
    [Tooltip("第 3 段：点「开始游戏」后播放的过场视频（几秒，播完进游戏）。留空 = 跳过")]
    public VideoClip gameStartVideo;
    [Tooltip("显示视频的 RawImage（可自己拖，放在按钮下面做背景）。留空 = 运行时自动建一个全屏的")]
    public RawImage videoRawImage;

    [Header("结局演出（结局一视频 / 结局二图片；留空 = 直接出结局面板）")]
    [Tooltip("结局一：YES 分支（杀光守卫后进传送门）后播放的视频")]
    public VideoClip endingVideo1;
    [Tooltip("结局二：NO 分支（不打怪直接进传送门）后显示的图片（CG）")]
    public Sprite endingImage2;

    [Header("危险警示（屏幕边缘泛红）")]
    [Tooltip("敌人逼近/警报升级时，屏幕边缘泛红的最大不透明度（0~1）")]
    public float dangerMaxAlpha = 0.45f;

    // HUD（自动生成）
    private GameObject hudRoot;
    private Image hpFill;
    private TextMeshProUGUI hpText, alertText, sugarText, killText, hintText, messageText;
    private float messageTimer;
    private bool endingShown;

    // 开场视频（点「Play」后播；留空 = 跳过）
    private enum StartStage { Menu, Intro, Loop, GameIntro, Playing }
    private StartStage startStage = StartStage.Menu; // 开始流程阶段

    private VideoPlayer introPlayer;
    private RawImage introVideoImage;
    private Image introCoverOverlay;   // 黑色全屏遮罩（盖住地图 + 所有 UI，视频下方不露出东西）
    private bool introVideoPlaying;    // 当前是否在播「会结束」的视频（第 1/3 段）
    private float introSkipGuard;
    private float introTimeout;     // 视频播放兜底计时：超时自动结束，防止视频卡住一直黑屏
    private Coroutine introCo;      // 视频 prepare→play 协程
    private bool gameplayStarted;   // 本场景是否已经真正开始游戏（防止重复 StartLevel）

    // 结局视频（复用 introPlayer / introVideoImage / introCoverOverlay 那套视频层）
    private bool endingVideoPlaying;      // 是否在播结局视频
    private System.Action endingVideoOnDone; // 结局视频播完后的回调（显示结局面板）
    private float endingSkipGuard;        // 前 0.5 秒忽略跳过，避免进门按的 E 立刻把视频跳掉
    private float endingTimeout;          // 兜底计时：超时自动结束，防视频卡住一直黑屏

    // 结局图片（结局二用静态 CG；复用 introCoverOverlay 的黑色遮罩）
    private bool endingImagePlaying;       // 是否在显示结局图片
    private System.Action endingImageOnDone; // 图片看完后的回调（显示结局面板）
    private float endingImageGuard;        // 前 0.5 秒忽略跳过，避免误触
    private float endingImageTimeout;      // 兜底计时
    private Image endingImageOverlay;      // 全屏图片 CG 层

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
        if (startPanel != null) startPanel.SetActive(false); // 等第 1 段视频播完再显示
        if (endingPanel != null) endingPanel.SetActive(false);
        if (clearPanel != null) clearPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);

        // 开始界面等第 1 段视频播完才显示：一开始先藏起来，等点 Unity ▶ Play 后自动播开场
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (quitButton != null) quitButton.gameObject.SetActive(false);

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
        else
        {
            // 点 Unity ▶ Play 后：隐藏一切，自动播第 1 段开场视频（屏幕干净）
            PlayIntroVideo();
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

        // 第 1/3 段视频播放中：0.3 秒后任意键/点击跳过；超 15 秒强制结束（防视频卡住一直黑屏）
        if (introVideoPlaying)
        {
            introTimeout += Time.unscaledDeltaTime;
            if (introSkipGuard > 0f) introSkipGuard -= Time.unscaledDeltaTime;
            else if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || introTimeout > 15f) OnTimedVideoEnd(introPlayer);
        }

        // 结局视频播放中：0.5 秒后任意键/点击跳过；超 30 秒强制结束（防视频卡住一直黑屏）
        if (endingVideoPlaying)
        {
            endingTimeout += Time.unscaledDeltaTime;
            if (endingSkipGuard > 0f) endingSkipGuard -= Time.unscaledDeltaTime;
            else if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || endingTimeout > 30f) OnEndingVideoEnd();
        }

        // 结局图片显示中：0.5 秒后任意键/点击关闭；超 30 秒自动关闭
        if (endingImagePlaying)
        {
            endingImageTimeout += Time.unscaledDeltaTime;
            if (endingImageGuard > 0f) endingImageGuard -= Time.unscaledDeltaTime;
            else if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || endingImageTimeout > 30f) OnEndingImageEnd();
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

    /// <summary>点 Unity ▶ Play 后自动调用：隐藏一切（遮罩盖地图），播第 1 段开场视频。</summary>
    void PlayIntroVideo()
    {
        // 隐藏所有 UI：开始界面、HUD（地图由黑色遮罩盖住）
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(false);
        if (introPlayer == null) BuildIntroVideo();

        // 第 1 段：开场视频（有才播，播完进循环阶段）
        if (introVideo != null && playIntroVideo && !LevelFlow.introPlayed)
        {
            LevelFlow.introPlayed = true;
            startStage = StartStage.Intro;
            PlayTimedVideo(introVideo);
        }
        else
        {
            EnterLoopStage();
        }
    }

    /// <summary>点「开始游戏」：隐藏开始界面和循环背景，播第 3 段过场视频 → 进游戏。</summary>
    void OnStartGame()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

        if (startPanel != null) startPanel.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (quitButton != null) quitButton.gameObject.SetActive(false);
        if (introPlayer != null) introPlayer.Stop();

        if (gameStartVideo != null)
        {
            startStage = StartStage.GameIntro;
            PlayTimedVideo(gameStartVideo);
        }
        else
        {
            StartGameplay();
        }
    }

    /// <summary>播一段「会结束」的视频（第 1 段开场 / 第 3 段过场）。结束由 loopPointReached → OnTimedVideoEnd。</summary>
    void PlayTimedVideo(VideoClip clip)
    {
        if (introPlayer == null) BuildIntroVideo();
        Time.timeScale = 0f;                              // 视频期间暂停游戏
        if (hudRoot != null) hudRoot.SetActive(false);
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(true);
        if (introVideoImage != null)
        {
            introVideoImage.gameObject.SetActive(true);
            introVideoImage.transform.SetAsLastSibling(); // 视频盖在遮罩上面
        }
        introPlayer.clip = clip;
        introPlayer.isLooping = false;                    // 播一遍就停
        introVideoPlaying = true;
        introSkipGuard = 0.3f;                            // 前 0.3 秒忽略跳过，避免误点
        introTimeout = 0f;

        if (introCo != null) StopCoroutine(introCo);
        introCo = StartCoroutine(TimedPlayRoutine());
    }

    /// <summary>等视频准备好后播放；准备失败/超时则直接结束，绝不卡屏。</summary>
    System.Collections.IEnumerator TimedPlayRoutine()
    {
        introPlayer.Prepare();
        float t = 0f;
        while (!introPlayer.isPrepared && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!introVideoPlaying) yield break;   // 准备期间被跳过

        if (introPlayer.isPrepared)
            introPlayer.Play();
        else
            OnTimedVideoEnd(introPlayer);      // 准备失败：直接结束，不卡屏
    }

    /// <summary>第 1/3 段视频播完（或被跳过）后：按当前阶段走下一步。</summary>
    void OnTimedVideoEnd(VideoPlayer vp)
    {
        // 结局视频播完（或被跳过）：走结局流程，别跟开场流程混淆
        if (endingVideoPlaying) { OnEndingVideoEnd(); return; }
        if (!introVideoPlaying) return;
        introVideoPlaying = false;
        if (introPlayer != null) introPlayer.Stop();

        if (startStage == StartStage.Intro)
            EnterLoopStage();
        else if (startStage == StartStage.GameIntro)
            StartGameplay();
    }

    /// <summary>进入「循环背景 + 开始界面」阶段：显示 startPanel，第 2 段视频循环播放。</summary>
    void EnterLoopStage()
    {
        startStage = StartStage.Loop;
        Time.timeScale = 0f;   // 开始界面停留时暂停游戏

        // 关遮罩：循环视频（或地图）自己作为开始界面背景
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(false);

        // 第 2 段：循环背景视频（有才播；isLooping 不会触发 loopPointReached）
        if (loopVideo != null)
        {
            if (introPlayer == null) BuildIntroVideo();
            if (introVideoImage != null)
            {
                introVideoImage.gameObject.SetActive(true);
                introVideoImage.transform.SetAsLastSibling();
            }
            introPlayer.clip = loopVideo;
            introPlayer.isLooping = true;   // 循环播放
            if (introCo != null) StopCoroutine(introCo);
            introCo = StartCoroutine(LoopPlayRoutine());
        }
        else if (introVideoImage != null)
        {
            introVideoImage.gameObject.SetActive(false);
        }

        ShowMenuButtons();
    }

    System.Collections.IEnumerator LoopPlayRoutine()
    {
        introPlayer.Prepare();
        float t = 0f;
        while (!introPlayer.isPrepared && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (introPlayer.isPrepared)
            introPlayer.Play();
    }

    /// <summary>显示「开始游戏」「退出游戏」按钮（盖在循环视频上面），隐藏 Play。</summary>
    void ShowMenuButtons()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(true);
            startPanel.transform.SetAsLastSibling(); // 按钮盖在循环视频上面
        }
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(true);
        if (quitButton != null) quitButton.gameObject.SetActive(true);
    }

    /// <summary>真正开始游戏：隐藏视频/遮罩/开始界面，显示 HUD + 地图，起 BGM。</summary>
    void StartGameplay()
    {
        if (gameplayStarted) return; // 防止视频结束 + 跳过 双重触发
        gameplayStarted = true;
        introVideoPlaying = false;
        startStage = StartStage.Playing;

        if (startPanel != null) startPanel.SetActive(false);
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (quitButton != null) quitButton.gameObject.SetActive(false);
        if (introPlayer != null) introPlayer.Stop();
        if (introVideoImage != null) introVideoImage.gameObject.SetActive(false);
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(true);
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.StartLevel();
    }

    /// <summary>运行时搭视频层：黑色遮罩盖地图 + 视频层（优先用你拖的 videoRawImage，否则自动建一个全屏的）。</summary>
    void BuildIntroVideo()
    {
        if (canvas == null) EnsureCanvas();

        // 黑色全屏遮罩：盖住地图 + 所有 UI（HUD / 对话框 / 开始界面），保证视频下方不露出任何东西
        if (introCoverOverlay == null)
        {
            var coverGo = new GameObject("IntroCover", typeof(RectTransform), typeof(Image));
            coverGo.transform.SetParent(canvas.transform, false);
            var wrt = coverGo.GetComponent<RectTransform>();
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = Vector2.zero;
            wrt.offsetMax = Vector2.zero;
            coverGo.transform.SetAsLastSibling();
            introCoverOverlay = coverGo.GetComponent<Image>();
            introCoverOverlay.color = Color.black; // 纯黑不透明
        }

        // 视频层：优先用你拖的 RawImage，否则自动建一个全屏的
        if (introVideoImage == null)
        {
            if (videoRawImage != null)
            {
                introVideoImage = videoRawImage;
                introVideoImage.color = Color.white;
                introPlayer = videoRawImage.gameObject.GetComponent<VideoPlayer>();
                if (introPlayer == null) introPlayer = videoRawImage.gameObject.AddComponent<VideoPlayer>();
            }
            else
            {
                var go = new GameObject("IntroVideo", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.transform.SetAsLastSibling(); // 视频盖在遮罩上面
                introVideoImage = go.GetComponent<RawImage>();
                introVideoImage.color = Color.white;
                introPlayer = go.AddComponent<VideoPlayer>();
            }

            introPlayer.playOnAwake = false;
            introPlayer.isLooping = false;
            introPlayer.renderMode = VideoRenderMode.RenderTexture;
            introPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            introPlayer.targetTexture = new RenderTexture(1920, 1080, 0);
            introPlayer.loopPointReached += OnTimedVideoEnd;
            introVideoImage.texture = introPlayer.targetTexture;
        }

        introCoverOverlay.gameObject.SetActive(false);
        if (introVideoImage != null) introVideoImage.gameObject.SetActive(false);
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

    /// <summary>播结局：有视频就先播视频（播完/跳过 → 结局面板），没视频直接出结局面板。</summary>
    public void PlayEnding(VideoClip clip, string title, string desc)
    {
        if (clip == null) { ShowEnding(title, desc); return; }

        if (introPlayer == null) BuildIntroVideo();
        Time.timeScale = 0f;                          // 视频期间暂停游戏
        if (hudRoot != null) hudRoot.SetActive(false);
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(true);
        if (introVideoImage != null)
        {
            introVideoImage.gameObject.SetActive(true);
            introVideoImage.transform.SetAsLastSibling(); // 视频盖在遮罩上面
        }

        introPlayer.clip = clip;
        introPlayer.isLooping = false;                // 播一遍就停
        endingVideoPlaying = true;
        endingSkipGuard = 0.5f;                       // 前 0.5 秒忽略跳过，避免进门按的 E 误跳
        endingTimeout = 0f;
        endingVideoOnDone = () => ShowEnding(title, desc);

        if (introCo != null) StopCoroutine(introCo);
        introCo = StartCoroutine(EndingPlayRoutine());
    }

    /// <summary>等结局视频准备好后播放；准备失败/超时则直接结束，绝不卡屏。</summary>
    System.Collections.IEnumerator EndingPlayRoutine()
    {
        introPlayer.Prepare();
        float t = 0f;
        while (!introPlayer.isPrepared && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!endingVideoPlaying) yield break;         // 准备期间被跳过

        if (introPlayer.isPrepared)
            introPlayer.Play();
        else
            OnEndingVideoEnd();                        // 准备失败：直接结束，不卡屏
    }

    /// <summary>结局视频结束（或跳过/超时）：收起视频层 → 显示结局面板。</summary>
    void OnEndingVideoEnd()
    {
        if (!endingVideoPlaying) return;
        endingVideoPlaying = false;
        if (introPlayer != null) introPlayer.Stop();
        if (introVideoImage != null) introVideoImage.gameObject.SetActive(false);
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(false);
        var cb = endingVideoOnDone; endingVideoOnDone = null;
        cb?.Invoke();
    }

    /// <summary>播结局二（静态图 CG）：显示全屏图 → 任意键/点击（或超时）→ 结局面板。没图直接出面板。</summary>
    public void PlayEndingImage(Sprite sprite, string title, string desc)
    {
        if (sprite == null) { ShowEnding(title, desc); return; }

        if (endingImageOverlay == null) BuildEndingImageOverlay();
        Time.timeScale = 0f;                          // 图片期间暂停游戏
        if (hudRoot != null) hudRoot.SetActive(false);
        if (introCoverOverlay == null) BuildIntroVideo(); // 借黑色遮罩盖住地图
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(true);

        endingImageOverlay.sprite = sprite;
        endingImageOverlay.gameObject.SetActive(true);
        endingImageOverlay.transform.SetAsLastSibling(); // 图片盖在遮罩上面

        endingImagePlaying = true;
        endingImageGuard = 0.5f;                      // 前 0.5 秒忽略跳过，避免进门按的 E 误关
        endingImageTimeout = 0f;
        endingImageOnDone = () => ShowEnding(title, desc);
    }

    /// <summary>运行时搭一张全屏 UI Image（结局二图片 CG 层）。</summary>
    void BuildEndingImageOverlay()
    {
        if (canvas == null) EnsureCanvas();
        var go = new GameObject("EndingImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        endingImageOverlay = go.GetComponent<Image>();
        endingImageOverlay.color = Color.white;
        endingImageOverlay.raycastTarget = true;
        endingImageOverlay.gameObject.SetActive(false);
    }

    /// <summary>结局图片看完（或跳过/超时）：收起图片层 → 显示结局面板。</summary>
    void OnEndingImageEnd()
    {
        if (!endingImagePlaying) return;
        endingImagePlaying = false;
        if (endingImageOverlay != null) endingImageOverlay.gameObject.SetActive(false);
        if (introCoverOverlay != null) introCoverOverlay.gameObject.SetActive(false);
        var cb = endingImageOnDone; endingImageOnDone = null;
        cb?.Invoke();
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

    /// <summary>剧情对话框已停用（对话/抉择将重做）：不再弹对话框、不再暂停游戏，直接触发结束回调。</summary>
    public void PlayDialogue(string[] lines, System.Action onDone = null)
    {
        onDone?.Invoke();
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
