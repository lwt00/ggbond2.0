using UnityEngine;

/// <summary>
/// 音效管理器（手动挂到一个空物体上）：把音频文件拖进下面栏位即可播放。
///   - BGM / 走路音 用循环 AudioSource
///   - 其余一次性音效用 PlayOneShot
/// 各处调用 AudioManager.Instance.XXX()，没挂 AudioManager 时自动跳过（不报错）。
/// 下面「扩展音效」区是这次新整理出来的槽位，逐个把做好的音频拖进去即可；
/// 播放方法已经留好（PlayXxx()），等对应玩法做好后再接线。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("背景音乐 / 走路（循环播放）")]
    [Tooltip("背景音乐（自动循环）")]
    public AudioClip bgm;
    [Tooltip("走路 / 粘液移动音效（循环：移动时播放、停下停止）")]
    public AudioClip walk;

    [Header("角色音效")]
    [Tooltip("玩家攻击（吞噬）")]
    public AudioClip attack;
    [Tooltip("敌人攻击（撞到玩家）")]
    public AudioClip enemyAttack;
    [Tooltip("玩家受击")]
    public AudioClip playerHurt;

    [Header("世界交互音效")]
    [Tooltip("吸收方糖")]
    public AudioClip absorb;
    [Tooltip("警报等级上升")]
    public AudioClip alert;
    [Tooltip("敌人受击")]
    public AudioClip enemyHurt;
    [Tooltip("敌人死亡")]
    public AudioClip enemyDeath;
    [Tooltip("交付 / 结算")]
    public AudioClip delivery;

    [Header("UI 音效")]
    [Tooltip("按钮点击")]
    public AudioClip uiClick;

    // ================= 扩展音效（新做好的，逐个拖入） =================

    [Header("剧情 / 过场")]
    [Tooltip("打字音（开局输入病人档案 / 介绍背景时）")]
    public AudioClip typing;
    [Tooltip("Microscopic Growth")]
    public AudioClip microscopicGrowth;

    [Header("Boss / 关卡")]
    [Tooltip("Boss 登场")]
    public AudioClip bossIntro;
    [Tooltip("心跳声（可最后一关使用）")]
    public AudioClip heartbeat;
    [Tooltip("心跳声·长（残血时循环播放，增加紧迫感）")]
    public AudioClip heartbeatLong;
    [Tooltip("心跳停止倒计时（倒计时快结束时）")]
    public AudioClip heartbeatStop;

    [Header("升级 / 分裂")]
    [Tooltip("火箭发射（升级时）")]
    public AudioClip rocketLaunch;
    [Tooltip("泡泡（细胞分裂时）")]
    public AudioClip bubble;

    [Header("攻击 / 受击")]
    [Tooltip("挥剑音效（攻击）")]
    public AudioClip swordSwing;
    [Tooltip("投掷音效（攻击）")]
    public AudioClip throwAttack;
    [Tooltip("被攻击且被击退")]
    public AudioClip hurtKnockback;
    [Tooltip("被攻击且未被击退")]
    public AudioClip hurtNoKnockback;

    [Header("吸收 / 吞噬")]
    [Tooltip("咀嚼（开始吸收时每秒播放一次）")]
    public AudioClip chew;
    [Tooltip("吞咽（吸收成功时）")]
    public AudioClip swallow;

    [Header("击杀 / 破坏")]
    [Tooltip("击杀敌人")]
    public AudioClip kill;
    [Tooltip("器官被破坏（破坏墙壁等）")]
    public AudioClip organDestroy;

    [Header("胜利 / 失败")]
    [Tooltip("胜利声1（欢快）")]
    public AudioClip victory1;
    [Tooltip("胜利声2（沉重）")]
    public AudioClip victory2;
    [Tooltip("欢声和掌声（游戏胜利时）")]
    public AudioClip cheer;
    [Tooltip("失败音（惨叫版本）")]
    public AudioClip failScream;
    [Tooltip("失败音（血条归零显示失败界面，传统版本）")]
    public AudioClip failTraditional;

    [Header("UI 点击（两个版本）")]
    [Tooltip("UI点击音1")]
    public AudioClip uiClick1;
    [Tooltip("UI点击音2")]
    public AudioClip uiClick2;

    private AudioSource musicSource;   // BGM（循环）
    private AudioSource walkSource;    // 走路（循环）
    private AudioSource sfxSource;     // 一次性音效
    private AudioSource typingSource;  // 打字音（循环，响 1 秒后自动停）
    private Coroutine typingCo;        // 打字音停止协程

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        walkSource = gameObject.AddComponent<AudioSource>();
        walkSource.loop = true;
        walkSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        typingSource = gameObject.AddComponent<AudioSource>();
        typingSource.loop = true;
        typingSource.playOnAwake = false;
    }

    // BGM 不在这里自动播：改由 GameManager 在「游戏真正开始（点开始游戏 / 跳过开始界面）」时调用 PlayMusic()。
    // 这样 Play 一进来停在开始界面时是静音的。

    /// <summary>开始播放背景音乐（bgm 为空则跳过）。</summary>
    public void PlayMusic()
    {
        if (bgm == null || musicSource == null) return;
        musicSource.clip = bgm;
        musicSource.Play();
    }

    /// <summary>走路音：moving=true 开始循环，false 停止。</summary>
    public void PlayWalk(bool moving)
    {
        if (walk == null || walkSource == null) return;
        if (moving && !walkSource.isPlaying)
        {
            walkSource.clip = walk;
            walkSource.Play();
        }
        else if (!moving)
        {
            walkSource.Stop();
        }
    }

    /// <summary>播放一次性音效。</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // ---- 原有便捷方法（各组件直接调用）----
    public void PlayAttack() => PlaySFX(attack);
    public void PlayEnemyAttack() => PlaySFX(enemyAttack);
    public void PlayPlayerHurt() => PlaySFX(playerHurt);
    public void PlayAbsorb() => PlaySFX(absorb);
    public void PlayAlert() => PlaySFX(alert);
    public void PlayEnemyHurt() => PlaySFX(enemyHurt);
    public void PlayEnemyDeath() => PlaySFX(enemyDeath);
    public void PlayDelivery() => PlaySFX(delivery);
    public void PlayUIClick() => PlaySFX(uiClick);

    // ---- 新增便捷方法（扩展音效，等对应玩法做好后调用）----
    public void PlayTyping() => PlaySFX(typing);
    /// <summary>循环播放打字音 duration 秒后自动停止（每行文字开始时调用，覆盖旧协程重新计时）。</summary>
    public void PlayTypingLoop(float duration = 1f)
    {
        if (typing == null || typingSource == null) return;
        typingSource.clip = typing;
        typingSource.Play();
        if (typingCo != null) StopCoroutine(typingCo);
        typingCo = StartCoroutine(StopTypingAfter(duration));
    }
    /// <summary>立即停止打字音。</summary>
    public void StopTyping()
    {
        if (typingSource != null) typingSource.Stop();
    }
    System.Collections.IEnumerator StopTypingAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (typingSource != null) typingSource.Stop();
        typingCo = null;
    }
    public void PlayMicroscopicGrowth() => PlaySFX(microscopicGrowth);
    public void PlayBossIntro() => PlaySFX(bossIntro);
    public void PlayHeartbeat() => PlaySFX(heartbeat);
    public void PlayHeartbeatLong() => PlaySFX(heartbeatLong);
    public void PlayHeartbeatStop() => PlaySFX(heartbeatStop);
    public void PlayRocketLaunch() => PlaySFX(rocketLaunch);
    public void PlayBubble() => PlaySFX(bubble);
    public void PlaySwordSwing() => PlaySFX(swordSwing);
    public void PlayThrowAttack() => PlaySFX(throwAttack);
    public void PlayHurtKnockback() => PlaySFX(hurtKnockback);
    public void PlayHurtNoKnockback() => PlaySFX(hurtNoKnockback);
    public void PlayChew() => PlaySFX(chew);
    public void PlaySwallow() => PlaySFX(swallow);
    public void PlayKill() => PlaySFX(kill);
    public void PlayOrganDestroy() => PlaySFX(organDestroy);
    public void PlayVictory1() => PlaySFX(victory1);
    public void PlayVictory2() => PlaySFX(victory2);
    public void PlayCheer() => PlaySFX(cheer);
    public void PlayFailScream() => PlaySFX(failScream);
    public void PlayFailTraditional() => PlaySFX(failTraditional);
    public void PlayUIClick1() => PlaySFX(uiClick1);
    public void PlayUIClick2() => PlaySFX(uiClick2);
}
