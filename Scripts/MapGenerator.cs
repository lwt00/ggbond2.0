using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成器（2D 俯视）：挂在「Map」空物体上，运行时按 MapData 生成整张地图。
/// 房间地板、通道、墙体（带门洞）、障碍、方糖、普通细胞、交付点。
/// 尺寸/颜色都在 Inspector 可配；美术贴图可直接拖进 Sprite 栏（留空则用纯色方块）。
/// </summary>
public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance { get; private set; }

    [Header("地图尺寸")]
    [Tooltip("房间边长")]
    public float roomSize = 16f;
    [Tooltip("通道宽度")]
    public float corridorWidth = 4f;
    [Tooltip("相邻房间之间通道的长度")]
    public float gap = 6f;
    [Tooltip("墙的厚度")]
    public float wallThickness = 1f;

    [Header("实体尺寸（调小让角色/方糖相对地图更小）")]
    [Tooltip("普通细胞边长")]
    public float enemySize = 0.6f;
    [Tooltip("方糖边长")]
    public float sugarSize = 0.6f;
    [Tooltip("传送门边长")]
    public float portalSize = 1.4f;
    [Tooltip("NPC 边长")]
    public float npcSize = 1.0f;

    [Header("关卡数据")]
    [Tooltip("本关的关卡数据（ScriptableObject，选关场景/关卡工具会指定）。留空 = 用默认第 1 关。")]
    public LevelData levelData;

    [Header("地砖尺寸（运行时实时调）")]
    [Tooltip("地板贴图一个重复单元的世界尺寸。你的地板图一张每边 12 块砖：填 12 = 每块砖正好 1 米。运行时可在 Inspector 直接拖，实时重建。")]
    public float floorTileSize = 12f;
    [Tooltip("墙/障碍贴图一个重复单元的世界尺寸。血肉斑点图建议 3~4：有肌理感又不糊。运行时可在 Inspector 直接拖，实时重建。")]
    public float wallTileSize = 3f;

    [Header("房间连接标记（门口高亮，帮玩家找路）")]
    [Tooltip("是否在门口画连接标记（半透明亮条，标出本房通往哪个方向）")]
    public bool showOpeningMarkers = true;
    [Tooltip("门口标记颜色（铺在门口地板上方，建议半透明亮色）")]
    public Color openingMarkerColor = new Color(0.25f, 1f, 0.85f, 0.5f);

    [Header("颜色（无贴图时用）")]
    [Tooltip("地板颜色")]
    public Color floorColor = new Color(0.18f, 0.20f, 0.24f);
    [Tooltip("墙体颜色")]
    public Color wallColor = new Color(0.10f, 0.10f, 0.12f);
    [Tooltip("通道地面颜色（复用同一张地板图平铺）")]
    public Color corridorColor = new Color(0.15f, 0.16f, 0.19f);
    [Tooltip("方糖颜色")]
    public Color sugarColor = new Color(1f, 0.85f, 0.35f);
    [Tooltip("针管女颜色（无动画帧时用）")]
    public Color enemyColor = new Color(0.35f, 0.75f, 1f);
    [Tooltip("传送门颜色")]
    public Color portalColor = new Color(0.55f, 0.55f, 1f);
    [Tooltip("NPC 颜色")]
    public Color npcColor = new Color(0.9f, 0.9f, 1f);
    [Tooltip("红线抉择（最终关）颜色")]
    public Color finalChoiceColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("祭坛颜色（和平模式出现在出口房）")]
    public Color altarColor = new Color(1f, 0.9f, 0.6f);

    [Header("狂暴波（最终关 YES 分支：杀光才显现传送门）")]
    [Tooltip("YES 后出口房刷出的守卫数量（「大量怪物」）")]
    public int rageWaveCount = 10;

    [Header("美术贴图（拖拽赋值；留空 = 用纯色方块）")]
    [Tooltip("房间地板贴图（平铺）")]
    public Sprite floorSprite;
    [Tooltip("墙体贴图（平铺）")]
    public Sprite wallSprite;
    [Tooltip("方糖贴图")]
    public Sprite sugarSprite;
    [Tooltip("传送门贴图")]
    public Sprite portalSprite;
    [Tooltip("最终关（第 3 关）传送门贴图（留空 = 复用上面的 portalSprite）")]
    public Sprite finalPortalSprite;
    [Tooltip("NPC 贴图")]
    public Sprite npcSprite;
    [Tooltip("针管女朝左走的动画帧（多张则逐帧播放）；留空 = 纯色方块")]
    public Sprite[] enemyLeftFrames;
    [Tooltip("针管女朝右走的动画帧（多张则逐帧播放）；留空 = 纯色方块")]
    public Sprite[] enemyRightFrames;
    [Tooltip("针管女攻击序列帧（正面，不分左右）；留空 = 攻击不播动画")]
    public Sprite[] enemyAttack;

    [Header("绿色袋子（远程）美术")]
    [Tooltip("绿色袋子朝左走的动画帧；留空 = 纯色方块")]
    public Sprite[] greenBagLeftFrames;
    [Tooltip("绿色袋子朝右走的动画帧；留空 = 纯色方块")]
    public Sprite[] greenBagRightFrames;
    [Tooltip("绿色袋子攻击序列帧（正面，不分左右）；留空 = 攻击不播动画")]
    public Sprite[] greenBagAttack;
    [Tooltip("绿色袋子颜色（无贴图时）")]
    public Color greenBagColor = new Color(0.4f, 0.9f, 0.45f);

    [Header("胶囊（近战）美术")]
    [Tooltip("胶囊朝左走的动画帧；留空 = 纯色方块")]
    public Sprite[] capsuleLeftFrames;
    [Tooltip("胶囊朝右走的动画帧；留空 = 纯色方块")]
    public Sprite[] capsuleRightFrames;
    [Tooltip("胶囊攻击序列帧（正面，不分左右）；留空 = 攻击不播动画")]
    public Sprite[] capsuleAttack;
    [Tooltip("胶囊颜色（无贴图时）")]
    public Color capsuleColor = new Color(1f, 0.7f, 0.25f);

    [Header("抗原（近战）美术")]
    [Tooltip("抗原朝左走的动画帧；留空 = 纯色方块")]
    public Sprite[] antigenLeftFrames;
    [Tooltip("抗原朝右走的动画帧；留空 = 纯色方块")]
    public Sprite[] antigenRightFrames;
    [Tooltip("抗原攻击序列帧（正面，不分左右）；留空 = 攻击不播动画")]
    public Sprite[] antigenAttack;
    [Tooltip("抗原颜色（无贴图时）")]
    public Color antigenColor = new Color(0.95f, 0.4f, 0.55f);

    private List<RoomDef> currentRooms;
    private Transform tileRoot;
    private float lastFloorTileSize;
    private float lastWallTileSize;
    private GameObject portalObject;   // 出口房传送门（和平模式要拆掉它换祭坛）
    private RoomDef portalRoomRef;     // 出口房引用（狂暴刷怪用）

    void Awake() { Instance = this; }

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        currentRooms = (levelData != null && levelData.rooms != null && levelData.rooms.Count > 0)
            ? levelData.rooms
            : MapData.BuildLevel(1, roomSize, gap);

        // 每关逻辑参数（攻击解锁数、警报半径等）
        ApplyLevelParams();

        // 把房间中心和半边长同步给 GameManager（敌人生效门控用）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.roomCenters = new List<Vector2>();
            foreach (var r in currentRooms) GameManager.Instance.roomCenters.Add(r.center);
            GameManager.Instance.roomHalfSize = roomSize * 0.5f;
            GameManager.Instance.portalRoomIndex = currentRooms.FindIndex(r => r.isPortal);
        }

        // 地板/通道/墙统一放进「Tiles」子物体，方便改 tileSize 时整体重建
        if (tileRoot == null)
        {
            var tgo = new GameObject("Tiles");
            tgo.transform.SetParent(transform, false);
            tileRoot = tgo.transform;
        }
        BuildTileSurfaces();

        // 实体（方糖 / 敌人 / NPC，按房间顺序 tier 递增；位置在房间内随机散布、避开障碍和门口）
        for (int i = 0; i < currentRooms.Count; i++)
            SpawnRoomContent(currentRooms[i], i);

        // 出口房：传送门（最后一关还有过道中的最终抉择）
        var portal = currentRooms.Find(r => r.isPortal);
        if (portal != null)
        {
            portalRoomRef = portal;
            SpawnPortal(portal.center);
            if (levelData != null && levelData.isFinalLevel) SpawnFinalChoice(portal);
        }

        lastFloorTileSize = floorTileSize;
        lastWallTileSize = wallTileSize;
    }

    /// <summary>生成所有「瓦片表面」：房间地板 + 通道 + 墙体（不含实体）。改 tileSize 时单独重建这部分。</summary>
    void BuildTileSurfaces()
    {
        foreach (var r in currentRooms)
            CreateFloor("Floor_" + r.name, r.center, r.size, floorColor);

        BuildCorridors(currentRooms);

        foreach (var r in currentRooms)
            BuildWalls(r);

        foreach (var r in currentRooms)
            SpawnObstacles(r);

        BuildOpeningMarkers();
    }

    void Update()
    {
        // 运行时在 Inspector 拖 floorTileSize / wallTileSize → 实时重建地板/墙，方便可视化调纹理大小
        if (!Mathf.Approximately(floorTileSize, lastFloorTileSize) ||
            !Mathf.Approximately(wallTileSize, lastWallTileSize))
            RebuildTileSurfaces();
    }

    void RebuildTileSurfaces()
    {
        if (tileRoot == null) return;
        for (int i = tileRoot.childCount - 1; i >= 0; i--)
            Destroy(tileRoot.GetChild(i).gameObject);
        BuildTileSurfaces();
        lastFloorTileSize = floorTileSize;
        lastWallTileSize = wallTileSize;
    }

    /// <summary>把关卡逻辑参数应用到运行时（警报半径）。攻击解锁数不在这里管，直接读 Player 上的 PlayerStats.attackUnlockCount。</summary>
    void ApplyLevelParams()
    {
        if (levelData == null) return;
        if (GameManager.Instance != null)
            GameManager.Instance.alertRadius = levelData.alertRadius;
    }

    void BuildCorridors(List<RoomDef> rooms)
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Vector2 a = rooms[i].center;
            Vector2 b = rooms[i + 1].center;
            Vector2 mid = (a + b) * 0.5f;

            bool vertical = Mathf.Approximately(a.x, b.x); // 竖直通道（南北）
            // 走廊地面两端各伸进房间一点，盖住房间地板与走廊的接缝，避免漏出背景
            float extend = wallThickness;
            Vector2 size = vertical
                ? new Vector2(corridorWidth, gap + extend * 2f)
                : new Vector2(gap + extend * 2f, corridorWidth);
            CreateFloor("Corridor_" + i, mid, size, corridorColor); // 通道地面复用「地砖」

            // 通道两侧墙：密封通道边缘，角色/怪物只能待在房间和通道内
            float halfW = corridorWidth * 0.5f;
            if (vertical)
            {
                // 竖直通道：左右两侧各一道竖墙
                CreateWall("CorridorWall_" + i + "_L", new Vector2(mid.x - halfW, mid.y), new Vector2(wallThickness, gap));
                CreateWall("CorridorWall_" + i + "_R", new Vector2(mid.x + halfW, mid.y), new Vector2(wallThickness, gap));
            }
            else
            {
                // 水平通道：上下两侧各一道横墙
                CreateWall("CorridorWall_" + i + "_B", new Vector2(mid.x, mid.y - halfW), new Vector2(gap, wallThickness));
                CreateWall("CorridorWall_" + i + "_T", new Vector2(mid.x, mid.y + halfW), new Vector2(gap, wallThickness));
            }
        }
    }

    void BuildWalls(RoomDef r)
    {
        float hw = r.size.x * 0.5f;
        float hh = r.size.y * 0.5f;
        float open = corridorWidth;

        // 北墙（顶部，水平方向）
        BuildWallEdge(r, Dir.North, new Vector2(r.center.x, r.center.y + hh), new Vector2(r.size.x, wallThickness), true, open);
        // 南墙
        BuildWallEdge(r, Dir.South, new Vector2(r.center.x, r.center.y - hh), new Vector2(r.size.x, wallThickness), true, open);
        // 东墙
        BuildWallEdge(r, Dir.East, new Vector2(r.center.x + hw, r.center.y), new Vector2(wallThickness, r.size.y), false, open);
        // 西墙
        BuildWallEdge(r, Dir.West, new Vector2(r.center.x - hw, r.center.y), new Vector2(wallThickness, r.size.y), false, open);
    }

    /// <summary>生成一条墙边；有门洞则拆成两段绕开。horizontal=true 表示墙沿水平方向（南北墙）。</summary>
    void BuildWallEdge(RoomDef r, Dir dir, Vector2 center, Vector2 fullSize, bool horizontal, float open)
    {
        bool hasOpening = r.openings.Contains(dir);

        if (!hasOpening)
        {
            CreateWall("Wall_" + r.name + "_" + dir, center, fullSize);
            return;
        }

        float length = horizontal ? fullSize.x : fullSize.y;
        float segLen = (length - open) * 0.5f;
        float segCenter = (length - segLen) * 0.5f;

        if (horizontal)
        {
            CreateWall("Wall_" + r.name + "_" + dir + "_L", center + new Vector2(-segCenter, 0), new Vector2(segLen, fullSize.y));
            CreateWall("Wall_" + r.name + "_" + dir + "_R", center + new Vector2(segCenter, 0), new Vector2(segLen, fullSize.y));
        }
        else
        {
            CreateWall("Wall_" + r.name + "_" + dir + "_B", center + new Vector2(0, -segCenter), new Vector2(fullSize.x, segLen));
            CreateWall("Wall_" + r.name + "_" + dir + "_T", center + new Vector2(0, segCenter), new Vector2(fullSize.x, segLen));
        }
    }

    /// <summary>地板块：有 floorSprite 就平铺（砖，用白色不染色、美术原样显示），否则纯色方块。</summary>
    GameObject CreateFloor(string name, Vector2 pos, Vector2 size, Color color)
    {
        if (floorSprite != null)
            return ColorBlockFactory.CreateTiledBlock(name, pos, size, Color.white, tileRoot, 0, floorSprite, floorTileSize);
        return ColorBlockFactory.CreateBlock(name, pos, size, color, tileRoot);
    }

    /// <summary>墙块：有 wallSprite 就平铺（白色不染色、美术原样显示），否则纯色方块（带碰撞体）。排序层比地板高（1），避免障碍被地板盖住看不见。</summary>
    GameObject CreateWall(string name, Vector2 pos, Vector2 size)
    {
        if (wallSprite != null)
            return ColorBlockFactory.CreateTiledBlock(name, pos, size, Color.white, tileRoot, 1, wallSprite, wallTileSize, addCollider: true);
        return ColorBlockFactory.CreateBlock(name, pos, size, wallColor, tileRoot, true, 1);
    }

    /// <summary>房间内障碍：复用墙的美术和碰撞体（在房间里挡路、做掩体）。</summary>
    void SpawnObstacles(RoomDef r)
    {
        if (r.obstacles == null) return;
        for (int i = 0; i < r.obstacles.Count; i++)
        {
            var o = r.obstacles[i];
            CreateWall("Obstacle_" + r.name + "_" + i, o.center, o.size);
        }
    }

    /// <summary>在每个门口画一条半透明亮条，标出「本房通往哪个方向」，帮玩家不迷路。</summary>
    void BuildOpeningMarkers()
    {
        if (!showOpeningMarkers) return;
        foreach (var r in currentRooms)
        {
            float hw = r.size.x * 0.5f;
            float hh = r.size.y * 0.5f;
            foreach (var dir in r.openings)
            {
                Vector2 pos;
                bool horizontal;
                switch (dir)
                {
                    case Dir.North: pos = new Vector2(r.center.x, r.center.y + hh); horizontal = true; break;
                    case Dir.South: pos = new Vector2(r.center.x, r.center.y - hh); horizontal = true; break;
                    case Dir.East: pos = new Vector2(r.center.x + hw, r.center.y); horizontal = false; break;
                    default: pos = new Vector2(r.center.x - hw, r.center.y); horizontal = false; break;
                }
                Vector2 size = horizontal
                    ? new Vector2(corridorWidth, wallThickness * 2.5f)
                    : new Vector2(wallThickness * 2.5f, corridorWidth);
                ColorBlockFactory.CreateBlock("Opening_" + r.name + "_" + dir, pos, size, openingMarkerColor, tileRoot, false, 1);
            }
        }
    }

    void SpawnSugar(Vector2 pos)
    {
        var go = ColorBlockFactory.CreateBlock("SugarCube", pos, new Vector2(sugarSize, sugarSize), sugarColor, transform, false, 3, sprite: sugarSprite);
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 2f; // 交互范围（世界半径约 2 * sugarSize）
        go.AddComponent<SugarCube>();
    }

    EnemyCell SpawnEnemy(Vector2 pos, int tier, EnemyType type, int hpOverride = -1)
    {
        var art = GetEnemyArt(type);
        var go = ColorBlockFactory.CreateBlock("EnemyCell", pos, new Vector2(enemySize, enemySize), art.color, transform, false, 3);
        // 碰撞体半径与视觉一致：视觉方块边长 = enemySize，圆形碰撞体取「内切圆」直径 = enemySize，
        // 世界半径 = enemySize * 0.5f。避免默认半径 0.5（直径 1）比视觉大近一倍、卡住玩家/互相卡。
        var ecCol = go.AddComponent<CircleCollider2D>();
        ecCol.radius = enemySize * 0.5f; // 与玩家/墙碰撞（非 trigger）
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 平滑移动，减少追击时的抖动

        // 有行走帧或攻击帧就挂方向动画（全空则保持纯色方块）
        if ((art.left != null && art.left.Length > 0) ||
            (art.right != null && art.right.Length > 0) ||
            (art.attack != null && art.attack.Length > 0))
        {
            var anim = go.AddComponent<DirectionalAnimator>();
            anim.left = art.left;
            anim.right = art.right;
            anim.attack = art.attack;   // 攻击帧留空则攻击时不播动画
            anim.size = enemySize;
            anim.sortingOrder = 3;
            anim.tint = Color.white;
            anim.startFacing = DirectionalAnimator.Facing.Right;
        }

        var e = go.AddComponent<EnemyCell>();
        e.roomIndex = tier; // 所属房间索引（用于「到达房间后才激活」门控）
        e.type = type;
        // 针管女、绿色袋子 = 远程；胶囊、抗原 = 近战
        e.attackMode = (type == EnemyType.Capsule || type == EnemyType.Antigen) ? EnemyAttackMode.Melee : EnemyAttackMode.Ranged;
        if (levelData != null)
        {
            // 每关敌人属性：基础值 + 越深处的房间越肉；EnemySpawnDef.hp > 0 时单独覆盖本只血量
            e.maxHp = hpOverride > 0 ? hpOverride : levelData.enemyMaxHp + levelData.enemyHpGrowth * tier;
            e.chaseSpeed = levelData.enemyChaseSpeed;
            e.damage = levelData.enemyDamage;
            e.bulletSpeed = levelData.enemyBulletSpeed;
            e.telegraphTime = levelData.enemyTelegraphTime;
            e.attackCooldown = levelData.enemyAttackCooldown;
        }
        return e;
    }

    struct EnemyArt { public Sprite[] left, right, attack; public Color color; }

    /// <summary>按敌人类型取对应的行走帧、攻击帧与占位颜色。</summary>
    EnemyArt GetEnemyArt(EnemyType type)
    {
        var a = new EnemyArt();
        switch (type)
        {
            case EnemyType.GreenBag:
                a.left = greenBagLeftFrames; a.right = greenBagRightFrames;
                a.attack = greenBagAttack;
                a.color = greenBagColor; break;
            case EnemyType.Capsule:
                a.left = capsuleLeftFrames; a.right = capsuleRightFrames;
                a.attack = capsuleAttack;
                a.color = capsuleColor; break;
            case EnemyType.Antigen:
                a.left = antigenLeftFrames; a.right = antigenRightFrames;
                a.attack = antigenAttack;
                a.color = antigenColor; break;
            default: // NeedleGirl（复用原来的 enemyLeftFrames/enemyRightFrames/enemyColor）
                a.left = enemyLeftFrames; a.right = enemyRightFrames;
                a.attack = enemyAttack;
                a.color = enemyColor; break;
        }
        return a;
    }

    /// <summary>按房间的计数式配置（sugarCount / enemySpawns / hasNPC）散布并生成方糖、敌人、NPC。</summary>
    void SpawnRoomContent(RoomDef r, int tier)
    {
        var points = ScatterContent(r, r.sugarCount + TotalEnemyCount(r));
        int idx = 0;

        for (int s = 0; s < r.sugarCount; s++) SpawnSugar(points[idx++]);

        if (r.enemySpawns != null)
        {
            foreach (var group in r.enemySpawns)
            {
                if (group == null) continue;
                for (int k = 0; k < group.count; k++) SpawnEnemy(points[idx++], tier, group.type, group.hp);
            }
        }

        // NPC 摆在房间右侧一点（第 1 房教学用；与方糖/玩家出生点错开）
        if (r.hasNPC) SpawnNPC(r.center + new Vector2(r.size.x * 0.28f, 0f));
    }

    static int TotalEnemyCount(RoomDef r)
    {
        int n = 0;
        if (r.enemySpawns != null) foreach (var g in r.enemySpawns) if (g != null) n += g.count;
        return n;
    }

    /// <summary>在房间内随机撒 count 个点（避开障碍与门口、彼此别叠太近），供方糖/敌人用。</summary>
    List<Vector2> ScatterContent(RoomDef r, int count)
    {
        var list = new List<Vector2>();
        float hw = r.size.x * 0.5f - 3f; // 留 3 单位边距，别贴墙
        float hh = r.size.y * 0.5f - 3f;
        if (hw <= 0.5f || hh <= 0.5f) hw = hh = 1f;

        int attempts = 0, max = count * 60 + 60;
        while (list.Count < count && attempts < max)
        {
            attempts++;
            Vector2 p = new Vector2(r.center.x + Random.Range(-hw, hw), r.center.y + Random.Range(-hh, hh));
            if (OverlapsObstacle(r, p) || NearOpening(r, p)) continue;
            if (OverlapsPoint(list, p, 1.2f)) continue;
            list.Add(p);
        }
        while (list.Count < count) list.Add(r.center); // 兜底：撒不够就落在房间中心
        return list;
    }

    bool OverlapsObstacle(RoomDef r, Vector2 p)
    {
        if (r.obstacles == null) return false;
        foreach (var o in r.obstacles)
        {
            Vector2 half = o.size * 0.5f;
            if (Mathf.Abs(p.x - o.center.x) < half.x + 1f && Mathf.Abs(p.y - o.center.y) < half.y + 1f) return true;
        }
        return false;
    }

    bool NearOpening(RoomDef r, Vector2 p)
    {
        float hw = r.size.x * 0.5f;
        float hh = r.size.y * 0.5f;
        foreach (var dir in r.openings)
        {
            Vector2 o;
            switch (dir)
            {
                case Dir.North: o = new Vector2(r.center.x, r.center.y + hh); break;
                case Dir.South: o = new Vector2(r.center.x, r.center.y - hh); break;
                case Dir.East: o = new Vector2(r.center.x + hw, r.center.y); break;
                default: o = new Vector2(r.center.x - hw, r.center.y); break;
            }
            if (Vector2.Distance(p, o) < 3f) return true;
        }
        return false;
    }

    static bool OverlapsPoint(List<Vector2> list, Vector2 p, float minDist)
    {
        foreach (var q in list) if (Vector2.Distance(p, q) < minDist) return true;
        return false;
    }

    void SpawnPortal(Vector2 pos)
    {
        // 最终关的传送门用独立贴图（finalPortalSprite），没拖就复用通用 portalSprite
        Sprite art = (levelData != null && levelData.isFinalLevel && finalPortalSprite != null)
            ? finalPortalSprite : portalSprite;
        portalObject = ColorBlockFactory.CreateBlock("Portal", pos, new Vector2(portalSize, portalSize), portalColor, transform, false, 2, sprite: art);
        var col = portalObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 1.4f; // 交互范围
        portalObject.AddComponent<Portal>();

        // 最终关：传送门一开始隐藏（无贴图），等 YES 杀光守卫 / NO 直接显示后才显现
        if (levelData != null && levelData.isFinalLevel)
            portalObject.SetActive(false);
    }

    /// <summary>显示传送门（最终关 YES 清完守卫 / NO 直接显示时调用）。</summary>
    public void ShowPortal()
    {
        if (portalObject != null) portalObject.SetActive(true);
    }

    void SpawnNPC(Vector2 pos)
    {
        var go = ColorBlockFactory.CreateBlock("NPC", pos, new Vector2(npcSize, npcSize), npcColor, transform, false, 3, sprite: npcSprite);
        // NPC 只是站桩贴图，不挡路：不加实体碰撞，只留一个交互触发器，靠近按 E 就能触发对话（可重叠走过去）
        var trigger = go.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 2.0f; // 交互范围（随 localScale=npcSize 缩放；比之前略大，靠近就能按 E）
        var npc = go.AddComponent<NPC>();
        // 剧情内容默认取代码里的 StoryDialogue.Level1；LevelData.npcDialogue 若在 Inspector 填了则覆盖
        if (levelData != null && levelData.npcDialogue != null && levelData.npcDialogue.Count > 0)
            npc.dialogue = levelData.npcDialogue.ToArray();
        else
            npc.dialogue = StoryDialogue.Level1;
    }

    /// <summary>最终抉择（只第 3 关）：触发区放在「倒数第二个房间 → 出口房」的过道中段，
    /// 玩家穿过过道必然踩中。红线只是过道里的可视化标记。</summary>
    void SpawnFinalChoice(RoomDef portalRoom)
    {
        int portalIdx = currentRooms.IndexOf(portalRoom);
        if (portalIdx <= 0) return; // 出口房必须是链上的非首房，才有「来的过道」
        Vector2 prev = currentRooms[portalIdx - 1].center;
        Vector2 mid = (prev + portalRoom.center) * 0.5f;
        bool horizontal = !Mathf.Approximately(prev.x, portalRoom.center.x); // 过道走向

        // 红线条（纯可视化，横在过道正中）。注意：CreateBlock 会把 localScale 拉成尺寸，
        // 碰撞体绝不能挂在它身上——半径会跟着 localScale 乘成巨型椭圆（历史 bug）。
        Vector2 lineSize = horizontal ? new Vector2(0.5f, corridorWidth) : new Vector2(corridorWidth, 0.5f);
        ColorBlockFactory.CreateBlock("FinalChoiceLine", mid, lineSize, finalChoiceColor, transform, false, 3);

        // 触发区挂在独立的无缩放物体上，半径按过道宽度取：保证过道横截面全覆盖，玩家必然触发
        var trigGo = new GameObject("FinalChoiceTrigger");
        trigGo.transform.SetParent(transform, false);
        trigGo.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
        var col = trigGo.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = corridorWidth * 0.75f;
        trigGo.AddComponent<FinalChoiceTrigger>();
    }

    /// <summary>GROW（狂暴模式）：出口房刷一波守卫（血量按狂暴标准），清完由 GameManager 解锁传送阵。</summary>
    public void SpawnRageWave()
    {
        if (portalRoomRef == null) return;
        int exitIdx = currentRooms.IndexOf(portalRoomRef);
        int gateTier = Mathf.Max(0, exitIdx - 1); // 玩家在过道时 furthestReachedRoom = 出口前一房，保证怪物立即激活

        // 四种守卫轮换刷，共 rageWaveCount 只（默认 10 只 = 「大量怪物」）
        var types = new EnemyType[] { EnemyType.NeedleGirl, EnemyType.Capsule, EnemyType.GreenBag, EnemyType.Antigen };
        int count = Mathf.Max(1, rageWaveCount);

        var points = ScatterContent(portalRoomRef, count);
        var wave = new List<EnemyCell>();
        for (int i = 0; i < count; i++)
        {
            var e = SpawnEnemy(points[i], gateTier, types[i % types.Length], 5);
            if (e != null) wave.Add(e);
        }
        if (GameManager.Instance != null) GameManager.Instance.RegisterRageWave(wave);
    }

    /// <summary>[已停用] NO 分支现在直接显示传送门（ShowPortal），不再摆祭坛。此方法保留备用，不再被调用。</summary>
    public void SpawnPeaceMode()
    {
        if (portalObject != null) Destroy(portalObject);
        if (portalRoomRef == null) return;
        SpawnAltar(portalRoomRef.center);
    }

    void SpawnAltar(Vector2 pos)
    {
        var go = ColorBlockFactory.CreateBlock("Altar", pos, new Vector2(1.8f, 1.8f), altarColor, transform, false, 2);
        var solid = go.AddComponent<CircleCollider2D>();
        solid.radius = 0.7f; // 实体：挡在祭坛前，不能穿过去
        var trigger = go.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 2.2f; // 交互范围：靠近按 E 献祭
        go.AddComponent<Altar>();
    }
}
