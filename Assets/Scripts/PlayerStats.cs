using UnityEngine;

/// <summary>
/// 玩家状态：已吸方糖数、击杀数、攻击解锁。
/// 手动挂在 Player 上，参数在 Inspector 里调。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("成长")]
    [Tooltip("已吸收方糖数（运行时状态，无需手动设置）")]
    public int sugarAbsorbed = 0;
    [Tooltip("吸收几块方糖后解锁攻击")]
    public int attackUnlockCount = 2;
    [Tooltip("每吸收一块方糖回复的血量")]
    public int healPerSugar = 25;

    [Header("击杀")]
    [Tooltip("击杀普通细胞数（决定结局，运行时状态）")]
    public int killCount = 0;

    [Tooltip("是否已解锁吞噬攻击（运行时状态）")]
    public bool attackUnlocked = false;

    /// <summary>吸收一块方糖：回血；达到阈值解锁攻击。</summary>
    public void AbsorbSugar()
    {
        sugarAbsorbed++;

        var h = GetComponent<Health>();
        if (h != null) h.Heal(healPerSugar);

        if (!attackUnlocked && sugarAbsorbed >= attackUnlockCount)
            attackUnlocked = true;
    }

    public void OnKill()
    {
        killCount++;
    }
}
