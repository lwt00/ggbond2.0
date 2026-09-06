using UnityEngine;

/// <summary>
/// 站桩 NPC（2D）：不移动，只是站在那里。玩家走进触发范围按 E 触发对话。
/// 对话内容由 MapGenerator 从 LevelData.npcDialogue 填入；没内容则按 E 无反应。
/// </summary>
public class NPC : MonoBehaviour
{
    [Tooltip("对话内容（逐行一句；由 MapGenerator 从关卡数据填入，也可手动设）")]
    public string[] dialogue;

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
        if (UIManager.DialogueActive) return; // 对话中不重复触发
        if (dialogue == null || dialogue.Length == 0) return;

        if (GameManager.Instance != null && GameManager.Instance.ui != null)
            GameManager.Instance.ui.PlayDialogue(dialogue);
    }
}
