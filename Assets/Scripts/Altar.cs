using UnityEngine;

/// <summary>
/// 祭坛（第 3 关 NO / 和平分支）：MapGenerator.SpawnPeaceMode 在出口房生成。
/// 玩家进入触发圈按 E → 献祭 → 播结局 CG 对白 → 宿主存活结算。一次性。
/// </summary>
public class Altar : MonoBehaviour
{
    bool playerInRange;
    bool used;

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
        if (used || !playerInRange) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;
        used = true;
        if (GameManager.Instance != null) GameManager.Instance.OnAltarSacrifice();
    }
}
