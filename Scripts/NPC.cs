using UnityEngine;

/// <summary>
/// 站桩 NPC（2D）：不移动，只是站在那里。玩家走进触发范围按 R 触发对话。
/// 对话内容由 MapGenerator 从 LevelData.npcDialogue 填入；没内容则按 R 无反应。
/// 触发后调用 DialoguePanel.Instance.Play 显示新版剧情面板（面板由你在 Hierarchy 里自建）。
/// </summary>
public class NPC : MonoBehaviour
{
    [Tooltip("对话内容（逐句：谁在说 + 内容；由 MapGenerator 从关卡数据填入，也可手动设）")]
    public DialogueLine[] dialogue;

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
        if (!Input.GetKeyDown(KeyCode.R)) return;
        if (DialoguePanel.Active) return; // 对话中不重复触发

        if (DialoguePanel.Instance == null)
        {
            Debug.LogWarning("[NPC] 场景里没有挂 DialoguePanel 组件，按 R 无法触发剧情。", this);
            return;
        }
        if (dialogue == null || dialogue.Length == 0)
        {
            Debug.LogWarning("[NPC] 这个 NPC 没配置对话内容（Level_1 的 npcDialogue 为空）。", this);
            return;
        }

        DialoguePanel.Instance.Play(dialogue);
    }
}
