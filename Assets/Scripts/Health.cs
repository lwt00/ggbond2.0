using UnityEngine;

/// <summary>
/// 通用血量组件（玩家与普通细胞共用）。
/// 玩家：手动挂在 Player 上，maxHp 在 Inspector 里调。
/// 敌人：由 EnemyCell 在 Start 里自动添加并 Init。
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("最大血量")]
    public int maxHp = 100;

    [Tooltip("当前血量（运行时自动等于最大血量，无需手动设置）")]
    public int currentHp;

    /// <summary>死亡时触发（玩家由 GameManager 接到「失败结算」；敌人由 EnemyCell 接到击杀）。</summary>
    public System.Action onDeath;

    public float Ratio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    void Awake()
    {
        currentHp = maxHp;
    }

    /// <summary>设最大血量并把当前血量拉满。</summary>
    public void Init(int max)
    {
        maxHp = max;
        currentHp = max;
    }

    public void TakeDamage(int dmg)
    {
        if (currentHp <= 0) return;
        currentHp -= dmg;
        if (currentHp <= 0)
        {
            currentHp = 0;
            onDeath?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, maxHp);
    }
}
