using UnityEngine;

/// <summary>
/// 玩家控制（2D 俯视）：WASD 移动 + 攻击（空格/左键）。
/// 交互「长按 E 吸收方糖」在 SugarCube 里处理。
/// 运行时会在脚下显示一个攻击范围指示圈，方便调 attackRange。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("主角移动速度（比普通细胞快，用来溜怪）")]
    public float moveSpeed = 4f;

    [Header("攻击")]
    [Tooltip("两次攻击之间的最小间隔（秒）")]
    public float attackCooldown = 0.5f;
    [Tooltip("攻击范围（以角色为中心的半径，也是脚下指示圈的半径）")]
    public float attackRange = 1.6f;
    [Tooltip("每次攻击造成的伤害")]
    public int attackDamage = 1;

    [Header("攻击范围指示")]
    [Tooltip("是否显示攻击范围指示圈（运行时可见）")]
    public bool showAttackRange = true;
    [Tooltip("指示圈颜色（半透明实心圆，Alpha 建议 0.2~0.4，别太浓）")]
    public Color attackRangeColor = new Color(1f, 0.3f, 0.3f, 0.3f);

    [Header("受击")]
    [Tooltip("被敌人攻击时的击退力度（初速度）")]
    public float hurtKnockbackForce = 7f;
    [Tooltip("被击退后失控（无法移动）的时长（秒）")]
    public float hurtKnockbackTime = 0.18f;
    [Tooltip("被击中后无敌窗口（秒）：期间无法再被击中，避免被连续攻击连掉血")]
    public float hurtInvincibleTime = 0.5f;
    [Tooltip("受击红色闪光时长（秒）")]
    public float hurtFlashTime = 0.2f;
    [Tooltip("受击闪光颜色")]
    public Color hurtFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("手感（打击感）")]
    [Tooltip("攻击命中瞬间的顿帧时长（秒）")]
    public float hitStopTime = 0.05f;
    [Tooltip("攻击斩击光环颜色")]
    public Color slashColor = new Color(1f, 0.5f, 0.35f, 0.55f);
    [Tooltip("攻击命中时震屏强度（世界单位，越大越抖，调小可减弱）")]
    public float hitShakeAmount = 0.12f;
    [Tooltip("攻击命中时震屏时长（秒）")]
    public float hitShakeDuration = 0.15f;
    [Tooltip("攻击挥空时震屏强度（世界单位）")]
    public float whiffShakeAmount = 0.05f;
    [Tooltip("攻击挥空时震屏时长（秒）")]
    public float whiffShakeDuration = 0.06f;

    [Tooltip("同一物体上的 PlayerStats，Awake 自动获取，无需手动拖")]
    public PlayerStats stats;

    private Rigidbody2D rb;
    private float lastAttackTime = -999f;
    private GameObject rangeIndicator;
    private Coroutine pulseCo;
    private Coroutine hurtFlashCo;
    private DirectionalAnimator animator;
    private float knockbackTimer;
    private float invincibleTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 平滑移动，减少左右走动时的抖动
        stats = GetComponent<PlayerStats>();
        animator = GetComponent<DirectionalAnimator>();
        BuildRangeIndicator();
    }

    void Update()
    {
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;

        UpdateRangeIndicator();

        if (!DialoguePanel.Active && !UIManager.DialogueActive && stats != null && stats.attackUnlocked &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            TryAttack();
        }
    }

    void FixedUpdate()
    {
        // 被击退期间：短暂失控，只让击退速度自然衰减（跳过 WASD 移动）
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            rb.velocity = Vector2.MoveTowards(rb.velocity, Vector2.zero, Time.fixedDeltaTime * 40f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWalk(false);
            return;
        }

        float h = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
        float v = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        rb.velocity = new Vector2(h, v).normalized * moveSpeed;

        bool moving = (h != 0f || v != 0f);

        // 驱动左右行走动画（挂了 DirectionalAnimator 才生效；只有左右两组帧）
        if (animator != null)
        {
            if (h < 0f)
                animator.SetFacing(DirectionalAnimator.Facing.Left);
            else if (h > 0f)
                animator.SetFacing(DirectionalAnimator.Facing.Right);
            // 纯上下移动时不改朝向，保持上一次的左右朝向
            animator.SetMoving(moving);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWalk(moving);
    }

    void BuildRangeIndicator()
    {
        var go = new GameObject("AttackRange");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ColorBlockFactory.CircleSprite;
        sr.color = attackRangeColor;
        sr.sortingOrder = 1; // 低于主角(10)/敌人(3)，只铺在地板上，不把角色染成紫红
        rangeIndicator = go;
    }

    void UpdateRangeIndicator()
    {
        if (rangeIndicator == null) return;
        rangeIndicator.SetActive(showAttackRange);
        if (!showAttackRange) return;
        rangeIndicator.transform.position = transform.position;
        rangeIndicator.transform.localScale = Vector3.one * (attackRange * 2f);
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayAttack();

        // 播放攻击序列帧（DirectionalAnimator 上没拖攻击帧则自动跳过，不报错）
        if (animator != null) animator.PlayAttack(attackCooldown * 0.9f);

        // 以角色为中心的圆形范围攻击（和脚下指示圈一致）
        Vector2 origin = (Vector2)transform.position;

        // 攻击视觉：向外扩张的斩击光环
        SimpleParticle.Ring(origin, slashColor, attackRange * 0.3f, attackRange * 2.4f, 0.16f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRange);
        bool hitAny = false;
        foreach (var h in hits)
        {
            // 攻击范围内有敌人子弹：先打掉子弹（不伤玩家）
            var bullet = h.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                bullet.HitByPlayer();
                hitAny = true;
                continue;
            }

            var enemy = h.GetComponent<EnemyCell>();
            if (enemy == null) continue;
            Vector2 dir = ((Vector2)enemy.transform.position - origin).normalized;
            enemy.TakeDamage(attackDamage, dir);
            hitAny = true;
        }

        // 打击感：命中时顿帧 + 更明显的震屏；挥空也有一点点震
        if (hitAny)
        {
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.HitStop(hitStopTime);
                CameraFollow.Instance.Shake(hitShakeAmount, hitShakeDuration);
            }
        }
        else if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.Shake(whiffShakeAmount, whiffShakeDuration);
        }
    }

    /// <summary>吸收方糖时的缩放脉冲（短暂变大再复原，反馈「生长」）。</summary>
    public void PulseScale(float amount = 0.2f, float duration = 0.22f)
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(PulseScaleRoutine(amount, duration));
    }

    private System.Collections.IEnumerator PulseScaleRoutine(float amount, float duration)
    {
        Vector3 baseScale = transform.localScale;
        Vector3 big = baseScale * (1f + amount);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI); // 0→1→0 平滑脉冲
            transform.localScale = Vector3.Lerp(baseScale, big, k);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    /// <summary>被敌人击退：沿 dir 方向给一个初速度，并进入短暂失控。</summary>
    public void ApplyKnockback(Vector2 dir, float force)
    {
        if (rb == null) return;
        Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        rb.velocity = d * force;
        knockbackTimer = hurtKnockbackTime;
    }

    /// <summary>能否被击中：无敌窗口内返回 false。</summary>
    public bool CanTakeDamage => invincibleTimer <= 0f;

    /// <summary>被击中：进入无敌窗口 + 红色闪光（由 GameManager 在扣血后调用）。</summary>
    public void NotifyHurt()
    {
        invincibleTimer = hurtInvincibleTime;
        FlashHurt();
    }

    /// <summary>受击闪红（短暂变红再恢复，让「被打中」一眼可见）。</summary>
    public void FlashHurt()
    {
        if (hurtFlashCo != null) StopCoroutine(hurtFlashCo);
        hurtFlashCo = StartCoroutine(FlashHurtRoutine());
    }

    private System.Collections.IEnumerator FlashHurtRoutine()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color baseColor = (animator != null) ? animator.tint : Color.white;
        sr.color = hurtFlashColor;
        yield return new WaitForSeconds(hurtFlashTime);
        if (sr != null) sr.color = baseColor;
    }
}
