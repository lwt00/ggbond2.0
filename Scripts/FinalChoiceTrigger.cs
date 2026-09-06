using UnityEngine;

/// <summary>
/// 最终抉择触发器（只第 3 关，放在「工作区C → 出口房」过道中段）：玩家穿过过道必触发
/// 「GROW / NO」抉择（无需集齐方糖；全程不吃糖通关另有彩蛋结局文案）。
/// </summary>
public class FinalChoiceTrigger : MonoBehaviour
{
    bool triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered) return;
        if (GameManager.Instance == null) return;

        triggered = true;
        GameManager.Instance.ShowGrowChoice();
    }
}
