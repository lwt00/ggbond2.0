using UnityEngine;

/// <summary>
/// 帧动画组件：给玩家/敌人等角色留的「放动画」的地方。
/// 在 Inspector 的 Frames 里拖入 Sprite 序列即可逐帧播放；
/// 留空则显示 SpriteRenderer 上已设的 Sprite，若也没有则用默认白色方块 + Tint 染色。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [Header("动画")]
    [Tooltip("动画帧序列（按顺序逐帧播放）。留空 = 不播动画，只显示单张 Sprite / 色块")]
    public Sprite[] frames;

    [Tooltip("每秒播放的帧数")]
    public float fps = 8f;

    [Tooltip("是否循环播放")]
    public bool loop = true;

    [Tooltip("是否一开始就自动播放")]
    public bool playOnAwake = true;

    [Header("外观")]
    [Tooltip("整体染色（白色 = 不染色）。色块模式下用它决定颜色")]
    public Color tint = Color.white;

    [Tooltip("角色显示尺寸（白色方块下 = 世界单位边长）。调小让角色/方糖更小，如 0.6")]
    public float size = 1f;

    [Tooltip("渲染排序层级（越大越靠前，角色通常设 10）")]
    public int sortingOrder = 10;

    private SpriteRenderer sr;
    private float timer;
    private int index;
    private bool playing;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        playing = playOnAwake;
    }

    void Start()
    {
        if (sr.sprite == null)
            sr.sprite = ColorBlockFactory.WhiteSprite;
        sr.color = tint;
        sr.sortingOrder = sortingOrder;
        transform.localScale = new Vector3(size, size, 1f);
        if (frames != null && frames.Length > 0)
            sr.sprite = frames[0];
    }

    void Update()
    {
        if (!playing) return;
        if (frames == null || frames.Length <= 1) return;

        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.0001f, fps);
        if (timer >= interval)
        {
            timer = 0f;
            index++;
            if (index >= frames.Length)
            {
                if (loop) index = 0;
                else { index = frames.Length - 1; playing = false; }
            }
            sr.sprite = frames[index];
        }
    }

    /// <summary>从头开始播放动画。</summary>
    public void Play()
    {
        playing = true;
        index = 0;
        timer = 0f;
    }

    /// <summary>暂停播放。</summary>
    public void Stop()
    {
        playing = false;
    }
}
