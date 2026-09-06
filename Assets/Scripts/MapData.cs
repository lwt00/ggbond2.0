using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 默认关卡布局工厂：提供「链式」布局骨架（每关从几套预设形状里轮流选），
/// 并给出每关的默认内容（方糖数 / 敌人类型+数量）+ 障碍。房间 i 与 i+1 之间自动生成通道。
/// 运行时兜底用；关卡工具（Editor）也会调用它生成默认关卡资产。
/// 内容按「计数式」配置（sugarCount / enemySpawns），实际位置由 MapGenerator 运行时散布。
/// </summary>
public static class MapData
{
    static readonly string[] CN = { "零", "一", "二", "三", "四", "五" };
    static readonly string[] RoomNames = { "出生房", "工作区A", "工作区B", "工作区C", "出口房" };

    // 障碍默认参数（障碍生成器工具可覆盖）
    public const int DefaultObstaclesPerRoom = 3;
    public const float DefaultObstacleMinSize = 1.5f;
    public const float DefaultObstacleMaxSize = 4f;
    public const float DefaultObstacleMargin = 3.5f;

    const float OpeningClearRadius = 3f;      // 门口保留半径（通道宽 4，留出通路不被障碍堵死）
    const float PlayerSpawnClearRadius = 2.5f; // 出生房中心（玩家出生点）保留半径

    /// <summary>5 套 5 房间链式形状（每套 10 个整数 = 5 个格点 x,y；乘 step = 世界坐标）。</summary>
    static readonly int[][] LayoutPresets =
    {
        new[] { 0,0, 0,1, 1,1, 1,0, 2,0 },  // 蛇形（原版）
        new[] { 0,0, 0,1, 0,2, 1,2, 2,2 },  // L 形
        new[] { 0,0, 1,0, 2,0, 2,1, 1,1 },  // 之字形
        new[] { 0,0, 1,0, 1,1, 0,1, 0,2 },  // 回字形
        new[] { 0,0, 1,0, 1,1, 2,1, 2,2 },  // 阶梯形
    };

    /// <summary>链式布局骨架（N 房 + 通道），按 shapeIndex 选形状、取前 roomCount 个格点；开口方向由相邻关系自动推导。</summary>
    static List<RoomDef> BuildChainLayout(float roomSize, float gap, int shapeIndex, int roomCount)
    {
        float s = roomSize + gap;
        Vector2 sz = new Vector2(roomSize, roomSize);
        int[] p = LayoutPresets[((shapeIndex % LayoutPresets.Length) + LayoutPresets.Length) % LayoutPresets.Length];
        roomCount = Mathf.Clamp(roomCount, 2, p.Length / 2); // 2 ~ 5

        var rooms = new List<RoomDef>();
        for (int i = 0; i < roomCount; i++)
        {
            rooms.Add(new RoomDef
            {
                name = RoomNames[Mathf.Min(i, RoomNames.Length - 1)],
                center = new Vector2(p[i * 2] * s, p[i * 2 + 1] * s),
                size = sz,
                isPortal = (i == roomCount - 1),
                openings = new List<Dir>(),
                sugarCount = 0,
                enemySpawns = new List<EnemySpawnDef>(),
                obstacles = new List<ObstacleDef>(),
            });
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (i > 0) rooms[i].openings.Add(DirBetween(rooms[i].center, rooms[i - 1].center));
            if (i < rooms.Count - 1) rooms[i].openings.Add(DirBetween(rooms[i].center, rooms[i + 1].center));
        }
        return rooms;
    }

    static Dir DirBetween(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0 ? Dir.East : Dir.West;
        return d.y > 0 ? Dir.North : Dir.South;
    }

    static EnemySpawnDef E(EnemyType type, int count) => new EnemySpawnDef { type = type, count = count };

