using UnityEngine;

/// <summary>敌人类型（决定美术与攻击方式）。</summary>
public enum EnemyType { NeedleGirl, GreenBag, Capsule, Antigen }

/// <summary>敌人攻击方式：近战贴脸 / 远程发射子弹。</summary>
public enum EnemyAttackMode { Melee, Ranged }

/// <summary>
/// 普通细胞 AI（2D 俯视）：
///   Idle  空闲（偶尔原地左右移动，不主动攻击）
///   Chase 警觉追击（朝玩家移动、靠近攻击）
/// 觉醒条件：玩家已武装（能攻击）且已到达本细胞所在房间，玩家进入 aggroRadius 才追击；
///           还没到达的房间的细胞保持待机。
/// </summary>
public class EnemyCell : MonoBehaviour
{
    public enum State { Idle, Chase }
    [Tooltip("当前状态（运行时自动切换，无需手动设置）")]
    public State state = State.Idle;

    [Tooltip("敌人类型（针管女/绿色袋子/胶囊；决定美术与攻击方式，运行时由 MapGenerator 填入）")]
    public EnemyType type = EnemyType.NeedleGirl;

    [Header("移动")]
    [Tooltip("空闲时移动速度（很慢）")]
    public float idleSpeed = 0.4f;
    [Tooltip("追击速度（比主角 4 慢，可被溜）")]
    public float chaseSpeed = 1.8f;

    [Header("警觉")]
    [Tooltip("已武装后、玩家进入此距离才追击")]
    public float aggroRadius = 8f;
    [Tooltip("脱战距离：超出则放弃追击回出生点")]
    public float deaggroRadius = 13f;
    [Tooltip("所属房间索引（0 起，越靠后的房间越大；运行时由 MapGenerator 填入）")]
    public int roomIndex = 0;

    [Header("攻击")]
    [Tooltip("近战攻击距离（以敌人中心到玩家中心算）。玩家碰撞体较大，此值须大于「玩家半径 + 自身半径」才能稳定够到，故调大；不要通过改碰撞体解决，以免卡墙")]
    public float attackRange = 2.0f;
    [Tooltip("两次攻击的最小间隔（秒）")]
    public float attackCooldown = 1.5f;
    [Tooltip("每次攻击对玩家造成的伤害")]
    public int damage = 10;

    [Header("远程攻击（attackMode = Ranged 时生效）")]
    [Tooltip("攻击方式：近战贴脸 / 远程发射子弹（针管女、绿色袋子用远程）")]
    public EnemyAttackMode attackMode = EnemyAttackMode.Melee;
    [Tooltip("进入此距离后停下开始射击")]
    public float fireRange = 6f;
    [Tooltip("子弹飞行速度")]
    public float bulletSpeed = 8f;
    [Tooltip("子弹尺寸（世界单位边长）")]
    public float bulletSize = 0.3f;
    [Tooltip("子弹颜色（无贴图时）")]
    public Color bulletColor = new Color(0.6f, 0.95f, 1f, 1f);
    [Tooltip("子弹贴图（留空 = 圆形色块）")]
    public Sprite bulletSprite;
    [Tooltip("发射前红线瞄准时长（秒）")]
    public float telegraphTime = 0.4f;
    [Tooltip("红线颜色")]
    public Color telegraphColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [Tooltip("红线粗细（世界单位）")]
    public float telegraphWidth = 0.08f;

    [Header("血量")]
    [Tooltip("最大血量")]
    public int maxHp = 2;

    [Header("受击反馈")]
    [Tooltip("被攻击时击退的力度（初速度）")]
    public float knockbackForce = 5f;
    [Tooltip("死亡粒子颜色（默认淡蓝，与细胞同色系）")]
    public Color deathParticleColor = new Color(0.5f, 0.8f, 1f);
    [Tooltip("被击中后无敌窗口（秒）：期间无法再被击中，避免被连击秒杀")]
    public float hitInvincibleTime = 0.3f;
    [Tooltip("受击红色闪光时长（秒）")]
    public float hitFlashTime = 0.12f;
    [Tooltip("受击闪光颜色（默认红）")]
    public Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    private Vector2 spawnPos;
    private Health health;
    private Rigidbody2D rb;
    private float nextAttackTime;
    private float idleTimer;
    private int idleDir = 1;
    private GameObject telegraphLine;   // 发射前红线
    private bool telegraphing;
    private float telegraphTimer;

