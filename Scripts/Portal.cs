using UnityEngine;

/// <summary>
/// 传送门（2D）：走进触发范围按 E 直接过关（第 1/2 关进下一关，第 3 关进结局）。
/// 无需集齐方糖；方糖现在是可选的收集/成长要素（全程不吃糖通关有彩蛋结局）。
/// </summary>
public class Portal : MonoBehaviour
{
    bool playerInRange;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    void Update()
    {
        if (!playerInRange) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.CanUsePortal) return; // 狂暴波未清完：传送阵锁定，按 E 无效
        GameManager.Instance.TryEnterPortal();
    }
}
