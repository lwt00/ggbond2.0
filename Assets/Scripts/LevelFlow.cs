/// <summary>
/// 跨场景的简单流程标记（静态，不挂物体）。
/// 从选关界面进入关卡时置 true，让关卡里的 UIManager 跳过「开始界面」直接开玩（只生效一次）。
/// </summary>
public static class LevelFlow
{
    /// <summary>true = 下一关加载后直接开始游戏，不显示「开始界面」。</summary>
    public static bool skipStartPanel = false;

    /// <summary>true = 开场视频已经播过（本次运行只播一次，之后重开/下一关都不再播）。</summary>
    public static bool introPlayed = false;
}