    private SpriteRenderer sr;
    private Color originalColor = Color.white;
    private float flashTimer;     // 命中闪光剩余时间
    private float knockbackTimer; // 击退剩余时间
    private float invincibleTimer; // 受击无敌剩余时间
    private DirectionalAnimator animator;
    private Coroutine punchCo;

    void Start()
    {
        spawnPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<DirectionalAnimator>();

        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        health.Init(maxHp);
        health.onDeath += OnDeath;

        GameManager.Instance.RegisterEnemy(this);
    }

    void Update()
    {
        if (health != null && health.currentHp <= 0) return;

        // 受击无敌计时
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;

        // 命中闪光计时：时间到恢复原色
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && sr != null) sr.color = originalColor;
        }

        // 击退期间：速度自然衰减，本帧不覆盖速度（跳过 AI 移动）
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            rb.velocity = Vector2.MoveTowards(rb.velocity, Vector2.zero, Time.deltaTime * 30f);
            return;
        }

        DecideState();

        if (state == State.Idle) { IdleUpdate(); UpdateAnimator(false); }
        else { ChaseUpdate(); UpdateAnimator(true); }
    }

    /// <summary>驱动左右行走动画：追击/攻击时朝向玩家，空闲时停在待机帧。</summary>
    void UpdateAnimator(bool moving)
    {
        if (animator == null) return;
        if (moving)
        {
            float dx = GameManager.Instance.PlayerPosition.x - transform.position.x;
            animator.SetFacing(dx < 0f ? DirectionalAnimator.Facing.Left : DirectionalAnimator.Facing.Right);
        }
        animator.SetMoving(moving);
    }

    void DecideState()
    {
        if (state == State.Chase) return; // 追击状态的脱战在 ChaseUpdate 里处理

        float dist = Vector2.Distance(transform.position, GameManager.Instance.PlayerPosition);
        bool armed = GameManager.Instance.CanEnemyActivate(roomIndex);
        if (armed && dist < aggroRadius)
            state = State.Chase;
    }

    void IdleUpdate()
    {
        // 偶尔左右移动：走一会停一会
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            idleTimer = Random.Range(0.8f, 2.2f);
            idleDir = Random.value < 0.5f ? -1 : 1;
        }
        float move = (idleTimer > 0.5f) ? idleDir * idleSpeed : 0f;
        rb.velocity = new Vector2(move, rb.velocity.y);
    }

    void ChaseUpdate()
    {
        Vector2 player = GameManager.Instance.PlayerPosition;
        float dist = Vector2.Distance(transform.position, player);

        // 超出脱战范围：回出生点，到达后转空闲
        if (dist > deaggroRadius)
        {
            Vector2 back = spawnPos - (Vector2)transform.position;
            if (back.magnitude < 0.2f)
            {
                rb.velocity = Vector2.zero;
                state = State.Idle;
                return;
            }
            rb.velocity = back.normalized * chaseSpeed;
            return;
        }

        // 远程攻击：进入射击距离就停下，先亮红线瞄准，再开火；否则继续追击
        if (attackMode == EnemyAttackMode.Ranged)
        {
            if (dist <= fireRange)
            {
                rb.velocity = Vector2.zero;
                if (telegraphing)
                {
                    UpdateTelegraph(player);
                    telegraphTimer -= Time.deltaTime;
                    if (telegraphTimer <= 0f)
                    {
                        telegraphing = false;
                        HideTelegraph();
                        FireBullet(player);
                    }
                }
                else if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    telegraphing = true;
                    telegraphTimer = telegraphTime;
                    ShowTelegraph(player);
                }
                return;
            }
            if (telegraphing) { telegraphing = false; HideTelegraph(); } // 走出射程：取消瞄准
            Vector2 chaseDir = (player - (Vector2)transform.position).normalized;
            rb.velocity = chaseDir * chaseSpeed;
            return;
        }

        // 攻击范围内：攻击（近战）
        if (dist <= attackRange)
        {
            rb.velocity = Vector2.zero;
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                if (animator != null) animator.PlayAttack();
                GameManager.Instance.DamagePlayer(damage, transform.position);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyAttack();
            }
            return;
        }

        // 追击
        Vector2 dir = (player - (Vector2)transform.position).normalized;
        rb.velocity = dir * chaseSpeed;
    }

    /// <summary>朝玩家方向发射一颗直线子弹。</summary>
    void FireBullet(Vector2 player)
    {
        Vector2 fireDir = (player - (Vector2)transform.position).normalized;
        if (animator != null) animator.PlayAttack();
        // 从敌人正前方一点发射，避免子弹一出生就与敌人自己的碰撞体重叠
        Vector2 muzzle = (Vector2)transform.position + fireDir * 0.6f;
        EnemyBullet.Spawn(muzzle, fireDir, bulletSpeed, damage, bulletColor, bulletSprite, bulletSize);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyAttack();
    }

    /// <summary>显示瞄准红线：从敌人指向玩家的一条红色细线。</summary>
    void ShowTelegraph(Vector2 player)
    {
        if (telegraphLine == null)
        {
            var go = new GameObject("TelegraphLine");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ColorBlockFactory.WhiteSprite;
            sr.color = telegraphColor;
            sr.sortingOrder = 4; // 高于敌人(3)，低于主角(10)
            telegraphLine = go;
        }
        telegraphLine.SetActive(true);
        UpdateTelegraph(player);
    }

    /// <summary>让红线始终对准玩家（玩家移动时红线跟着转）。</summary>
    void UpdateTelegraph(Vector2 player)
    {
        if (telegraphLine == null) return;
        Vector2 dir = player - (Vector2)transform.position;
        Vector2 mid = (Vector2)transform.position + dir * 0.5f;
        telegraphLine.transform.position = mid;
        telegraphLine.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        telegraphLine.transform.localScale = new Vector3(dir.magnitude, telegraphWidth, 1f);
    }

    void HideTelegraph()
    {
        if (telegraphLine != null) telegraphLine.SetActive(false);
    }

    public void TakeDamage(int dmg, Vector2 fromDir)
    {
        // 无敌窗口：这一击忽略（避免连续受击一下掉好几段血）
        if (invincibleTimer > 0f) return;

        if (health != null) health.TakeDamage(dmg);

        // 这一击直接打死了：死亡反馈（音效 + 爆裂 + 销毁）已由 OnDeath 处理，这里不再做受击反馈
        if (health != null && health.currentHp <= 0) return;

        invincibleTimer = hitInvincibleTime;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyHurt();
        FlashHit();
        ApplyKnockback(fromDir); // 必退：受击反馈确定、可学习

        // 打击感：顿帧 + 命中火花 + 短暂缩小 punch
        if (CameraFollow.Instance != null) CameraFollow.Instance.HitStop(0.04f);
        SimpleParticle.Burst(transform.position, new Color(1f, 0.6f, 0.4f), 6, 4f, 0.22f, 0.3f);
        ScalePunch(0.25f, 0.12f);
    }

    void FlashHit()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (flashTimer <= 0f) originalColor = sr.color; // 第一次闪前记录当前颜色（含动画染色）
        sr.color = hitFlashColor;
        flashTimer = hitFlashTime;
    }

    void ApplyKnockback(Vector2 dir)
    {
        if (rb == null || dir == Vector2.zero) return;
        rb.velocity = dir * knockbackForce;
        knockbackTimer = 0.15f;
    }

    /// <summary>受击时短暂缩小再复原（配合闪白一起，让命中更有「肉感」）。</summary>
    void ScalePunch(float amount, float duration)
    {
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(ScalePunchRoutine(amount, duration));
    }

    System.Collections.IEnumerator ScalePunchRoutine(float amount, float duration)
    {
        Vector3 baseScale = transform.localScale;
        Vector3 small = baseScale * (1f - amount);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            transform.localScale = Vector3.Lerp(baseScale, small, k);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    void OnDeath()
    {
        GameManager.Instance.OnEnemyKilled();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyDeath();
        SimpleParticle.Burst(transform.position, deathParticleColor, 8, 3f, 0.25f, 0.5f);
        Destroy(gameObject);
    }

    /// <summary>由 GameManager 在吸收方糖时调用：范围内细胞立即进入追击。</summary>
    public void ForceAlert(Vector2 pos, float radius)
    {
        if (state == State.Chase) return;
        if (!GameManager.Instance.CanEnemyActivate(roomIndex)) return; // 未武装或房间未到达：保持待机
        if (Vector2.Distance(transform.position, pos) < radius)
            state = State.Chase;
    }

    void OnDestroy()
    {
        if (telegraphLine != null) Destroy(telegraphLine);
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterEnemy(this);
    }
}
