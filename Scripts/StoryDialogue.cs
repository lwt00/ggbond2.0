using UnityEngine;

/// <summary>
/// 新版剧情对话内容（静态，硬编码在这里，改文字直接改这里）。
/// 每关一份独立的 DialogueLine[]，由各自的触发器调用 DialoguePanel.Instance.Play(...)：
///   第 1 关：走近 NPC 按 E（MapGenerator.SpawnNPC 默认取 Level1）
///   第 2 关：病变加重触发内心独白（触发条件待定，稍后补）
///   第 3 关：触发条件待定，稍后补
/// 说话人约定：1 → 立绘 A（DialogueSpeaker.A），2 → 立绘 B（DialogueSpeaker.B）。
/// 括号里的「（…）」是内心独白 / 舞台提示，直接作为文字显示。
/// </summary>
public static class StoryDialogue
{
    /// <summary>第 1 关：NPC 按 E 触发。1 = 主角（立绘 A），2 = 其它细胞（立绘 B）。</summary>
    public static readonly DialogueLine[] Level1 = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueSpeaker.A, text = "又有新成员加入了？" },
        new DialogueLine { speaker = DialogueSpeaker.A, text = "（这里是…？）" },
        new DialogueLine { speaker = DialogueSpeaker.B, text = "你怎么和我们不一样？" },
        new DialogueLine { speaker = DialogueSpeaker.A, text = "（观察四周，又看看自己，的确，相比起其他人绿色的眼睛、头发，确实显得格格不入。）" },
        new DialogueLine { speaker = DialogueSpeaker.B, text = "你不是我们的一员，你从哪里来？" },
        new DialogueLine { speaker = DialogueSpeaker.A, text = "（沉默…）" },
        new DialogueLine { speaker = DialogueSpeaker.B, text = "管它干什么！长的难看死了！哪里来的异种！离它远点！" },
        new DialogueLine { speaker = DialogueSpeaker.B, text = "（附和）就是就是，哪里来的滚回哪里去！（推搡、争吵、七嘴八舌）" },
        new DialogueLine { speaker = DialogueSpeaker.A, text = "我不知道....别....哎呦！（被推出去）" },
    };

    // 第 2 关、第 3 关的对话（不同触发方式），等你提供文字后补在这里：
    // public static readonly DialogueLine[] Level2 = new DialogueLine[] { ... };
    // public static readonly DialogueLine[] Level3 = new DialogueLine[] { ... };
}
