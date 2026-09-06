using UnityEngine;

/// <summary>成长时期：幼年（浅绿、懵懂）→ 成年（易变癌细胞、紫红血管）。第 3 步做视觉进化时用。</summary>
public enum GrowthStage { Infant, Adult }

/// <summary>
/// 剧情文案（静态，硬编码在这个文件里，改文字直接改这里即可）。
/// 按关卡号取前置剧情；幼年→成年的「异变显现」剧情在第一次吸糖时触发。
/// 关卡号由当前场景名解析（Level_1~Level_3），SampleScene 兜底为第 1 关。
/// </summary>
public static class StoryData
{
    /// <summary>第 1 关为幼年，第 2 关起为成年。</summary>
    public static GrowthStage GetStage(int level) => level <= 1 ? GrowthStage.Infant : GrowthStage.Adult;

    /// <summary>取某关的前置剧情（逐行一句话）。</summary>
    public static string[] GetIntro(int level)
    {
        switch (level)
        {
            case 1: return IntroInfant;
            case 2: return IntroAdult;
            default: return IntroFinal;
        }
    }

    /// <summary>第一关第一次吸糖时触发的「异变显现」剧情。</summary>
    public static string[] GetInfantEvolution() => InfantEvolution;

    // ------- 各段文案（直接改这里的文字即可） -------

    // 幼年上：出生 / 异类
    static readonly string[] IntroInfant = new[]
    {
        "随着这具身体的主人降生，你在湿润的血肉深处，第一次有了意识。",
        "你是一小簇粘膜上皮干细胞。浅绿色，本应和周围的细胞毫无分别。",
        "可你偏偏有着异色的头发，和一双红色的眼睛。",
        "在这片绿色的细胞群里，你格格不入。",
        "刚拥有意识的你，有些失落，有些拘谨。",
        "你开始怀疑自己，也慢慢把自己封闭起来——",
        "从外表，到内心，你正在一点点变成一个「异类」。",
        "（靠近方糖，长按 E 吸收，让自己长大）",
    };

    // 幼年下：异变显现（第一次吸糖触发）
    static readonly string[] InfantEvolution = new[]
    {
        "一阵强烈的不适，毫无征兆地涌了上来。",
        "你的身体开始周期性地进化。",
        "血管般的脉络，一点一点缠上了你。",
        "肌肤也开始溃烂……",
        "你终于明白，你不再只是「和它们不一样」了。",
        "你正在变成某种，令这具身体恐惧的东西。",
    };

    // 成年上：NK 细胞追杀
    static readonly string[] IntroAdult = new[]
    {
        "你已经长成了另一副模样。",
        "专门消杀异常细胞的 NK 细胞，开始成群结队地追杀你。",
        "你不解，你害怕。你只是不想被抹去，只是自保。",
        "可——为了自保而杀死别的细胞，究竟是对，还是错？",
    };

    // 第三关（最终关）：真相 + 最后的抉择
    static readonly string[] IntroFinal = new[]
    {
        "你终于看清了这具身体的全貌。",
        "那些「敌人」，不过是在拼命保护宿主的细胞。",
        "而真正在吞噬它的，是你。",
        "通往出口的路上，你感到体内有什么在蠢动。",
        "最后的抉择，就在前面的过道里。",
    };

    /// <summary>最终抉择（第 3 关过道）描述：GROW / NO。</summary>
    public const string GrowChoiceDesc =
        "出口就在眼前。身体深处有个声音在低语：\n\n「吞掉眼前的一切，你会变得更强。」\n\nYES——接纳狂暴：力量暴涨、环境被污染，出口房刷出大量守卫，杀光后传送门才会显现。\nNO——拒绝力量：环境涤净变亮，隐藏的传送门直接显现。";

    /// <summary>结局：继续杀戮 → 癌细胞吞噬宿主，人体死亡。</summary>
    public const string EndingCancerWins =
        "你选择继续杀戮。\n癌细胞无限增殖，最终吞噬了宿主。\n人体死亡。";

    /// <summary>结局：消灭自己 → 癌症被清除，宿主存活。</summary>
    public const string EndingHostSurvives =
        "你选择消灭自己。\n癌细胞被清除，宿主的身体恢复了健康。\n宿主存活。";

    /// <summary>彩蛋：全程一块方糖都没吸收就通关（结尾追加显示）。</summary>
    public const string EasterEggNoSugar =
        "【彩蛋 · 空腹通关】\n你连一块方糖都没尝过，就走到了最后。\n饥饿的癌细胞，反而更可怕。";

    // ------- 最终抉择分支文案（第 3 关过道 GROW / NO） -------

    /// <summary>选 GROW：狂暴开场白。</summary>
    public static readonly string[] RageIntro = new[]
    {
        "你张开了嘴。",
        "污秽顺着血管倒灌进来，视野被染成浓稠的黑红。",
        "力量——从未如此清晰。你听见自己的细胞在尖叫着增殖。",
        "这具身体的守卫疯狂地涌了过来。",
        "杀光它们。然后，走进传送阵。",
    };

    /// <summary>选 GROW：狂暴波清完、传送阵解锁时——已停用（解锁是纯提示，不再触发对话，避免与 HitStop 抢时间缩放）。</summary>
    // public static readonly string[] RageWaveCleared = new[]
    // {
    //     "守卫的残骸沉入黑暗。",
    //     "传送阵亮了起来——身体深处，有什么东西已经死了。",
    // };

    /// <summary>选 NO：和平开场白（环境变亮）。</summary>
    public static readonly string[] PeaceIntro = new[]
    {
        "你闭上了嘴，把那个声音压了回去。",
        "污秽缓缓退去，血肉的灼痛平息了。",
        "眼前的一切亮了起来——柔和，温暖，像很早以前的阳光。",
        "隐藏的传送门，在你面前显现。",
        "走近它，按 E，走进传送门。",
    };

    /// <summary>GROW 结局 CG（对白演出版；之后可换成动画视频）。</summary>
    public static readonly string[] RageEndingCG = new[]
    {
        "传送阵的光吞没了你。",
        "你的族群沿着你撕开的裂口，蔓延到这具身体的每一个角落。",
        "心跳声越来越远，越来越轻。",
        "最后一刻，你想起自己最初只是想活下去。",
        "——人体死亡。",
    };

    /// <summary>NO 结局 CG（献祭对白演出版；之后可换成动画视频）。</summary>
    public static readonly string[] AltarRitualCG = new[]
    {
        "你爬上祭坛，把最后一点污秽交了出去。",
        "身体深处传来久违的、平稳的心跳。",
        "免疫细胞围拢过来，却没有攻击——它们认出了干净的你。",
        "你化作一粒微光，消散在温暖里。",
        "——宿主存活。",
    };
}
