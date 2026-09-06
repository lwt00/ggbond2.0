using System.Collections.Generic;
using UnityEngine;

/// <summary>通道/门的方向。</summary>
public enum Dir { North, South, East, West }

/// <summary>敌人投放组：一种敌人类型 + 数量（房间内运行时随机散布）。</summary>
[System.Serializable]
public class EnemySpawnDef
{
    [Tooltip("敌人类型（4 种怪之一）")]
    public EnemyType type = EnemyType.NeedleGirl;
    [Tooltip("该类型数量")]
    public int count = 1;
    [Tooltip("该组每只敌人的血量覆盖（-1 = 用关卡默认 enemyMaxHp；正数 = 单独指定，如精英怪）")]
    public int hp = -1;
}

/// <summary>单个房间的定义（可序列化，供关卡数据在 Inspector 里编辑）。</summary>
[System.Serializable]
public class RoomDef
{
    [Tooltip("房间名（调试用）")]
    public string name;
    [Tooltip("房间中心（世界坐标，链式布局下由工具生成）")]
    public Vector2 center;
    [Tooltip("房间尺寸（宽, 高）")]
    public Vector2 size;
    [Tooltip("是否出口房（最后一房，中间放传送门）")]
    public bool isPortal;
    [Tooltip("是否放站桩 NPC（各关第 1 房教学用）")]
    public bool hasNPC;
    [Tooltip("与通道相连的方向（墙在此方向开口）")]
    public List<Dir> openings = new List<Dir>();
    [Tooltip("本房间方糖数量（运行时在房间内随机散布）")]
    public int sugarCount = 0;
    [Tooltip("本房间敌人（类型 + 数量，运行时在房间内随机散布）")]
    public List<EnemySpawnDef> enemySpawns = new List<EnemySpawnDef>();
    [Tooltip("房内障碍（用墙的美术生成，带碰撞体挡路）")]
    public List<ObstacleDef> obstacles = new List<ObstacleDef>();
}

/// <summary>单个障碍的定义（位置 + 尺寸，用墙的美术平铺生成）。</summary>
[System.Serializable]
public class ObstacleDef
{
    [Tooltip("障碍中心（世界坐标）")]
    public Vector2 center;
    [Tooltip("障碍尺寸（宽, 高）")]
    public Vector2 size;
}

/// <summary>
/// 关卡数据（ScriptableObject）：一份资产 = 一关。
/// 房间采用「链式布局」（第 i 个与第 i+1 个房间之间自动生成通道），
/// 布局形状可沿用默认蛇形，内容（方糖数 / 敌人类型+数量 / 传送门 / NPC）在 Inspector 里可视化配置。
/// 由 MapGenerator 读取生成；选关界面按关卡列表加载对应场景。
/// </summary>
[CreateAssetMenu(menuName = "GGJ/关卡数据", fileName = "Level_")]
public class LevelData : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("关卡显示名（选关界面显示）")]
    public string displayName = "第一关";
    [Tooltip("关卡描述（选关界面可选显示）")]
    public string description = "";

    [Header("房间内容")]
    [Tooltip("房间列表（按顺序链式相连：i 与 i+1 之间生成通道）")]
    public List<RoomDef> rooms = new List<RoomDef>();

    [Header("逻辑参数")]
    [Tooltip("敌人基础血量")]
    public int enemyMaxHp = 2;
    [Tooltip("敌人追击速度")]
    public float enemyChaseSpeed = 1.8f;
    [Tooltip("敌人攻击伤害")]
    public int enemyDamage = 10;
    [Tooltip("敌人子弹飞行速度（远程）")]
    public float enemyBulletSpeed = 8f;
    [Tooltip("敌人发射前红线瞄准时长（秒）")]
    public float enemyTelegraphTime = 0.4f;
    [Tooltip("敌人两次攻击的最小间隔（秒）；调大敌人更温和、调小更凶")]
    public float enemyAttackCooldown = 1.5f;
    [Tooltip("敌人每深入一个房间额外增加的血量（0 = 不递增）")]
    public int enemyHpGrowth = 0;
    [Tooltip("吸糖警报半径（此半径内敌人立即警觉）")]
    public float alertRadius = 6f;

    [Header("结局 / 剧情")]
    [Tooltip("是否最后一关（第 3 关：出口房传送门前会放红线抉择）")]
    public bool isFinalLevel = false;
    [Tooltip("本关 NPC 对话（第 1 房有 NPC 时，靠近按 E 播放；可后补）")]
    public List<string> npcDialogue = new List<string>();
}
