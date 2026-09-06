using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时色块工厂（2D 俯视）：零美术资源。
/// 动态生成 1x1 白色纹理并创建 Sprite，之后用 SpriteRenderer.color 染色即可得到任意颜色方块。
/// 也支持传入美术给的 Sprite（MapGenerator 里拖拽赋值）。
/// </summary>
public static class ColorBlockFactory
{
    private static Sprite _whiteSprite;

    // 源精灵 → 「整张纹理 + Full Rect」精灵。Tiled 平铺必须用 Full Rect 网格，
    // 若美术图导入成 Tight 网格，平铺会异常（障碍/墙/地板会看不见或变形）。
    private static readonly Dictionary<Sprite, Sprite> _fullRectCache = new Dictionary<Sprite, Sprite>();

    /// <summary>按锚点(pivot)获取一个白色方块 Sprite。中心锚点会缓存复用。</summary>
    public static Sprite GetSprite(Vector2 pivot)
    {
        if (pivot.x == 0.5f && pivot.y == 0.5f)
        {
            if (_whiteSprite == null)
                _whiteSprite = MakeSprite(new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }
        return MakeSprite(pivot);
    }

    /// <summary>中心锚点的白色方块 Sprite（最常用）。</summary>
    public static Sprite WhiteSprite => GetSprite(new Vector2(0.5f, 0.5f));

    private static Sprite _circleSprite;

    /// <summary>实心圆 Sprite（原生 1x1 世界单位、中心锚点、边缘柔和过渡，用于攻击范围等指示圈）。</summary>
    public static Sprite CircleSprite
    {
        get
        {
            if (_circleSprite == null)
                _circleSprite = MakeCircleSprite(128, 0.05f);
            return _circleSprite;
        }
    }

    private static Sprite MakeCircleSprite(int res, float softness)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.name = "AttackRangeCircle";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float fx = (x + 0.5f) / res * 2f - 1f; // -1..1
                float fy = (y + 0.5f) / res * 2f - 1f;
                float d = Mathf.Sqrt(fx * fx + fy * fy);
                // 实心圆，边缘按 softness 比例柔和过渡；透明处用白色(alpha=0)避免黑边
                float a = 1f - Mathf.Clamp01((d - (1f - softness)) / softness);
                pixels[y * res + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        // pixelsPerUnit = res：sprite 原生尺寸 = 1x1 世界单位
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private static Sprite _vignetteSprite;

    /// <summary>径向渐变 Sprite：中心透明、四周实心白色。给 UI Image 染色即可做「屏幕边缘泛红」。</summary>
    public static Sprite VignetteSprite
    {
        get
        {
            if (_vignetteSprite == null)
                _vignetteSprite = MakeVignetteSprite(128);
            return _vignetteSprite;
        }
    }

    private static Sprite MakeVignetteSprite(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.name = "Vignette";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float fx = (x + 0.5f) / res * 2f - 1f; // -1..1
                float fy = (y + 0.5f) / res * 2f - 1f;
                float d = Mathf.Sqrt(fx * fx + fy * fy);
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f); // 中心透明、边缘实心
                pixels[y * res + x] = new Color(1f, 1f, 1f, a * a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private static Sprite MakeSprite(Vector2 pivot)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        // pixelsPerUnit=1：sprite 本地尺寸就是 1x1 世界单位，方便用 localScale 控制大小
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), pivot, 1f);
    }

