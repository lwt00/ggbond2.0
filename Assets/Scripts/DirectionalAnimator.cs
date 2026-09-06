using UnityEngine;

/// <summary>
/// 左右行走序列帧动画（所有角色通用，以撒式简化为左右两方向）：
/// 只有 Left / Right 两组帧，每组 8 张；移动时按「最后朝向」播放，停下时停在待机帧。
/// 由 PlayerController / EnemyCell 调用 SetFacing / SetMoving 驱动。
/// 初始朝向默认朝右。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DirectionalAnimator : MonoBehaviour
{
    public enum Facing { Left = 0, Right = 1 }

    /// <summary>Size 对齐贴图的高度还是宽度。</summary>
    public enum SizeFit { Height, Width }

    [Header("两组行走帧（每组 8 张，按顺序拖入）")]
    [Tooltip("朝左走（角色左侧）的 8 帧")]
    public Sprite[] left;
    [Tooltip("朝右走（角色右侧）的 8 帧")]
    public Sprite[] right;

    [Header("攻击序列帧（可选，留空 = 攻击时无动画）")]
    [Tooltip("攻击动画序列帧（正面，不分左右；留空则攻击不播动画）")]
    public Sprite[] attack;

    [Header("变换形态美术（吃够方糖解锁攻击后切换，可选）")]
    [Tooltip("变换后的朝左走帧（留空 = 变换后仍用原朝左帧）")]
    public Sprite[] transformedLeft;
    [Tooltip("变换后的朝右走帧（留空 = 变换后仍用原朝右帧）")]
    public Sprite[] transformedRight;
    [Tooltip("变换后的攻击帧（留空 = 变换后仍用原攻击帧）")]
    public Sprite[] transformedAttack;

    [Header("播放")]
    [Tooltip("每秒播放帧数（建议 8~12）")]
    public float fps = 8f;
    [Tooltip("初始朝向（默认朝右）")]
    public Facing startFacing = Facing.Right;

    [Header("外观")]
    [Tooltip("整体染色（白色 = 不染色）")]
    public Color tint = Color.white;
    [Tooltip("角色显示尺寸（世界单位）：按贴图高度等比缩放，使贴图高度恒等于此值")]
    public float size = 0.6f;
    [Tooltip("变换形态后的显示尺寸（世界单位）：变身时视觉和碰撞一起长到该值。填 0 = 变身不变大小")]
    public float transformedSize = 1.3f;
    [Tooltip("碰撞体直径相对视觉尺寸的缩放系数（主角可设为 0.3~0.6：视觉明显但碰撞体小而好走位；默认 1 = 碰撞体和视觉一样大）")]
    public float colliderScale = 1f;
    [Tooltip("Size 对齐贴图的高度还是宽度（竖版角色用 Height；横向攻击图想按宽度对齐可切 Width）")]
    public SizeFit sizeFit = SizeFit.Height;
    [Tooltip("渲染排序层级（主角通常 10，敌人 3）")]
    public int sortingOrder = 10;

    private SpriteRenderer sr;
    private Facing facing;
    private float timer;
    private int index;
    private bool moving;
    private bool transformed; // 是否已切换到「变换形态」美术
    private Coroutine attackCo;
    private bool attacking;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        facing = startFacing;
    }

    void Start()
    {
        if (sr.sprite == null) sr.sprite = ColorBlockFactory.WhiteSprite;
        sr.color = tint;
        sr.sortingOrder = sortingOrder;
        ApplyFrame(0);
        ApplySize();
    }

    void Update()
    {
        if (attacking) return; // 攻击动画播放期间暂停走路帧

        var frames = CurrentFrames();
        if (!moving || frames == null || frames.Length <= 1) return;

        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.0001f, fps);
        if (timer >= interval)
        {
            timer = 0f;
            index = (index + 1) % frames.Length;
            ApplyFrame(index);
        }
    }

    Sprite[] CurrentFrames()
    {
        if (transformed)
        {
            var t = (facing == Facing.Left) ? transformedLeft : transformedRight;
            if (t != null && t.Length > 0) return t; // 变换帧留空则回退用原帧
        }
        return facing == Facing.Left ? left : right;
    }

    void ApplyFrame(int i)
    {
        var frames = CurrentFrames();
        if (frames == null || frames.Length == 0) return; // 没拖帧：保持当前 Sprite（纯色方块）
        sr.sprite = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
        ApplySize();
    }

    /// <summary>
    /// 让 Size 直接等于角色最终显示大小：按当前贴图的原生尺寸（像素/PPU）等比缩放，
    /// 使贴图的「高度」（或宽度，见 sizeFit）恒等于 size，行走/攻击帧都保持同一大小，
    /// 不再受每张贴图自身分辨率/PPU/宽高比影响（导入时忘跑工具、各帧尺寸不一致都没关系）。
    /// 同时把圆形碰撞体半径反算成世界直径恒等于 size，保证「大小 = 碰撞体积」。
    /// </summary>
    /// <summary>当前生效的尺寸：已变身且配了 transformedSize 时用变身后尺寸，否则用 size。</summary>
    float EffectiveSize
    {
        get { return (transformed && transformedSize > 0.0001f) ? transformedSize : size; }
    }

    void ApplySize()
    {
        if (sr == null || sr.sprite == null) return;
        float span = (sizeFit == SizeFit.Width) ? sr.sprite.bounds.size.x : sr.sprite.bounds.size.y;
        if (span <= 0.0001f) return;

        float eff = EffectiveSize;
        float s = eff / span;
        transform.localScale = new Vector3(s, s, 1f);

        var col = GetComponent<CircleCollider2D>();
        if (col != null) col.radius = span * 0.5f * Mathf.Max(0.0001f, colliderScale); // 世界直径 = 当前尺寸 * colliderScale
    }

    /// <summary>设置朝向（左/右），由 PlayerController / EnemyCell 按移动方向调用。</summary>
    public void SetFacing(Facing f)
    {
        if (f == facing) return;
        facing = f;
        index = 0;
        timer = 0f;
        ApplyFrame(0);
    }

    /// <summary>设置是否在移动。true=播放行走帧，false=停在当前组第 0 帧（待机）。</summary>
    public void SetMoving(bool m)
    {
        if (m == moving) return;
        moving = m;
        if (!moving)
        {
            index = 0;
            timer = 0f;
            ApplyFrame(0);
        }
    }

    /// <summary>切换主角形态（吃够方糖解锁攻击时由 GameManager 调用）：换用「变换后的美术」。</summary>
    public void SetTransformed(bool on)
    {
        if (transformed == on) return;
        transformed = on;
        index = 0;
        timer = 0f;
        ApplyFrame(0);
    }

    /// <summary>播放一次攻击动画（attack 为空则直接跳过，不报错）。</summary>
    public void PlayAttack(float duration = 0.3f)
    {
        if (attackCo != null) StopCoroutine(attackCo);
        attacking = false; // 复位，避免被打断的旧动画卡住状态
        attackCo = StartCoroutine(AttackRoutine(duration));
    }

    System.Collections.IEnumerator AttackRoutine(float duration)
    {
        var frames = (transformed && transformedAttack != null && transformedAttack.Length > 0) ? transformedAttack : attack;
        if (frames == null || frames.Length == 0)
        {
            attacking = false;
            attackCo = null;
            yield break; // 没拖帧：不播动画，也不报错
        }

        attacking = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            int i = Mathf.Clamp((int)(t / Mathf.Max(0.0001f, duration) * frames.Length), 0, frames.Length - 1);
            if (sr != null) { sr.sprite = frames[i]; ApplySize(); }
            yield return null;
        }
        attacking = false;
        attackCo = null;
        ApplyFrame(0); // 回到待机帧
    }
}
