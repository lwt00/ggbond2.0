using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>一句对话里的说话人：对应两位立绘 A / B（谁说话谁亮）。</summary>
public enum DialogueSpeaker { A, B }

/// <summary>
/// 一句剧情对话：谁在说 + 内容。
/// 在 Inspector 里逐条填（NPC 的对话、LevelData 的 npcDialogue 都是这种列表），可视化配置剧情。
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("这句话由谁来说（决定哪张立绘变亮置顶、另一张压暗）")]
    public DialogueSpeaker speaker = DialogueSpeaker.A;

    [Tooltip("说话人名字（显示在名字框，可留空）")]
    public string speakerName = "";

    [Tooltip("这句话内容（支持多行）")]
    [TextArea(2, 5)]
    public string text = "";
}

/// <summary>
/// 剧情对话框面板（新版，面板 UI 由你自己在 Hierarchy 里搭）：
/// 触发后暂停游戏、显示面板，空格/回车/鼠标左键翻下一句，最后一句后自动关闭恢复。
/// 两位立绘：谁在说话谁恢复「正常亮度」并提到最上层，另一位压暗（dimColor）沉到下面。
/// 用法：把本组件挂在你的面板根节点上，再把 panelRoot / dialogueText / nameText / portraitA / portraitB
///       拖进下面的字段；然后由 NPC 按 E、或后续内心独白/第三关剧情调用 DialoguePanel.Instance.Play(lines)。
/// </summary>
public class DialoguePanel : MonoBehaviour
{
    private static DialoguePanel _instance;
    /// <summary>全局单例。惰性查找：就算面板物体一开始被隐藏（Awake 没跑成），也能找到，避免报「没挂组件」。</summary>
    public static DialoguePanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<DialoguePanel>(true);
            return _instance;
        }
    }
    /// <summary>是否正在播对话（PlayerController 等据此禁用攻击）。</summary>
    public static bool Active { get; private set; }

    [Header("面板（自己在 Hierarchy 里搭好，拖进来）")]
    [Tooltip("整个面板根节点（含透明黑背景 + 立绘 + 对话框），播剧情时显隐它")]
    public GameObject panelRoot;

    [Header("对话框文字")]
    [Tooltip("对话框文字（TextMeshPro）")]
    public TextMeshProUGUI dialogueText;
    [Tooltip("说话人名字（TextMeshPro，可留空则名字框不更新）")]
    public TextMeshProUGUI nameText;
    [Tooltip("正文/名字的颜色（默认白色；对话框背景偏暗时务必用亮色，否则黑字看不见）")]
    public Color textColor = Color.white;

    [Header("两位立绘（谁说话谁亮/在上，另一个变暗/在下）")]
    [Tooltip("立绘 A（Image）")]
    public Image portraitA;
    [Tooltip("立绘 B（Image）")]
    public Image portraitB;
    [Tooltip("非说话者压暗后的颜色（建议半透明黑，如 (0.35, 0.35, 0.35, 1)）")]
    public Color dimColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("交互")]
    [Tooltip("翻下一句 / 结束 的按键（默认空格）")]
    public KeyCode advanceKey = KeyCode.Space;
    [Tooltip("是否鼠标左键也能翻页")]
    public bool clickToAdvance = true;

    private DialogueLine[] lines;
    private int index;
    private float prevTimeScale;
    private Color normalA = Color.white;
    private Color normalB = Color.white;

    void Awake()
    {
        _instance = this;
        Active = false;
        // 隐藏面板（如果 panelRoot 拖的是自己，会把自己一起隐藏；下次进场景 Instance 由上面的惰性查找兜底，不会丢）。
        if (panelRoot != null) panelRoot.SetActive(false);
        // 记下立绘「正常亮度」的初始颜色：说话者恢复成它，非说话者压暗成 dimColor。
        // 所以 Inspector 里请把立绘摆成「正常亮」的样子。
        if (portraitA != null) normalA = portraitA.color;
        if (portraitB != null) normalB = portraitB.color;
    }

    void OnDestroy()
    {
        if (_instance == this) { _instance = null; Active = false; }
    }

    /// <summary>播放一段对话：暂停游戏、显示面板、从第一句开始。</summary>
    public void Play(DialogueLine[] newLines)
    {
        if (newLines == null || newLines.Length == 0) return;
        lines = newLines;
        index = 0;
        Active = true;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (panelRoot != null) panelRoot.SetActive(true);
        ValidateWiring(); // 第一次播时把「文字不见了」的原因打到 Console，方便你定位
        ShowLine(0);
    }

    void Update()
    {
        if (!Active) return;
        if (Input.GetKeyDown(advanceKey) || Input.GetKeyDown(KeyCode.Return) ||
            (clickToAdvance && Input.GetMouseButtonDown(0)))
            Next();
    }

    void Next()
    {
        index++;
        if (index >= lines.Length) { Close(); return; }
        ShowLine(index);
    }

    void ShowLine(int i)
    {
        var line = lines[i];
        if (dialogueText != null) { dialogueText.text = line.text; dialogueText.color = textColor; }
        if (nameText != null) { nameText.text = line.speakerName; nameText.color = textColor; }
        SetSpeaker(line.speaker == DialogueSpeaker.A);

        if (i == 0 && dialogueText != null)
            Debug.Log("[DialoguePanel] 第 1 句文字已写入：" + line.text + "（说话人 " + line.speaker + "）。" +
                      "若 Console 有这句、画面仍无字，就是字体/颜色/宽高问题，看上面的 Warning。", this);
    }

    /// <summary>检查接线/渲染配置，把文字显示不出来的具体原因打到 Console（只在播对话时调用一次）。</summary>
    void ValidateWiring()
    {
        if (dialogueText == null)
        {
            Debug.LogWarning("[DialoguePanel] 文字不见了：dialogueText（Dialog Text 字段）是空的，请把对话框里的文字拖进去。", this);
            Debug.LogWarning("    → 注意：必须用 TextMeshPro 文本（右键 Hierarchy → UI → Text - TextMeshPro），不是老的「Text」。", this);
            return;
        }
        if (dialogueText.font == null)
            Debug.LogWarning("[DialoguePanel] 文字不见了：dialogueText 的字体（Font Asset）是空的，选一个字体即可（如 LiberationSans SDF）。", this);
        if (dialogueText.color.a <= 0f)
            Debug.LogWarning("[DialoguePanel] 文字不见了：dialogueText 颜色 Alpha = 0（全透明），把 Color 的 A 调回 255。", this);
        if (dialogueText.rectTransform.sizeDelta.x <= 0f || dialogueText.rectTransform.sizeDelta.y <= 0f)
            Debug.LogWarning("[DialoguePanel] 文字不见了：dialogueText 宽或高是 0，给它一个尺寸（如 800 × 120）。", this);
    }

    /// <summary>让说话者亮+置顶，另一位压暗沉底。两个立绘需在同一父节点下，置顶才生效；不同父节点则只有明暗变化。</summary>
    void SetSpeaker(bool aSpeaks)
    {
        bool sameParent = portraitA != null && portraitB != null &&
                          portraitA.transform.parent == portraitB.transform.parent;

        if (portraitA != null)
        {
            portraitA.color = aSpeaks ? normalA : dimColor;
            if (aSpeaks && sameParent) portraitA.transform.SetAsLastSibling();
        }
        if (portraitB != null)
        {
            portraitB.color = aSpeaks ? dimColor : normalB;
            if (!aSpeaks && sameParent) portraitB.transform.SetAsLastSibling();
        }
    }

    void Close()
    {
        Active = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevTimeScale;
        lines = null;
    }
}