    /// <summary>
    /// 创建一个方块 GameObject（含 SpriteRenderer，可选 BoxCollider2D）。
    /// </summary>
    /// <param name="position">若 parent 为 null 则是世界坐标，否则是相对 parent 的本地坐标。</param>
    /// <param name="size">世界尺寸（宽, 高）。</param>
    /// <param name="sprite">美术提供的 Sprite；传 null 则用默认白色方块。</param>
    public static GameObject CreateBlock(string name, Vector2 position, Vector2 size, Color color,
                                         Transform parent = null, bool addCollider = false,
                                         int sortingOrder = 0, Vector2? pivot = null, Sprite sprite = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        }
        else
        {
            go.transform.position = new Vector3(position.x, position.y, 0f);
        }
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : GetSprite(pivot ?? new Vector2(0.5f, 0.5f));
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        if (addCollider)
        {
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one; // collider 随 localScale 一起缩放，最终就是 size
        }
        return go;
    }

    /// <summary>
    /// 取「整张纹理 + Full Rect」的精灵，供 Tiled 平铺使用。
    /// 原精灵若被导入成 Tight 网格（Sprite 导入默认），Tiled 会提示
    /// "not generated with Full Rect" 并平铺异常；这里用同一张纹理重建一个
    /// Full Rect 精灵即可正常平铺。结果按源精灵缓存复用。
    /// </summary>
    static Sprite GetFullRectSprite(Sprite source)
    {
        if (source == null || source.texture == null) return source;
        if (_fullRectCache.TryGetValue(source, out var cached)) return cached;

        Texture2D tex = source.texture;
        float ppu = source.pixelsPerUnit > 0.0001f ? source.pixelsPerUnit : 100f;
        var fullRect = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        fullRect.name = (source.name ?? "sprite") + "_FullRect";
        _fullRectCache[source] = fullRect;
        return fullRect;
    }

    // 平铺材质缓存：一张纹理一个共享材质。颜色用网格顶点色染（Sprites/Default 支持），
    // 不同颜色不需要克隆材质。
    private static readonly Dictionary<Texture2D, Material> _tileMatCache = new Dictionary<Texture2D, Material>();

    private static Material GetTileMaterial(Texture2D tex)
    {
        if (!_tileMatCache.TryGetValue(tex, out var mat) || mat == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent"); // 极端兜底
            mat = new Material(shader) { mainTexture = tex };
            _tileMatCache[tex] = mat;
        }
        return mat;
    }

    /// <summary>
    /// 创建一个「平铺」方块，给地板砖 / 墙砖用。一个贴图重复单元的世界尺寸 = tileSize。
    /// 砖缝全局对齐（用户判据：所有扩散点距基准点都是 tileSize 整数倍）：
    /// 不用 SpriteRenderer 的 Tiled 模式（它从各块自己的中心起算相位，块间相位必然错开，
    /// 接缝处砖被切在不同位置 → 阶梯状错缝），而是自建四边形 Mesh，
    /// UV = 矩形角点世界坐标 ÷ tileSize —— 全图所有块采样同一张以原点为基准的无限网格，
    /// 相位天然一致，接缝处采样连续，砖缝必然对齐、边缘无半砖。
    /// 矩形完全等于逻辑矩形（仅四边外扩 0.01 防光栅细缝），碰撞体直接挂本体（无缩放把戏），
    /// 不会外溢到墙外，游戏性完全不变。
    /// </summary>
    public static GameObject CreateTiledBlock(string name, Vector2 position, Vector2 size, Color color,
                                              Transform parent = null, int sortingOrder = 0,
                                              Sprite sprite = null, float tileSize = 1f, bool addCollider = false)
    {
        Texture2D tex = sprite != null ? sprite.texture : null;
        if (tex == null)
            return CreateBlock(name, position, size, color, parent, addCollider, sortingOrder);

        // Repeat 采样：UV 超出 0~1 时贴图循环重复（运行时设置立即生效）
        tex.wrapMode = TextureWrapMode.Repeat;

        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        }
        else
        {
            go.transform.position = new Vector3(position.x, position.y, 0f);
        }

        // 逻辑矩形四边各外扩 0.01：相邻块互相重叠一点点，
        // 消除像素光栅化在两块相接处漏出的背景细线；重叠区域采样同一网格，画面完全相同，看不出重叠。
        const float SeamOverlap = 0.02f;
        float x0 = position.x - (size.x + SeamOverlap) * 0.5f;
        float x1 = position.x + (size.x + SeamOverlap) * 0.5f;
        float y0 = position.y - (size.y + SeamOverlap) * 0.5f;
        float y1 = position.y + (size.y + SeamOverlap) * 0.5f;
        float inv = 1f / Mathf.Max(0.0001f, tileSize);

        // 四边形网格：顶点 TL,TR,BR,BL（Sprites/Default 双面渲染，朝向无所谓），
        // 局部坐标 = 角点 - 中心；UV = 角点坐标 ÷ tileSize（全局无限网格，跨块连续）
        var mesh = new Mesh { name = name + "_TileMesh" };
        mesh.vertices = new[]
        {
            new Vector3(x0 - position.x, y1 - position.y, 0f), // TL
            new Vector3(x1 - position.x, y1 - position.y, 0f), // TR
            new Vector3(x1 - position.x, y0 - position.y, 0f), // BR
            new Vector3(x0 - position.x, y0 - position.y, 0f), // BL
        };
        mesh.uv = new[]
        {
            new Vector2(x0 * inv, y1 * inv),
            new Vector2(x1 * inv, y1 * inv),
            new Vector2(x1 * inv, y0 * inv),
            new Vector2(x0 * inv, y0 * inv),
        };
        // 顶点色染 tint（Sprites/Default = 贴图 × 顶点色；不设可能读出黑色）
        Color32 c32 = color;
        mesh.colors32 = new[] { c32, c32, c32, c32 };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetTileMaterial(tex);
        mr.sortingOrder = sortingOrder;

        if (addCollider)
        {
            // 本体无缩放，碰撞体直接挂本体，size 即世界尺寸，恒等于逻辑 size。
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }
        return go;
    }
}
