using UnityEngine;

/// <summary>
/// 程序化粒子：一个小方块，按速度飞行、随生命周期淡出并缩小。
/// 用于打击感（死亡爆裂）与吸收反馈（飞向角色）等零素材特效，无需美术/粒子资源。
/// </summary>
public class SimpleParticle : MonoBehaviour
{
    private Vector2 velocity;
    private Transform target;    // 可选：飞向的目标（吸收方糖时飞向玩家）
    private float homeSpeed;
    private float lifetime;
    private float age;
    private float size;
    private Color color;
    private SpriteRenderer sr;
    private bool ring;       // true = 扩张淡出的光环（斩击/冲击波），false = 飞行粒子
    private float endSize;   // 光环扩张到的最终尺寸

    /// <summary>生成一个粒子。</summary>
    public static SimpleParticle Spawn(Vector2 pos, Color color, Vector2 velocity, float size, float lifetime, Transform target = null, float homeSpeed = 0f)
    {
        var go = new GameObject("Particle");
        go.transform.position = pos;
        var p = go.AddComponent<SimpleParticle>();
        p.velocity = velocity;
        p.target = target;
        p.homeSpeed = homeSpeed;
        p.lifetime = Mathf.Max(0.05f, lifetime);
        p.color = color;
        p.size = size;
        return p;
    }

    /// <summary>向外爆开（敌人死亡等）。</summary>
    public static void Burst(Vector2 pos, Color color, int count, float speed, float size, float lifetime)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(0.3f, 1f) * speed;
            Spawn(pos, color, v, size * Random.Range(0.6f, 1.2f), lifetime * Random.Range(0.7f, 1.3f));
        }
    }

    /// <summary>飞向目标（吸收方糖飞向角色）。</summary>
    public static void Collect(Vector2 pos, Color color, int count, Transform target, float speed, float size, float lifetime)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(0.3f, 1f) * speed;
            Spawn(pos, color, v, size * Random.Range(0.6f, 1.2f), lifetime * Random.Range(0.7f, 1.3f), target, speed * 1.5f);
        }
    }

    /// <summary>一个向外扩张并淡出的光环（斩击波 / 冲击波），用于攻击命中与受击的打击感。</summary>
    public static SimpleParticle Ring(Vector2 pos, Color color, float startSize, float endSize, float lifetime)
    {
        var go = new GameObject("Ring");
        go.transform.position = pos;
        var p = go.AddComponent<SimpleParticle>();
        p.ring = true;
        p.color = color;
        p.size = startSize;
        p.endSize = endSize;
        p.lifetime = Mathf.Max(0.05f, lifetime);
        return p;
    }

    void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = ring ? ColorBlockFactory.CircleSprite : ColorBlockFactory.WhiteSprite;
        sr.sortingOrder = 12;
        sr.color = color;
        transform.localScale = Vector3.one * size;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / lifetime;
        if (t >= 1f) { Destroy(gameObject); return; }

        if (ring)
        {
            // 扩张 + 淡出
            Color rc = color;
            rc.a *= (1f - t);
            sr.color = rc;
            transform.localScale = Vector3.one * Mathf.Lerp(size, endSize, t);
            return;
        }

        if (target != null)
        {
            Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            velocity = Vector2.Lerp(velocity, dir * homeSpeed, Time.deltaTime * 8f);
        }

        transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.deltaTime;

        Color c = color;
        c.a *= (1f - t);
        sr.color = c;
        transform.localScale = Vector3.one * (size * (1f - t * 0.5f));
    }
}
