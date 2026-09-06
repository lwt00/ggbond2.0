using UnityEngine;

/// <summary>
/// 敌人子弹（2D）：沿直线朝一个方向飞行。
///   碰到墙体/障碍 → 消失；碰到玩家 → 造成伤害并消失；
///   碰到敌人 / 其它子弹 / 方糖 / 交付点 → 穿过不消失。
/// 玩家攻击时若子弹在其攻击范围内，会调用 HitByPlayer() 把子弹打掉（不伤玩家）。
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    [Tooltip("飞行速度")]
    public float speed = 8f;
    [Tooltip("命中玩家造成的伤害")]
    public int damage = 10;
    [Tooltip("存活时间（秒），超时自动消失，防止子弹无限存在）")]
    public float lifetime = 5f;
    [Tooltip("子弹颜色（无贴图时）")]
    public Color color = new Color(0.6f, 0.95f, 1f, 1f);
    [Tooltip("子弹尺寸（世界单位边长）")]
    public float size = 0.3f;
    [Tooltip("子弹贴图（留空 = 圆形色块）")]
    public Sprite sprite;
    [Tooltip("飞行方向（运行时由 Spawn 设置）")]
    public Vector2 direction = Vector2.right;

    private Rigidbody2D rb;

    /// <summary>生成一颗子弹，朝 dir 方向直线飞行。</summary>
    public static EnemyBullet Spawn(Vector2 pos, Vector2 dir, float speed, int damage, Color color, Sprite sprite, float size)
    {
        var go = new GameObject("EnemyBullet");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : ColorBlockFactory.CircleSprite;
        sr.color = color;
        sr.sortingOrder = 5;
        go.transform.localScale = new Vector3(size, size, 1f);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f; // 1x1 精灵随 localScale 缩放后，世界半径 ≈ 0.5 * size

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var b = go.AddComponent<EnemyBullet>();
        b.speed = speed;
        b.damage = damage;
        b.color = color;
        b.size = size;
        b.direction = dir.normalized;
        b.rb = rb;
        rb.velocity = b.direction * speed;
        return b;
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 命中玩家：造成伤害并消失
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.DamagePlayer(damage, transform.position);
            Destroy(gameObject);
            return;
        }

        // 穿过敌人 / 其它子弹 / 方糖 / 传送门 / NPC / 红线
        if (other.GetComponent<EnemyCell>() != null ||
            other.GetComponent<EnemyBullet>() != null ||
            other.GetComponent<SugarCube>() != null ||
            other.GetComponent<Portal>() != null ||
            other.GetComponent<NPC>() != null ||
            other.GetComponent<FinalChoiceTrigger>() != null)
            return;

        // 其余（墙体 / 障碍）→ 消失
        Destroy(gameObject);
    }

    /// <summary>玩家攻击范围内被打掉：不造成伤害，直接销毁（加一点火花反馈）。</summary>
    public void HitByPlayer()
    {
        SimpleParticle.Burst(transform.position, color, 6, 3f, 0.22f, 0.3f);
        Destroy(gameObject);
    }
}
