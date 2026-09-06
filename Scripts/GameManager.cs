using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局游戏管理（单例）：警报等级、方糖计数、结局判定、玩家/敌人注册。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("运行时状态（无需手动设置）")]
    [Tooltip("全局警报等级（每吸一块糖 +1）")]
    public int alertLevel = 0;
    [Tooltip("本关方糖总数（方糖自己注册进来）")]
    public int totalSugar = 0;
    [Tooltip("已吸收方糖数")]
    public int absorbedSugar = 0;
    [Tooltip("当前关卡号（1~3，由场景名解析；过关后进下一关用）")]
    public int currentLevel = 1;

    [Header("参数")]
    [Tooltip("吸糖时，此半径内的普通细胞立即警觉")]
    public float alertRadius = 6f;

    [Header("手感反馈")]
    [Tooltip("吸收方糖时飞向角色的粒子颜色（淡黄，与方糖同色系）")]
    public Color sugarParticleColor = new Color(1f, 0.85f, 0.35f);
    [Tooltip("敌人逼近到多远时屏幕边缘开始泛红（世界距离）")]
    public float dangerRadius = 5f;
    [Tooltip("玩家受击震屏强度（世界单位，越大越抖，调小可减弱）")]
    public float hurtShakeAmount = 0.15f;
    [Tooltip("玩家受击震屏时长（秒）")]
    public float hurtShakeDuration = 0.2f;

    [Header("房间进度（敌人生效门控，运行时由 MapGenerator 填入）")]
    [Tooltip("各房间中心坐标（MapGenerator 运行时自动填入，无需手动设置）")]
    public List<Vector2> roomCenters = new List<Vector2>();
    [Tooltip("房间半边长（判断玩家进入房间用，= roomSize / 2）")]
    public float roomHalfSize = 8f;
    [Tooltip("玩家已到达的最远房间索引（运行时状态，0 = 出生房）")]
    public int furthestReachedRoom = 0;
    [Tooltip("出口房（传送门）在 roomCenters 里的索引（小地图标绿用，-1 = 无）")]
    public int portalRoomIndex = -1;

    [Header("成长时期（剧情用，由 StoryData 按关卡决定）")]
    [Tooltip("幼年 / 成年（第 1 关幼年，第 2 关起成年；第 3 步做视觉进化时用）")]
    public GrowthStage stage = GrowthStage.Infant;

    [Header("引用（拖拽赋值）")]
    [Tooltip("拖入场景里的 Player 物体")]
    public PlayerController player;
    [Tooltip("拖入场景里的 UI 物体")]
    public UIManager ui;

    [Header("最终抉择（第 3 关过道 GROW / NO）")]
    [Tooltip("是否处于狂暴模式（选了 GROW）")]
    public bool rageMode = false;
    [Tooltip("传送阵是否锁定（狂暴波未清完时锁住，按 E 无效）")]
    public bool portalLocked = false;
    [Tooltip("狂暴化：额外攻击力（加到玩家 attackDamage 上）")]
    public int rageBonusDamage = 2;
    [Tooltip("狂暴化：额外攻击范围（世界单位）")]
    public float rageBonusRange = 0.5f;
    [Tooltip("狂暴化：额外移动速度（世界单位/秒）")]
    public float rageBonusSpeed = 0.8f;
    [Tooltip("GROW 后的镜头背景色（近黑血色）")]
    public Color rageBackgroundColor = new Color(0.07f, 0.01f, 0.02f, 1f);
    [Tooltip("NO 后的镜头背景色（明亮暖色）")]
    public Color peaceBackgroundColor = new Color(0.90f, 0.82f, 0.68f, 1f);

    private List<EnemyCell> rageEnemies = new List<EnemyCell>(); // 狂暴波敌人（清完解锁传送阵）
    private bool rageBuffApplied;

    private List<EnemyCell> enemies = new List<EnemyCell>();

    private static readonly string[] CN = { "零", "一", "二", "三", "四", "五" };

    public Vector2 PlayerPosition => player != null ? (Vector2)player.transform.position : Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        WirePlayerDeath();
    }

    void Start()
    {
        // 剧情 / BGM 不再在这里自动播：改由 UIManager 在「点开始游戏」或「跳过开始界面直接开玩」时调用 StartLevel()。
        // 这样 Play 一进来停在开始界面时，不会提前出现对话和音乐。
    }

    /// <summary>把玩家死亡事件接到「失败结算」上（手动搭场景时由这里接线）。</summary>
    void WirePlayerDeath()
    {
        if (player == null) return;
        var h = player.GetComponent<Health>();
        if (h != null) h.onDeath += HandlePlayerDeath;
    }

    void Update()
    {
        TrackFurthestRoom();
        UpdateDanger();
        UpdateRageWave();
    }

    /// <summary>狂暴波清完 → 解锁传送阵并播一段收束台词。敌人死亡即 Destroy，引用变 null 即视为消灭。</summary>
    void UpdateRageWave()
    {
        if (!rageMode || !portalLocked) return;
        rageEnemies.RemoveAll(e => e == null);
        if (rageEnemies.Count > 0) return;

        portalLocked = false;
        if (MapGenerator.Instance != null) MapGenerator.Instance.ShowPortal(); // 清完守卫 → 传送门显现
        if (ui != null) ui.ShowMessage("传送阵解锁了");
        // 这里故意不触发剧情对话——按设计：传送阵解锁只是提示，剧情动画要等玩家自己走过来触发。
        // 之前的 PlayDialogue(RageWaveCleared) 会和 HitStop 抢 Time.timeScale，导致游戏永久定格。
    }

    // ---------- 剧情 ----------

    /// <summary>从当前场景名解析关卡号（Level_3 → 3；解析失败兜底 1）。</summary>
    int CurrentLevel()
    {
        string name = SceneManager.GetActiveScene().name;
        int i = name.LastIndexOf('_');
        if (i >= 0 && int.TryParse(name.Substring(i + 1), out int n)) return Mathf.Max(1, n);
        return 1;
    }

    /// <summary>游戏真正开始（点「开始游戏」或跳过开始界面）后：初始化关卡号/时期，起 BGM，播本关开场对话。</summary>
    public void StartLevel()
    {
        currentLevel = CurrentLevel();
        stage = StoryData.GetStage(currentLevel);

        // 攻击解锁永久保留：第 1 关吸第 2 块糖才解锁，第 2、3 关一进来就是已解锁
        if (player != null && player.stats != null)
            player.stats.attackUnlocked = (currentLevel > 1);

        // 已解锁攻击 → 主角已是「变换形态」（第 2、3 关进来直接就是变身状态）
        TryTransformPlayer();

        // 游戏真正开始后才起 BGM
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic();

        string[] intro = StoryData.GetIntro(currentLevel);
        if (intro != null && intro.Length > 0 && ui != null)
            ui.PlayDialogue(intro);
    }

    /// <summary>根据玩家位置更新「已到达的最远房间索引」。</summary>
    void TrackFurthestRoom()
    {
        if (player == null || roomCenters.Count == 0) return;
        Vector2 p = PlayerPosition;
        for (int i = 0; i < roomCenters.Count; i++)
        {
            Vector2 c = roomCenters[i];
            if (Mathf.Abs(p.x - c.x) <= roomHalfSize && Mathf.Abs(p.y - c.y) <= roomHalfSize)
            {
                if (i > furthestReachedRoom) furthestReachedRoom = i;
                return;
            }
        }
    }

    /// <summary>根据最近敌人距离更新「敌人逼近」危险覆盖层强度。</summary>
    void UpdateDanger()
    {
        if (ui == null) return;
        if (enemies.Count == 0) { ui.SetDanger(0f); return; }

        Vector2 p = PlayerPosition;
        float nearest = Mathf.Infinity;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            float d = Vector2.Distance(p, e.transform.position);
            if (d < nearest) nearest = d;
        }
        if (float.IsInfinity(nearest)) { ui.SetDanger(0f); return; }
        ui.SetDanger(Mathf.Clamp01(1f - nearest / dangerRadius));
    }

    /// <summary>敌人能否进入追击：玩家已武装（能攻击）且已到达该敌人所在房间。</summary>
    public bool CanEnemyActivate(int roomIndex)
    {
        if (player == null || player.stats == null) return false;
        return player.stats.attackUnlocked && roomIndex <= furthestReachedRoom;
    }

    /// <summary>主角变换形态：攻击已解锁时，把 DirectionalAnimator 切到「变换后的美术」。</summary>
    void TryTransformPlayer()
    {
        if (player == null || player.stats == null || !player.stats.attackUnlocked) return;
        var anim = player.GetComponent<DirectionalAnimator>();
        if (anim != null) anim.SetTransformed(true);
    }

    public void RegisterEnemy(EnemyCell e) { if (!enemies.Contains(e)) enemies.Add(e); }
    public void UnregisterEnemy(EnemyCell e) { enemies.Remove(e); }
    public void RegisterSugar() { totalSugar++; }

    /// <summary>吸收一块方糖。</summary>
    public void OnSugarAbsorbed(Vector2 pos)
    {
        absorbedSugar++;
        alertLevel++;
        player.stats.AbsorbSugar();

        // 刚吃够解锁数量的方糖 → 主角变换形态（换美术）
        TryTransformPlayer();

        foreach (var e in enemies)
            e.ForceAlert(pos, alertRadius);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayAlert();
        ui.ShowMessage("吸收了一块方糖！警报等级 " + alertLevel);

        // 吸收反馈：粒子飞向角色 + 角色缩放脉冲 + 警报屏幕泛红脉冲
        SimpleParticle.Collect(pos, sugarParticleColor, 10, player.transform, 4f, 0.2f, 0.5f);
        player.PulseScale();
        ui.PulseAlert();

        if (absorbedSugar >= totalSugar)
            ui.ShowMessage("已集齐所有方糖，前往交付房！");

        // 第一关第一次吸糖：幼年时期触发「异变显现」剧情（暂停游戏播放）
        if (player.stats != null && player.stats.sugarAbsorbed == 1 && stage == GrowthStage.Infant && ui != null)
        {
            var evo = StoryData.GetInfantEvolution();
            if (evo != null && evo.Length > 0) ui.PlayDialogue(evo);
        }
    }

    public void OnEnemyKilled()
    {
        player.stats.OnKill();
    }

    public void DamagePlayer(int dmg, Vector2 fromPos)
    {
        if (player == null) return;

        // 无敌窗口：玩家受击闪光未结束，本次攻击不扣血、不重复反馈
        if (!player.CanTakeDamage) return;

        var h = player.GetComponent<Health>();
        if (h != null) h.TakeDamage(dmg);

        // 玩家已死（刚进入失败结算）：不再做受击反馈
        if (h != null && h.currentHp <= 0) return;

        // 进入无敌窗口 + 红色闪光
        player.NotifyHurt();

        // 受击反馈：必击退（沿 玩家←攻击者 方向），力度由 Player 上的 hurtKnockbackForce 决定；
        // 失控时长很短（Player.hurtKnockbackTime），表达「被打了」但不长时间剥夺操作权
        Vector2 dir = (Vector2)player.transform.position - fromPos;
        player.ApplyKnockback(dir, player.hurtKnockbackForce);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayHurtKnockback();

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.HitStop(0.05f);
            CameraFollow.Instance.Shake(hurtShakeAmount, hurtShakeDuration);
        }
        SimpleParticle.Burst(player.transform.position, new Color(1f, 0.35f, 0.3f), 10, 5f, 0.25f, 0.35f);
    }

    public void HandlePlayerDeath()
    {
        ui.ShowEnding("被清除",
            "免疫系统最终清除了你。\n你没能走到最后。");
    }

    /// <summary>传送门是否可用（狂暴波没清完时锁定）。</summary>
    public bool CanUsePortal => !portalLocked;

    /// <summary>传送门交互：无需集齐方糖，按 E 直接过关。第 1/2 关弹过关弹窗（进下一关）。
    /// 第 3 关按最终抉择分支：YES（rageMode）→ 结局一视频 + 人体死亡；NO → 结局二图片 + 宿主存活。
    /// 全程一块糖都没吃就通关 → 追加彩蛋文案。</summary>
    public void TryEnterPortal()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDelivery();

        if (currentLevel < 3)
        {
            ui.ShowClear("第" + CN[currentLevel] + "关 通过",
                "击杀 " + player.stats.killCount + " · 吸收方糖 " + absorbedSugar + " / " + totalSugar);
        }
        else if (rageMode)
        {
            // 结局一（YES / 狂暴）：杀光守卫后进传送门
            string ending = StoryData.EndingCancerWins;
            if (absorbedSugar == 0) ending += "\n\n" + StoryData.EasterEggNoSugar;
            if (ui != null) ui.PlayEnding(ui.endingVideo1, "人体死亡", ending);
        }
        else
        {
            // 结局二（NO / 和平）：不打怪直接进传送门 → 显示图片 CG
            string ending = StoryData.EndingHostSurvives;
            if (absorbedSugar == 0) ending += "\n\n" + StoryData.EasterEggNoSugar;
            if (ui != null) ui.PlayEndingImage(ui.endingImage2, "宿主存活", ending);
        }
    }

    /// <summary>[已停用] NO 分支现在直接走传送门（TryEnterPortal 里 rageMode==false 分支），不再摆祭坛。保留备用。</summary>
    public void OnAltarSacrifice()
    {
        string ending = StoryData.EndingHostSurvives;
        if (absorbedSugar == 0) ending += "\n\n" + StoryData.EasterEggNoSugar;
        if (ui != null) ui.PlayDialogue(StoryData.AltarRitualCG, () => ui.ShowEnding("宿主存活", ending));
        else ui.ShowEnding("宿主存活", ending);
    }

    /// <summary>最终抉择（第 3 关过道）：GROW 进狂暴模式 / NO 进和平模式。</summary>
    public void ShowGrowChoice()
    {
        string desc = StoryData.GrowChoiceDesc;
        if (absorbedSugar == 0) desc += "\n\n" + StoryData.EasterEggNoSugar;
        ui.ShowChoice("最后的抉择", desc,
            "YES", "NO",
            OnChooseGrow, OnChooseNo);
    }

    /// <summary>选 GROW：环境变暗变红 + 玩家狂暴增益 + 出口房刷狂暴波，清完解锁传送阵。</summary>
    void OnChooseGrow()
    {
        rageMode = true;
        portalLocked = true;

        // 环境切换：镜头背景近黑血色 + 全屏黑红压暗遮罩渐入
        if (CameraFollow.Instance != null) CameraFollow.Instance.SetAmbience(rageBackgroundColor);
        if (ui != null) ui.SetAmbienceOverlay(new Color(0.30f, 0.01f, 0.05f, 0.45f));

        // 玩家狂暴增益（一次性）
        if (player != null && !rageBuffApplied)
        {
            rageBuffApplied = true;
            player.attackDamage += rageBonusDamage;
            player.attackRange += rageBonusRange;
            player.moveSpeed += rageBonusSpeed;
        }

        if (MapGenerator.Instance != null) MapGenerator.Instance.SpawnRageWave();
        if (ui != null) ui.PlayDialogue(StoryData.RageIntro);
    }

    /// <summary>选 NO：环境涤净变亮 + 拆传送阵摆祭坛，按 E 献祭走结局。</summary>
    void OnChooseNo()
    {
        rageMode = false;

        // 环境切换：镜头背景明亮暖色 + 轻微暖光提亮遮罩
        if (CameraFollow.Instance != null) CameraFollow.Instance.SetAmbience(peaceBackgroundColor);
        if (ui != null) ui.SetAmbienceOverlay(new Color(1f, 0.92f, 0.70f, 0.12f));

        // 不打怪：直接把隐藏的传送门显示出来
        if (MapGenerator.Instance != null) MapGenerator.Instance.ShowPortal();
        if (ui != null) ui.PlayDialogue(StoryData.PeaceIntro);
    }

    /// <summary>MapGenerator 生成狂暴波后注册进来（清完 → 解锁传送阵）。</summary>
    public void RegisterRageWave(List<EnemyCell> wave)
    {
        rageEnemies = wave ?? new List<EnemyCell>();
    }
}