    /// <summary>生成第 level 关的房间列表（含计数式内容 + 障碍）。</summary>
    public static List<RoomDef> BuildLevel(int level, float roomSize, float gap,
        int obstaclesPerRoom = DefaultObstaclesPerRoom,
        float obstacleMinSize = DefaultObstacleMinSize,
        float obstacleMaxSize = DefaultObstacleMaxSize,
        float obstacleMargin = DefaultObstacleMargin)
    {
        int roomCount = (level <= 1) ? 4 : 5;                       // 第 1 关 4 房（教学），其余 5 房
        int shapeIndex = (level <= 1) ? 0 : (level - 1) % LayoutPresets.Length;
        var rooms = BuildChainLayout(roomSize, gap, shapeIndex, roomCount);

        if (level <= 1)
        {
            // 第 1 关（教学）：房0 NPC + 1 糖（无敌人）；房1 1 糖 + 敌人（吸第 2 糖解锁攻击后追击）；房2 2 糖 + 敌人；房3 传送门。共 4 糖。
            rooms[0].hasNPC = true;
            rooms[0].sugarCount = 1;
            rooms[0].enemySpawns = new List<EnemySpawnDef>();

            rooms[1].sugarCount = 1;
            rooms[1].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.NeedleGirl, 2) };

            rooms[2].sugarCount = 2;
            rooms[2].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.GreenBag, 2), E(EnemyType.Capsule, 1) };

            rooms[3].sugarCount = 0;
            rooms[3].enemySpawns = new List<EnemySpawnDef>();
        }
        else if (level == 2)
        {
            rooms[0].sugarCount = 0; rooms[0].enemySpawns = new List<EnemySpawnDef>(); // 第 1 房空
            rooms[1].sugarCount = 2; rooms[1].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.NeedleGirl, 3) };
            rooms[2].sugarCount = 2; rooms[2].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.Capsule, 3), E(EnemyType.GreenBag, 2) };
            rooms[3].sugarCount = 2; rooms[3].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.Antigen, 3), E(EnemyType.NeedleGirl, 2) };
            rooms[4].sugarCount = 0; rooms[4].enemySpawns = new List<EnemySpawnDef>();
        }
        else
        {
            // 第 3 关：怪多、糖多（可自行在 Inspector 里再调）
            rooms[0].sugarCount = 0; rooms[0].enemySpawns = new List<EnemySpawnDef>();
            rooms[1].sugarCount = 3; rooms[1].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.NeedleGirl, 3), E(EnemyType.Capsule, 2) };
            rooms[2].sugarCount = 3; rooms[2].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.GreenBag, 3), E(EnemyType.Antigen, 3) };
            rooms[3].sugarCount = 3; rooms[3].enemySpawns = new List<EnemySpawnDef> { E(EnemyType.Capsule, 3), E(EnemyType.Antigen, 3), E(EnemyType.NeedleGirl, 2) };
            rooms[4].sugarCount = 0; rooms[4].enemySpawns = new List<EnemySpawnDef>();
        }

        GenerateObstacles(rooms, level, obstaclesPerRoom, obstacleMinSize, obstacleMaxSize, obstacleMargin);
        return rooms;
    }

    /// <summary>为已有房间列表（重新）生成障碍（固定种子，可复现）。只改障碍、不动布局和内容。</summary>
    public static void GenerateObstacles(List<RoomDef> rooms, int level,
        int obstaclesPerRoom, float obstacleMinSize, float obstacleMaxSize, float obstacleMargin)
    {
        Random.InitState(level * 104729 + 4567); // 独立种子：改障碍参数重生成也稳定
        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            if (r.isPortal) { r.obstacles = new List<ObstacleDef>(); continue; }
            bool spawnRoom = (i == 0);
            r.obstacles = ScatterObstacles(r, spawnRoom, obstaclesPerRoom, obstacleMinSize, obstacleMaxSize, obstacleMargin);
        }
    }

    /// <summary>为某关填默认逻辑参数（难度随关卡递增：追速/伤害/预警/攻速逐关收紧，玩家仍保有速度优势）。</summary>
    public static void ApplyLevelParams(LevelData data, int level)
    {
        data.displayName = "第" + CN[Mathf.Clamp(level, 1, 3)] + "关";
        data.enemyMaxHp = 2 + (level - 1) / 2;              // L1=2, L2=2, L3=3
        data.enemyChaseSpeed = (level <= 1) ? 2.6f : (level == 2 ? 3.0f : 3.2f); // 仍慢于主角(4)，可逃但难溜
        data.enemyDamage = (level <= 1) ? 8 : (level == 2 ? 10 : 15);
        data.enemyBulletSpeed = 7f + level;                  // 子弹越来越快：L1=8, L2=9, L3=10
        data.enemyTelegraphTime = (level <= 1) ? 0.6f : (level == 2 ? 0.5f : 0.4f); // 反应窗口逐关缩窄
        data.enemyAttackCooldown = (level <= 1) ? 1.8f : (level == 2 ? 1.5f : 1.2f);
        data.enemyHpGrowth = Mathf.Max(0, level - 2);       // 后几关：越深处的房间敌人越肉
        data.alertRadius = 6f;
        data.isFinalLevel = (level >= 3);
    }

    // ---------- 障碍生成 ----------

    static List<ObstacleDef> ScatterObstacles(RoomDef r, bool spawnRoom, int count, float minSize, float maxSize, float margin)
    {
        var list = new List<ObstacleDef>();
        float hw = r.size.x * 0.5f - margin;
        float hh = r.size.y * 0.5f - margin;
        if (hw <= 0.5f || hh <= 0.5f) return list;

        int attempts = 0;
        int maxAttempts = count * 40 + 40;
        while (list.Count < count && attempts < maxAttempts)
        {
            attempts++;
            Vector2 size = RollObstacleSize(minSize, maxSize);
            Vector2 c = new Vector2(r.center.x + Random.Range(-hw, hw), r.center.y + Random.Range(-hh, hh));

            if (RejectsNearOpenings(r, c, size)) continue;
            if (spawnRoom && OverlapsCircle(r.center, PlayerSpawnClearRadius, c, size)) continue;
            if (OverlapsAnyObstacle(list, c, size)) continue;

            list.Add(new ObstacleDef { center = c, size = size });
        }
        return list;
    }

    /// <summary>随机一个障碍尺寸：45% 长条掩体，其余方块。</summary>
    static Vector2 RollObstacleSize(float minSize, float maxSize)
    {
        float w, h;
        if (Random.value < 0.45f)
        {
            float longSide = Random.Range(minSize, maxSize);
            float shortSide = Random.Range(minSize, Mathf.Max(minSize, maxSize * 0.5f));
            if (Random.value < 0.5f) { w = longSide; h = shortSide; }
            else { w = shortSide; h = longSide; }
        }
        else
        {
            w = Random.Range(minSize, maxSize);
            h = Random.Range(minSize, maxSize);
        }
        return new Vector2(w, h);
    }

    /// <summary>障碍是否压到门口：压到则拒绝（保证通路）。</summary>
    static bool RejectsNearOpenings(RoomDef r, Vector2 c, Vector2 size)
    {
        float hw = r.size.x * 0.5f;
        float hh = r.size.y * 0.5f;
        foreach (var dir in r.openings)
        {
            Vector2 p;
            switch (dir)
            {
                case Dir.North: p = new Vector2(r.center.x, r.center.y + hh); break;
                case Dir.South: p = new Vector2(r.center.x, r.center.y - hh); break;
                case Dir.East: p = new Vector2(r.center.x + hw, r.center.y); break;
                default: p = new Vector2(r.center.x - hw, r.center.y); break;
            }
            if (OverlapsCircle(p, OpeningClearRadius, c, size)) return true;
        }
        return false;
    }

    static bool OverlapsAnyObstacle(List<ObstacleDef> list, Vector2 c, Vector2 size)
    {
        const float pad = 0.5f; // 障碍之间留一点缝
        foreach (var o in list)
            if (OverlapsRect(o.center, o.size, c, size, pad)) return true;
        return false;
    }

    /// <summary>圆（点）与 AABB 是否相交。</summary>
    static bool OverlapsCircle(Vector2 p, float radius, Vector2 c, Vector2 size)
    {
        Vector2 half = size * 0.5f;
        float dx = Mathf.Max(0f, Mathf.Abs(p.x - c.x) - half.x);
        float dy = Mathf.Max(0f, Mathf.Abs(p.y - c.y) - half.y);
        return (dx * dx + dy * dy) < (radius * radius);
    }

    static bool OverlapsRect(Vector2 c1, Vector2 s1, Vector2 c2, Vector2 s2, float pad)
    {
        return Mathf.Abs(c1.x - c2.x) < (s1.x + s2.x) * 0.5f + pad &&
               Mathf.Abs(c1.y - c2.y) < (s1.y + s2.y) * 0.5f + pad;
    }
}
